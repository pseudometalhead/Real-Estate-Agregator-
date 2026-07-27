using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class StorageExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Apartamento T2 com cozinha equipada.")]
    public void Extract_NoMentionOfStorage_ReturnsNull(string? description)
    {
        Assert.Null(StorageExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento com arrecadação no piso -1.")]
    [InlineData("T1 com arrumos incluídos.")]
    [InlineData("Inclui arrumo de 6m2.")]
    public void Extract_StorageMentioned_ReturnsTrue(string description)
    {
        Assert.True(StorageExtractor.Extract(description));
    }

    [Theory]
    [InlineData("Apartamento sem arrecadação.")]
    [InlineData("T2 não tem arrumos.")]
    public void Extract_NoStorageMentioned_ReturnsFalse(string description)
    {
        Assert.False(StorageExtractor.Extract(description));
    }
}
