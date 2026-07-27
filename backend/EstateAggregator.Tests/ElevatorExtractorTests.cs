using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class ElevatorExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfElevator_ReturnsNull(string? description)
    {
        Assert.Null(ElevatorExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento no 3º andar com elevador.")]
    [InlineData("Prédio com ascensor, todo remodelado.")]
    public void Extract_ElevatorMentioned_ReturnsTrue(string description)
    {
        Assert.True(ElevatorExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento no 4º andar, sem elevador.")]
    [InlineData("Prédio antigo, não tem elevador.")]
    [InlineData("Edifício sem elevador, acesso apenas por escadas.")]
    public void Extract_NoElevatorMentioned_ReturnsFalse(string description)
    {
        Assert.False(ElevatorExtractor.Extract(description));
    }
}
