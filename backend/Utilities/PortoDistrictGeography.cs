using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Fixes a specific class of bad data: BackfillLocationHierarchyAsync (the
// one-time migration backfill) and some scrapers' pre-3-level-hierarchy
// history left many properties with a civil-parish (freguesia) name sitting
// in the Concelho field — e.g. "Pombeiro de Ribavizela" is a freguesia of
// Felgueiras, not a concelho in its own right. This resolver maps a
// (possibly mis-slotted) freguesia name back to its real concelho, scoped to
// the district this app actually operates in.
//
// Data verified against Wikipedia's post-2013 (post-parish-merger) civil
// parish lists for each of Porto district's 18 concelhos. Deliberately
// leaves ambiguous single-parish names unresolved (e.g. "Madalena" is a
// parish of both Amarante and Vila Nova de Gaia) rather than guessing —
// showing nothing new is better than showing a confidently wrong concelho,
// which is exactly the bug this exists to fix.
public static class PortoDistrictGeography
{
    private static readonly Dictionary<string, string[]> ConcelhoFreguesias = new()
    {
        ["Felgueiras"] = new[] { "Aião", "Airães", "Friande", "Idães", "Jugueiros", "Macieira da Lixa e Caramos", "Margaride (Santa Eulália), Várzea, Lagares, Varziela e Moure", "Pedreira, Rande e Sernande", "Penacova", "Pinheiro", "Pombeiro de Ribavizela", "Refontoura", "Regilde", "Revinhade", "Sendim", "Torrados e Sousa", "Unhão e Lordelo", "Vila Cova da Lixa e Borba de Godim", "Vila Fria e Vizela (São Jorge)", "Vila Verde e Santão" },
        ["Amarante"] = new[] { "Aboadela, Sanche e Várzea", "Amarante (São Gonçalo), Madalena, Cepelos e Gatão", "Ansiães", "Bustelo, Carneiro e Carvalho de Rei", "Candemil", "Figueiró (Santiago e Santa Cristina)", "Fregim", "Freixo de Cima e de Baixo", "Fridão", "Gondar", "Jazente", "Lomba", "Louredo", "Lufrei", "Mancelos", "Olo e Canadelo", "Padronelo", "Real, Ataíde e Oliveira", "Rebordelo", "Salvador do Monte", "São Simão de Gouveia", "Telões", "Travanca", "Vila Caiz", "Vila Chã do Marão", "Vila Garcia, Aboim e Chapa" },
        ["Gondomar"] = new[] { "Baguim do Monte", "Fânzeres e São Pedro da Cova", "Foz do Sousa e Covelo", "Lomba", "Melres e Medas", "Rio Tinto", "Gondomar (São Cosme), Valbom e Jovim" },
        ["Paredes"] = new[] { "Aguiar de Sousa", "Astromil", "Baltar", "Beire", "Cete", "Cristelo", "Duas Igrejas", "Gandra", "Lordelo", "Louredo", "Parada de Todeia", "Paredes", "Rebordosa", "Recarei", "Sobreira", "Sobrosa", "Vandoma", "Vilela" },
        ["Lousada"] = new[] { "Aveleda", "Caíde de Rei", "Cernadelo e Lousada (São Miguel e Santa Margarida)", "Cristelos, Boim e Ordem", "Figueiras e Covas", "Lodares", "Lustosa e Barrosas (Santo Estêvão)", "Macieira", "Meinedo", "Nespereira e Casais", "Nevogilde", "Silvares, Pias, Nogueira e Alvarenga", "Sousela", "Torno", "Vilar do Torno e Alentém" },
        ["Penafiel"] = new[] { "Abragão", "Boelhe", "Bustelo", "Cabeça Santa", "Canelas", "Capela", "Castelões", "Croca", "Duas Igrejas", "Eja", "Fonte Arcada", "Galegos", "Guilhufe e Urrô", "Irivo", "Lagares e Figueira", "Luzim e Vila Cova", "Oldrões", "Paço de Sousa", "Penafiel", "Perozelo", "Rans", "Rio de Moinhos", "Rio Mau", "São Mamede de Recezinhos", "São Martinho de Recezinhos", "Sebolido", "Termas de São Vicente", "Valpedre" },
        ["Vila Nova de Gaia"] = new[] { "Arcozelo", "Avintes", "Canelas", "Canidelo", "Grijó e Sermonde", "Gulpilhares e Valadares", "Madalena", "Mafamude e Vilar do Paraíso", "Oliveira do Douro", "Pedroso e Seixezelo", "Sandim, Olival, Lever e Crestuma", "Santa Marinha e São Pedro da Afurada", "São Félix da Marinha", "Serzedo e Perosinho", "Vilar de Andorinho" },
        ["Maia"] = new[] { "Águas Santas", "Castêlo da Maia", "Cidade da Maia", "Folgosa", "Milheirós", "Moreira", "Nogueira e Silva Escura", "Pedrouços", "São Pedro Fins", "Vila Nova da Telha" },
        ["Matosinhos"] = new[] { "Custóias, Leça do Balio e Guifões", "Matosinhos e Leça da Palmeira", "Perafita, Lavra e Santa Cruz do Bispo", "São Mamede de Infesta e Senhora da Hora" },
        ["Valongo"] = new[] { "Alfena", "Campo e Sobrado", "Ermesinde", "Valongo" },
        ["Santo Tirso"] = new[] { "Agrela", "Água Longa", "Areias, Sequeiró, Lama e Palmeira", "Aves", "Carreira e Refojos de Riba de Ave", "Lamelas e Guimarei", "Monte Córdova", "Rebordões", "Reguenga", "Roriz", "Santo Tirso, Couto (Santa Cristina e São Miguel) e Burgães", "São Tomé de Negrelos", "Vila Nova do Campo", "Vilarinho" },
        ["Trofa"] = new[] { "Alvarelhos e Guidões", "Bougado (São Martinho e Santiago)", "Coronado (São Romão e São Mamede)", "Covelas", "Muro" },
        ["Paços de Ferreira"] = new[] { "Carvalhosa", "Eiriz", "Ferreira", "Figueiró", "Frazão", "Arreigada", "Freamunde", "Meixomil", "Paços de Ferreira", "Penamaior", "Raimonda", "Sanfins de Ferreira", "Seroa" },
        ["Marco de Canaveses"] = new[] { "Alpendorada, Várzea e Torrão", "Avessadas e Rosém", "Banho e Carvalhosa", "Bem Viver", "Constance", "Marco", "Paredes de Viadores e Manhuncelos", "Penhalonga e Paços de Gaiolo", "Sande e São Lourenço do Douro", "Santo Isidoro e Livração", "Soalhães", "Sobretâmega", "Tabuado", "Várzea, Aliviada e Folhada", "Vila Boa de Quires e Maureles", "Vila Boa do Bispo" },
        ["Baião"] = new[] { "Ancede e Ribadouro", "Baião (Santa Leocádia) e Mesquinhata", "Campelo e Ovil", "Frende", "Gestaçô", "Gove", "Grilo", "Loivos da Ribeira e Tresouras", "Loivos do Monte", "Santa Cruz do Douro e São Tomé de Covelas", "Santa Marinha do Zêzere", "Teixeira e Teixeiró", "Valadares", "Viariz" },
        ["Póvoa de Varzim"] = new[] { "Aguçadoura", "Amorim", "Aver-o-Mar", "Balazar", "Beiriz", "Estela", "Laundos", "Navais", "Póvoa de Varzim, Beiriz e Argivai", "Rates", "Terroso", "Aver-o-Mar, Amorim e Terroso", "Argivai" },
        ["Vila do Conde"] = new[] { "Árvore", "Aveleda", "Azurara", "Bagunte, Ferreiró, Outeiro Maior e Parada", "Fajozes", "Fornelo e Vairão", "Gião", "Guilhabreu", "Junqueira", "Labruge", "Macieira da Maia", "Malta e Canidelo", "Mindelo", "Modivas", "Retorta e Tougues", "Rio Mau e Arcos", "Touguinha e Touguinhó", "Vila Chã", "Vila do Conde", "Vilar e Mosteiró", "Vilar de Pinheiro" },
        ["Porto"] = new[] { "Aldoar, Foz do Douro e Nevogilde", "Bonfim", "Campanhã", "Cedofeita, Santo Ildefonso, Sé, Miragaia, São Nicolau e Vitória", "Lordelo do Ouro e Massarelos", "Paranhos", "Ramalde" },
    };

    private static readonly Regex ConnectorWordPattern = new(@"\b(de|do|da|dos|das)\b", RegexOptions.Compiled);
    private static readonly Regex NonAlphanumericPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex ParentheticalPattern = new(@"\s*\([^)]*\)", RegexOptions.Compiled);
    private static readonly char[] ComponentSeparators = { ',' };

    private static readonly Dictionary<string, string> FreguesiaToConcelho = BuildLookup();

    private static string Normalize(string s)
    {
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var lower = sb.ToString().ToLowerInvariant();
        var alnumOnly = NonAlphanumericPattern.Replace(lower, " ").Trim();
        var noConnectors = ConnectorWordPattern.Replace(alnumOnly, "");
        return WhitespacePattern.Replace(noConnectors, " ").Trim();
    }

    // Splits a merged parish name into its historic components — e.g.
    // "Amarante (São Gonçalo), Madalena, Cepelos e Gatão" -> "Amarante (São
    // Gonçalo)", "Madalena", "Cepelos", "Gatão", plus each with any
    // parenthetical qualifier stripped, and the name as a whole. Also
    // handles the "-"-joined variant some scrapers/backfills produced for
    // the same names ("Amarante (São Gonçalo) - Madalena - Cepelos - Gatão").
    private static HashSet<string> SplitComponents(string name)
    {
        var parts = Regex.Split(name, @",|\se\s|\s-\s")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var result = new HashSet<string>(parts) { name };
        foreach (var p in parts)
        {
            var stripped = ParentheticalPattern.Replace(p, "").Trim();
            if (stripped.Length > 0)
                result.Add(stripped);
        }
        var strippedFull = ParentheticalPattern.Replace(name, "").Trim();
        if (strippedFull.Length > 0)
            result.Add(strippedFull);

        return result;
    }

    private static Dictionary<string, string> BuildLookup()
    {
        var lookup = new Dictionary<string, string>();
        var conflicted = new HashSet<string>();

        foreach (var (concelho, freguesias) in ConcelhoFreguesias)
        {
            var keys = new Dictionary<string, string> { [Normalize(concelho)] = concelho };
            foreach (var f in freguesias)
            {
                foreach (var variant in SplitComponents(f))
                {
                    var key = Normalize(variant);
                    if (key.Length > 0)
                        keys[key] = concelho;
                }
            }

            foreach (var (key, resolvedConcelho) in keys)
            {
                if (lookup.TryGetValue(key, out var existing) && existing != resolvedConcelho)
                    conflicted.Add(key);
                else
                    lookup[key] = resolvedConcelho;
            }
        }

        foreach (var key in conflicted)
            lookup.Remove(key);

        return lookup;
    }

    // Given whatever currently sits in a property's Concelho field, tries to
    // resolve the REAL concelho. Returns null if the value is already a
    // recognized concelho name (nothing to fix) or can't be matched
    // unambiguously (left alone rather than guessed).
    public static string? ResolveConcelho(string? possiblyMisplacedFreguesia)
    {
        if (string.IsNullOrWhiteSpace(possiblyMisplacedFreguesia))
            return null;

        var key = Normalize(possiblyMisplacedFreguesia);
        if (FreguesiaToConcelho.TryGetValue(key, out var direct))
            return direct;

        // The stored value may itself be a "-"/","-joined combination of
        // parish-name components (e.g. scraped/backfilled before the real
        // union name was known) — if every recognizable component agrees on
        // one concelho, that's the answer; anything else is left alone.
        var candidates = SplitComponents(possiblyMisplacedFreguesia)
            .Select(Normalize)
            .Where(k => k.Length > 0 && FreguesiaToConcelho.ContainsKey(k))
            .Select(k => FreguesiaToConcelho[k])
            .Distinct()
            .ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    // True only when the resolved concelho differs from what's already
    // stored — i.e. there's an actual correction to make, not a no-op.
    public static bool TryFixMisplacedConcelho(string? storedConcelho, out string correctConcelho, out string freguesia)
    {
        correctConcelho = string.Empty;
        freguesia = string.Empty;

        if (string.IsNullOrWhiteSpace(storedConcelho))
            return false;

        var resolved = ResolveConcelho(storedConcelho);
        if (resolved == null || string.Equals(resolved, storedConcelho, StringComparison.Ordinal))
            return false;

        correctConcelho = resolved;
        freguesia = storedConcelho;
        return true;
    }
}
