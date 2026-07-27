using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class EnergyRatingExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoRatingMentioned_ReturnsNull(string? description)
    {
        Assert.Null(EnergyRatingExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Excelente apartamento. Certificado Energético: B-", "B-")]
    [InlineData("Excelente apartamento. Certificado Energético B-.", "B-")]
    [InlineData("Categoria Energética: D", "D")]
    [InlineData("Classe Energética A+", "A+")]
    [InlineData("classe energética c, muito eficiente", "C")]
    public void Extract_RatingMentioned_ReturnsUppercaseRating(string description, string expected)
    {
        Assert.Equal(expected, EnergyRatingExtractor.Extract(description));
    }
}
