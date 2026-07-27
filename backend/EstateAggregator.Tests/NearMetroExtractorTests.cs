using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class NearMetroExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apartamento T2 com bons acessos rodoviários.")]
    public void Extract_NoMetroMentioned_ReturnsNull(string? description)
    {
        Assert.Null(NearMetroExtractor.Extract(description));
    }

    [Theory]
    [InlineData("A poucos minutos da estação de metro da Trindade.")]
    [InlineData("Perto do metro e de transportes públicos.")]
    [InlineData("A 280 metros da estação de Metro do Bolhão.")]
    public void Extract_MetroMentioned_ReturnsTrue(string description)
    {
        Assert.True(NearMetroExtractor.Extract(description));
    }

    // "metros" (the distance unit) must NOT be confused with "metro" (the
    // subway) — the word-boundary in the pattern is what prevents this.
    [Fact]
    public void Extract_MetrosAsDistanceUnit_DoesNotFalsePositive()
    {
        Assert.Null(NearMetroExtractor.Extract("A casa tem 120 metros quadrados de área."));
    }
}
