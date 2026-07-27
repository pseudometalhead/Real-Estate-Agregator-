using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class ConstructionStatusExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Extract_NullEmptyOrWhitespaceDescription_ReturnsNotAvailable(string? description)
    {
        Assert.Equal("Not Available", ConstructionStatusExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento T3 em construção, entrega prevista para 2027.")]
    [InlineData("Moradia em fase de construção, com acabamentos à escolha.")]
    [InlineData("Conclusão prevista para o final do ano.")]
    public void Extract_EmConstrucaoPhrases_ReturnsEmConstrucao(string description)
    {
        Assert.Equal("Em Construção", ConstructionStatusExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento T2 de nova construção, pronto a habitar.")]
    [InlineData("Moradia nova, excelente localização.")]
    [InlineData("Prédio novo com elevador e garagem.")]
    [InlineData("Fração recém-construída, nunca habitada.")]
    public void Extract_NovaConstrucaoPhrases_ReturnsNovaConstrucao(string description)
    {
        Assert.Equal("Nova Construção", ConstructionStatusExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento T1 para recuperar, com bom potencial.")]
    [InlineData("Moradia a necessitar de obras de fundo.")]
    [InlineData("Prédio em ruína para reabilitar.")]
    [InlineData("T2 por remodelar no centro da cidade.")]
    public void Extract_ParaRecuperarPhrases_ReturnsParaRecuperar(string description)
    {
        Assert.Equal("Para Recuperar", ConstructionStatusExtractor.Extract(description));
    }

    [Fact]
    public void Extract_EmConstrucaoTakesPrecedenceOverNovaConstrucao()
    {
        var description = "Empreendimento em construção, será uma nova construção de referência na zona.";
        Assert.Equal("Em Construção", ConstructionStatusExtractor.Extract(description));
    }

    [Fact]
    public void Extract_NovoEmFolha_DoesNotMatchAsNovaConstrucao()
    {
        // Generic marketing slang for "like new condition", not a reliable
        // signal of actual new construction — a renovated 1960s building
        // can also be described this way.
        Assert.Equal("Not Available", ConstructionStatusExtractor.Extract("Apartamento remodelado, novo em folha."));
    }

    [Fact]
    public void Extract_NoConstructionStatusMentioned_ReturnsNotAvailable()
    {
        Assert.Equal("Not Available", ConstructionStatusExtractor.Extract("Apartamento T2 com cozinha equipada e garagem."));
    }
}
