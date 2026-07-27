using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class AirConditioningExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfAc_ReturnsNull(string? description)
    {
        Assert.Null(AirConditioningExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento equipado com ar condicionado em todas as divisões.")]
    [InlineData("T1 com ar-condicionado e aquecimento central.")]
    public void Extract_AcMentioned_ReturnsTrue(string description)
    {
        Assert.True(AirConditioningExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento sem ar condicionado.")]
    [InlineData("T2 não tem ar-condicionado instalado.")]
    public void Extract_NoAcMentioned_ReturnsFalse(string description)
    {
        Assert.False(AirConditioningExtractor.Extract(description));
    }
}
