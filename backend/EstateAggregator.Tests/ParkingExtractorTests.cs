using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class ParkingExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfParking_ReturnsNull(string? description)
    {
        Assert.Null(ParkingExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento T3 com garagem para dois carros.")]
    [InlineData("Inclui lugar de estacionamento privativo.")]
    [InlineData("Moradia com amplo parqueamento.")]
    public void Extract_ParkingMentioned_ReturnsTrue(string description)
    {
        Assert.True(ParkingExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento T2, sem garagem.")]
    [InlineData("Não possui estacionamento privativo.")]
    [InlineData("Sem lugar de garagem incluído.")]
    public void Extract_NoParkingMentioned_ReturnsFalse(string description)
    {
        Assert.False(ParkingExtractor.Extract(description));
    }
}
