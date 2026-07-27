using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class OrientationExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_NullEmptyOrWhitespaceDescription_ReturnsNotAvailable(string? description)
    {
        var (orientation, source) = OrientationExtractor.Extract(description);

        Assert.Equal("Not Available", orientation);
        Assert.Equal("not_available", source);
    }

    [Theory]
    [InlineData("Apartamento com fachada sul, muito luminoso.")]
    [InlineData("Excelente exposição solar a sul.")]
    [InlineData("T3 virado a sul, todo remodelado.")]
    public void Extract_SulPhrases_ReturnsSulExtracted(string description)
    {
        var (orientation, source) = OrientationExtractor.Extract(description);

        Assert.Equal("Sul", orientation);
        Assert.Equal("extracted", source);
    }

    [Theory]
    [InlineData("Moradia com fachada norte.")]
    [InlineData("Apartamento virado a norte, bastante espaçoso.")]
    public void Extract_NortePhrases_ReturnsNorteExtracted(string description)
    {
        var (orientation, source) = OrientationExtractor.Extract(description);

        Assert.Equal("Norte", orientation);
        Assert.Equal("extracted", source);
    }

    [Theory]
    [InlineData("Apartamento nascente, muito luminoso durante a manhã.")]
    [InlineData("Fração virada a nascente.")]
    [InlineData("Fachada este com bastante luz natural.")]
    public void Extract_OrientePhrases_ReturnsOrienteExtracted(string description)
    {
        var (orientation, source) = OrientationExtractor.Extract(description);

        Assert.Equal("Oriente", orientation);
        Assert.Equal("extracted", source);
    }

    [Theory]
    [InlineData("Apartamento poente, com vista para o rio ao pôr do sol.")]
    [InlineData("Fração virada a poente.")]
    public void Extract_PoentePhrases_ReturnsPoenteExtracted(string description)
    {
        var (orientation, source) = OrientationExtractor.Extract(description);

        Assert.Equal("Poente", orientation);
        Assert.Equal("extracted", source);
    }

    [Fact]
    public void Extract_NorteShoppingPlaceName_DoesNotMatchAsOrientation()
    {
        var (orientation, source) = OrientationExtractor.Extract("Apartamento perto do Norte Shopping.");

        Assert.Equal("Not Available", orientation);
        Assert.Equal("not_available", source);
    }

    [Fact]
    public void Extract_MatosinhosSulNeighborhoodName_DoesNotMatchAsOrientation()
    {
        var (orientation, source) = OrientationExtractor.Extract("Localizado em Matosinhos Sul, perto da praia.");

        Assert.Equal("Not Available", orientation);
        Assert.Equal("not_available", source);
    }

    [Fact]
    public void Extract_BareEsteDemonstrative_DoesNotMatchAsOriente()
    {
        var (orientation, source) = OrientationExtractor.Extract("Este apartamento tem uma excelente localização.");

        Assert.Equal("Not Available", orientation);
        Assert.Equal("not_available", source);
    }

    [Fact]
    public void Extract_NoOrientationMentioned_ReturnsNotAvailable()
    {
        var (orientation, source) = OrientationExtractor.Extract("Apartamento T2 com cozinha equipada e garagem.");

        Assert.Equal("Not Available", orientation);
        Assert.Equal("not_available", source);
    }

    [Fact]
    public void Extract_SulTakesPrecedenceWhenMultipleMatch()
    {
        // Sul is checked first, so a description mentioning both sul and
        // norte phrases should resolve to Sul.
        var (orientation, source) = OrientationExtractor.Extract("Fachada sul e fachada norte, dúplex com dois acessos.");

        Assert.Equal("Sul", orientation);
        Assert.Equal("extracted", source);
    }
}
