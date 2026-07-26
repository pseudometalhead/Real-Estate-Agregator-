namespace EstateAggregator.Utilities;

// Scrapers that build a district-specific search URL (ImoVirtual, CasaSapo)
// need an English/Portuguese alias -> URL slug. Scrapers that instead fetch
// broadly and filter by matching a district name against listing location
// text (CaixaImobiliario, Santander) need the same alias resolved to its
// Portuguese form first — otherwise a user-configured "Lisbon" never
// matches a listing's "Lisboa" text, since one isn't a substring of the
// other. Both needs share this one alias table.
public static class DistrictAliases
{
    private static readonly Dictionary<string, string> EnglishToPortuguese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lisbon"] = "Lisboa",
        ["Oporto"] = "Porto",
        ["Evora"] = "Évora",
        ["Setubal"] = "Setúbal",
        ["Santarem"] = "Santarém",
        ["Braganca"] = "Bragança",
    };

    // Returns the Portuguese form of a district name if a known English
    // alias is given, otherwise returns the input unchanged (it may already
    // be Portuguese, or simply not have a known alias).
    public static string ToPortuguese(string district) =>
        EnglishToPortuguese.TryGetValue(district.Trim(), out var pt) ? pt : district;
}
