using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class OpenPlanKitchenExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_NullOrEmptyDescription_ReturnsNull(string? description)
    {
        var result = OpenPlanKitchenExtractor.Extract(description);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("Cozinha em open space, totalmente equipada.")]
    [InlineData("Cozinha equipada em open space com a sala.")]
    [InlineData("Open space entre cozinha e sala.")]
    public void Extract_KitchenOpenSpacePhrases_ReturnsTrue(string description)
    {
        var result = OpenPlanKitchenExtractor.Extract(description);

        Assert.True(result);
    }

    [Theory]
    [InlineData("Cozinha aberta para a sala, muito espaçosa.")]
    [InlineData("Sala aberta à cozinha, ideal para receber.")]
    public void Extract_KitchenLivingOpenPhrases_ReturnsTrue(string description)
    {
        var result = OpenPlanKitchenExtractor.Extract(description);

        Assert.True(result);
    }

    [Fact]
    public void Extract_CozinhaAmericana_ReturnsTrue()
    {
        var result = OpenPlanKitchenExtractor.Extract("Apartamento com cozinha americana e ilha central.");

        Assert.True(result);
    }

    [Theory]
    [InlineData("Sala e cozinha em conceito aberto.")]
    [InlineData("Conceito open space em toda a área social.")]
    public void Extract_OpenConceptPhrases_ReturnsTrue(string description)
    {
        var result = OpenPlanKitchenExtractor.Extract(description);

        Assert.True(result);
    }

    [Fact]
    public void Extract_VarandaAberta_ReturnsNull()
    {
        var result = OpenPlanKitchenExtractor.Extract("Apartamento com varanda aberta e muita luz.");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_VistaAberta_ReturnsNull()
    {
        var result = OpenPlanKitchenExtractor.Extract("Vista aberta sobre o rio, sem obstruções.");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_NoLayoutMentioned_ReturnsNull()
    {
        var result = OpenPlanKitchenExtractor.Extract("Apartamento T3 com dois quartos e uma casa de banho.");

        Assert.Null(result);
    }
}
