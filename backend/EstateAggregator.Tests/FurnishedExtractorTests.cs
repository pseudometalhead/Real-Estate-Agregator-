using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class FurnishedExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfFurniture_ReturnsNull(string? description)
    {
        Assert.Null(FurnishedExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento totalmente mobilado e equipado.")]
    [InlineData("T1 mobiliado, pronto a habitar.")]
    [InlineData("Fração parcialmente mobilada.")]
    public void Extract_FurnishedMentioned_ReturnsTrue(string description)
    {
        Assert.True(FurnishedExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento não mobilado, vazio.")]
    [InlineData("T2 não mobiliado, pronto para decorar ao seu gosto.")]
    [InlineData("Fração sem mobília.")]
    public void Extract_NotFurnishedMentioned_ReturnsFalse(string description)
    {
        Assert.False(FurnishedExtractor.Extract(description));
    }
}
