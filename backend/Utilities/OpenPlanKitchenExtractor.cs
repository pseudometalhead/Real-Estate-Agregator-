using System.Text.RegularExpressions;

namespace EstateAggregator.Utilities;

public static class OpenPlanKitchenExtractor
{
    // Kitchen combined with the English loanword "open space" (very common in
    // PT real-estate listings), in either order and allowing a short run of
    // other words in between ("cozinha em open space", "cozinha equipada em
    // open space com a sala", "open space entre cozinha e sala").
    private static readonly Regex KitchenOpenSpacePattern = new(
        @"cozinha[^.\n]{0,40}open[\s-]?space|open[\s-]?space[^.\n]{0,40}cozinha",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "cozinha aberta para/à/a (a) sala" and the reverse "sala aberta para/à/a
    // (a) cozinha", plus "sala e cozinha em espaço/conceito aberto" (and the
    // cozinha-first ordering). These are deliberately NOT a bare
    // \baberta?\b / \baberto\b check: that alone is far too generic and
    // false-positives constantly on unrelated PT listing phrases like
    // "varanda aberta" (open balcony) or "vista aberta" (open view). Every
    // pattern here requires "aberta/aberto" to sit directly between (or right
    // next to) both "cozinha" and "sala" so the match is unambiguously about
    // the kitchen/living-room layout, not some other open thing in the flat.
    private static readonly Regex KitchenLivingOpenPattern = new(
        @"cozinha abert[ao]\s*(para|à|a)\s*(a\s+)?sala|sala abert[ao]\s*(para|à|a)\s*(a\s+)?cozinha|sala\s*(e|,)?\s*cozinha\s*(em\s+)?(espaço|conceito)\s*abert[oa]|cozinha\s*(e|,)?\s*sala\s*(em\s+)?(espaço|conceito)\s*abert[oa]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "cozinha americana" is a standalone PT real-estate term of art for an
    // open (breakfast-bar style) kitchen open to the living room — no need to
    // pair it with "sala", the phrase itself is unambiguous.
    private static readonly Regex KitchenAmericanaPattern = new(
        @"cozinha american[ao]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "conceito open space" and the bare English phrases "open space" /
    // "open-plan" / "open plan". Unlike "aberta"/"aberto" these compound
    // phrases are not generic PT words that show up in unrelated contexts —
    // in PT listings "open space" is used specifically to mean this layout
    // concept, so it's trusted on its own per the task's note that the
    // English phrasing sometimes appears directly without a "cozinha"/"sala"
    // anchor nearby.
    private static readonly Regex OpenSpaceConceptPattern = new(
        @"conceito\s*open[\s-]?space|\bopen[\s-]?space\b|\bopen[\s-]?plan\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Returns true if an open-plan kitchen/living-room phrase is confidently
    // detected in the description. There is no reliable way to infer a
    // CLOSED kitchen from the mere absence of one of these phrases (most
    // listings simply don't mention the layout at all), so this never
    // returns false — only true ("mentioned") or null ("not mentioned").
    public static bool? Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        if (KitchenOpenSpacePattern.IsMatch(description)
            || KitchenLivingOpenPattern.IsMatch(description)
            || KitchenAmericanaPattern.IsMatch(description)
            || OpenSpaceConceptPattern.IsMatch(description))
        {
            return true;
        }

        return null;
    }
}
