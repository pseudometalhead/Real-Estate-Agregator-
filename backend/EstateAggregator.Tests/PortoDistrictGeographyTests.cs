using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class PortoDistrictGeographyTests
{
    [Theory]
    [InlineData("Pombeiro de Ribavizela", "Felgueiras")]
    [InlineData("Ancede e Ribadouro", "Baião")]
    [InlineData("Cedofeita, Santo Ildefonso, Sé, Miragaia, São Nicolau e Vitória", "Porto")]
    // "-"-joined variant of the same union name, as some scrapers/backfills
    // produced before the real structured fields were captured.
    [InlineData("Amarante (São Gonçalo) - Madalena - Cepelos - Gatão", "Amarante")]
    [InlineData("São Martinho Recezinhos", "Penafiel")]
    public void ResolveConcelho_KnownFreguesia_ReturnsRealConcelho(string freguesia, string expectedConcelho)
    {
        Assert.Equal(expectedConcelho, PortoDistrictGeography.ResolveConcelho(freguesia));
    }

    [Theory]
    [InlineData("Porto")]
    [InlineData("Felgueiras")]
    [InlineData("Vila Nova de Gaia")]
    public void ResolveConcelho_AlreadyARealConcelho_ResolvesToItself(string concelho)
    {
        Assert.Equal(concelho, PortoDistrictGeography.ResolveConcelho(concelho));
    }

    [Theory]
    [InlineData("Madalena")] // parish of both Amarante and Vila Nova de Gaia
    [InlineData("Canelas")] // parish of both Penafiel and Vila Nova de Gaia
    [InlineData("Some Made Up Place")]
    public void ResolveConcelho_AmbiguousOrUnknown_ReturnsNullRatherThanGuessing(string value)
    {
        Assert.Null(PortoDistrictGeography.ResolveConcelho(value));
    }

    [Fact]
    public void TryFixMisplacedConcelho_MisplacedFreguesia_ProducesCorrectionAndMovesNameToFreguesia()
    {
        var fixedIt = PortoDistrictGeography.TryFixMisplacedConcelho("Pombeiro de Ribavizela", out var concelho, out var freguesia);

        Assert.True(fixedIt);
        Assert.Equal("Felgueiras", concelho);
        Assert.Equal("Pombeiro de Ribavizela", freguesia);
    }

    [Fact]
    public void TryFixMisplacedConcelho_AlreadyCorrectConcelho_IsANoOp()
    {
        var fixedIt = PortoDistrictGeography.TryFixMisplacedConcelho("Porto", out _, out _);

        Assert.False(fixedIt);
    }

    [Fact]
    public void TryFixMisplacedConcelho_AmbiguousValue_LeavesItAlone()
    {
        var fixedIt = PortoDistrictGeography.TryFixMisplacedConcelho("Madalena", out _, out _);

        Assert.False(fixedIt);
    }
}
