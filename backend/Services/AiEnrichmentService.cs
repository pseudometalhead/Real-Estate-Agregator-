using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using EstateAggregator.Data;
using Microsoft.EntityFrameworkCore;

namespace EstateAggregator.Services;

// Extracts facts a regex extractor can't reliably catch (floor, condo fee,
// pool, garden, year built — see Property.cs's comment above those columns)
// out of each property's Description using Claude Haiku 4.5, and rewrites
// Description down to whatever's left over that isn't already captured by a
// column — see PropertyDto.AiEnrichedAt for how the frontend knows a
// description has been through this.
//
// This calls the Anthropic API directly in-process rather than shelling out
// to a Python script against the same sqlite file — an earlier version did
// exactly that, and it reliably failed with "unable to open database file"
// the moment a second process tried to open the file for read/write while
// this app already had it open. Verified live: with the backend stopped, a
// second process CAN open the file read-write fine; with it running, even a
// bare `sqlite3.connect()` + read fails. That's a known limitation of
// SQLite's POSIX locking over Docker Desktop's Windows bind-mount layer
// (docker-compose.yml's `./data:/data` mount) — not something worth working
// around when calling the same API natively from the process that already
// owns the DbContext sidesteps it completely.
public class AiEnrichmentService
{
    private readonly EstateDbContext _db;
    private readonly ILogger<AiEnrichmentService> _logger;

    private const string Model = "claude-haiku-4-5";
    // Bounds per-run API spend/latency when a backlog exists (e.g. right
    // after this feature ships, or after a from-scratch re-scrape) — the
    // remainder picks up on the next scrape run since unprocessed rows
    // (AiEnrichedAt IS NULL) are always requeried from scratch.
    private const int LimitPerRun = 40;

    private const string SystemPrompt = """
        You extract structured facts from Portuguese real-estate listing descriptions for a personal property-aggregator app.

        The app already has separate, reliable extraction for these — do NOT extract or mention them again in extraInfo: sun orientation, open-plan kitchen, construction status (new/needs renovation/completed), elevator, parking, furnished, air conditioning, balcony/terrace, renovated, storage room (arrecadação/arrumo), water/river/sea view, proximity to metro, energy rating, room count, size, price. Also remove agency self-promotion (company history, "contact us" calls to action, generic marketing language about the agency itself, not the property) — that is not property information.

        Some listings repeat the entire description multiple times, once per language (Portuguese, then the same text again in English, French, German, Spanish, ...), sometimes with a "this translation was AI-assisted" disclaimer after each block. When that happens, keep only the original Portuguese content and drop every translated repeat entirely, disclaimers included — they add nothing a Portuguese reader doesn't already have.

        Extract exactly these fields:
        - floor: this unit's own floor exactly as written (e.g. "2º", "R/C", "Cave", "Sótão"), or null if not mentioned.
        - totalFloors: total floors in the building, or null if not mentioned.
        - condoFeeMonthly: monthly condominium/maintenance fee in euros as a plain number with no currency symbol, or null if not mentioned.
        - hasPool: true only if a pool belonging to this specific property/building is mentioned, false if the text explicitly says there isn't one, null if not mentioned at all.
        - hasGarden: same rule for a private garden/yard (jardim/quintal) — not a public park nearby.
        - yearBuilt: the year the building was constructed, or null if not mentioned.
        - extraInfo: the description rewritten in Portuguese, keeping ONLY information not already captured above or by the app's existing extraction (room-by-room layout, neighborhood description, distances to amenities, anything genuinely distinctive), with agency self-promotion and calls-to-action removed. Do not add headers, bullet points, or commentary that weren't in the original — just trim it down. If nothing meaningful remains, return an empty string.
        """;

    private static readonly Dictionary<string, JsonElement> ExtractionSchema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
            "type": "object",
            "properties": {
                "floor": { "type": ["string", "null"] },
                "totalFloors": { "type": ["integer", "null"] },
                "condoFeeMonthly": { "type": ["number", "null"] },
                "hasPool": { "type": ["boolean", "null"] },
                "hasGarden": { "type": ["boolean", "null"] },
                "yearBuilt": { "type": ["integer", "null"] },
                "extraInfo": { "type": "string" }
            },
            "required": ["floor", "totalFloors", "condoFeeMonthly", "hasPool", "hasGarden", "yearBuilt", "extraInfo"],
            "additionalProperties": false
        }
        """)!;

    public AiEnrichmentService(EstateDbContext db, ILogger<AiEnrichmentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogInformation("ANTHROPIC_API_KEY not set — skipping AI fact-extraction pass");
            return;
        }

        var properties = await _db.Properties
            .Where(p => p.AiEnrichedAt == null && p.Description != null && p.Description != "")
            .Take(LimitPerRun)
            .ToListAsync(cancellationToken);

        if (properties.Count == 0)
            return;

        var client = new AnthropicClient { ApiKey = apiKey };
        var processed = 0;
        var failed = 0;

        foreach (var property in properties)
        {
            try
            {
                var facts = await ExtractFactsAsync(client, property.Description!, cancellationToken);
                if (facts == null)
                {
                    // Refused by the safety classifier — mark processed anyway
                    // so this exact description isn't retried every run. If it
                    // later genuinely changes, DeduplicationService.ProcessAsync
                    // resets AiEnrichedAt to null and it's picked up again.
                    property.AiEnrichedAt = DateTime.UtcNow;
                    processed++;
                    continue;
                }

                property.Floor = facts.Value.TryGetProperty("floor", out var floorEl) && floorEl.ValueKind == JsonValueKind.String
                    ? floorEl.GetString() : null;
                property.TotalFloors = facts.Value.TryGetProperty("totalFloors", out var totalFloorsEl) && totalFloorsEl.ValueKind == JsonValueKind.Number
                    ? totalFloorsEl.GetInt32() : null;
                property.CondoFeeMonthly = facts.Value.TryGetProperty("condoFeeMonthly", out var condoFeeEl) && condoFeeEl.ValueKind == JsonValueKind.Number
                    ? condoFeeEl.GetDecimal() : null;
                property.HasPool = facts.Value.TryGetProperty("hasPool", out var poolEl) && poolEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? poolEl.GetBoolean() : null;
                property.HasGarden = facts.Value.TryGetProperty("hasGarden", out var gardenEl) && gardenEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? gardenEl.GetBoolean() : null;
                property.YearBuilt = facts.Value.TryGetProperty("yearBuilt", out var yearEl) && yearEl.ValueKind == JsonValueKind.Number
                    ? yearEl.GetInt32() : null;

                var extraInfo = facts.Value.TryGetProperty("extraInfo", out var extraInfoEl) ? extraInfoEl.GetString() : null;
                property.Description = string.IsNullOrEmpty(extraInfo) ? null : extraInfo;
                property.AiEnrichedAt = DateTime.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                // One bad response/timeout shouldn't stop the rest of the
                // batch — this property just gets retried next run.
                _logger.LogWarning(ex, "AI fact extraction failed for property {Id}", property.Id);
                failed++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("AI fact extraction: processed {Processed}, failed {Failed}", processed, failed);
    }

    private static async Task<JsonElement?> ExtractFactsAsync(AnthropicClient client, string description, CancellationToken cancellationToken)
    {
        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 2048,
            System = SystemPrompt,
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = ExtractionSchema } },
            Messages = [new() { Role = Role.User, Content = description }],
        }, cancellationToken);

        if (response.StopReason == "refusal")
            return null;

        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault();

        return string.IsNullOrEmpty(text) ? null : JsonSerializer.Deserialize<JsonElement>(text);
    }
}
