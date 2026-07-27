using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

// Same pattern/precedence style as OrientationExtractor: a description can
// arguably mention more than one of these, so the checks below run in a
// fixed order and the first match wins rather than trying to be clever
// about which one is "more true".
public static class ConstructionStatusExtractor
{
    // Actively being built / not yet finished. Checked first: a listing
    // that's "em construção" is sometimes also marketed as "nova
    // construção" (it will be new once done), and "under construction" is
    // the more specific/actionable fact for a buyer (different timeline,
    // different financing, can't view a finished unit yet).
    private static readonly Regex EmConstrucaoPattern = new(
        @"em constru[cç][ãa]o|em fase de constru[cç][ãa]o|obra em curso|previs[ãa]o de (entrega|conclus[ãa]o)|conclus[ãa]o prevista",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Already built and new (as opposed to a resale of an older property).
    // "novo em folha" is deliberately excluded — it's generic marketing
    // slang ("brand new [condition]") used even for renovated older units,
    // not a reliable signal of actual new construction the way "nova
    // construção" or "recém construído" are.
    private static readonly Regex NovaConstrucaoPattern = new(
        @"nova constru[cç][ãa]o|constru[cç][ãa]o nova|pr[ée]dio novo|moradia nova|rec[ée]m[\s-]constru[íi]d[oa]|acabado de construir",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Needs renovation/repair before it's livable to a normal standard —
    // the PT real-estate term of art covers several phrasings ("para
    // recuperar", "para restaurar", "necessita de obras", "para remodelar",
    // "ruína").
    private static readonly Regex ParaRecuperarPattern = new(
        @"para recuperar|para restaurar|para reabilitar|necessita(ndo)? de obras|a necessitar de obras|por remodelar|para remodelar|em ru[íi]na|para recupera[cç][ãa]o",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Not Available";

        if (EmConstrucaoPattern.IsMatch(description))
            return "Em Construção";

        if (NovaConstrucaoPattern.IsMatch(description))
            return "Nova Construção";

        if (ParaRecuperarPattern.IsMatch(description))
            return "Para Recuperar";

        return "Not Available";
    }
}
