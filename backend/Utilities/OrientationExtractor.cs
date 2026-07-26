namespace EstateAggregator.Utilities;

public static class OrientationExtractor
{
    private static readonly string[] SulKeywords = { "fachada sul", "frente sul", " sul ", " a sul" };
    private static readonly string[] NorteKeywords = { "fachada norte", "frente norte", " norte ", "virada a norte" };
    private static readonly string[] OrienteKeywords = { "nascente", "oriente", " este ", " leste " };
    private static readonly string[] PoenteKeywords = { "poente", " oeste " };

    public static (string SunOrientation, string OrientationSource) Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return ("Not Available", "not_available");

        var padded = " " + description.ToLowerInvariant() + " ";

        if (ContainsAny(padded, SulKeywords))
            return ("Sul", "extracted");

        if (ContainsAny(padded, NorteKeywords))
            return ("Norte", "extracted");

        if (ContainsAny(padded, OrienteKeywords))
            return ("Oriente", "extracted");

        if (ContainsAny(padded, PoenteKeywords))
            return ("Poente", "extracted");

        return ("Not Available", "not_available");
    }

    private static bool ContainsAny(string text, string[] keywords)
        => keywords.Any(text.Contains);
}
