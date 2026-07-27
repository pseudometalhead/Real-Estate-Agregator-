using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class RenovatedExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    [InlineData("Apartamento para recuperar, com bom potencial.")]
    public void Extract_NoMentionOfRenovation_ReturnsNull(string? description)
    {
        Assert.Null(RenovatedExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento totalmente remodelado, pronto a habitar.")]
    [InlineData("T2 recentemente renovado, com acabamentos modernos.")]
    [InlineData("Prédio com obras de renovação recentes.")]
    [InlineData("Casa renovada em 2024.")]
    public void Extract_RenovationMentioned_ReturnsTrue(string description)
    {
        Assert.True(RenovatedExtractor.Extract(description));
    }
}
