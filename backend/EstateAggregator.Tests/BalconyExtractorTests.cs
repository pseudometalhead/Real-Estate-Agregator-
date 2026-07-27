using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class BalconyExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfBalcony_ReturnsNull(string? description)
    {
        Assert.Null(BalconyExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento com varanda ampla e vista desafogada.")]
    [InlineData("T1 com terraço privativo de 20m2.")]
    [InlineData("Fração com terraco exclusivo.")]
    public void Extract_BalconyMentioned_ReturnsTrue(string description)
    {
        Assert.True(BalconyExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento sem varanda.")]
    [InlineData("T2 não tem terraço.")]
    public void Extract_NoBalconyMentioned_ReturnsFalse(string description)
    {
        Assert.False(BalconyExtractor.Extract(description));
    }
}
