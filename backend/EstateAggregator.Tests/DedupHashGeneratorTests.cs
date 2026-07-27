using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class DedupHashGeneratorTests
{
    [Fact]
    public void Compute_SameInputs_ProducesSameHash()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_PricesInSameBucket_ProduceSameHash()
    {
        // Both 220000 and 221000 round to the same 2500-wide bucket (220000):
        // 220000 / 2500 = 88.0 -> bucket 220000; 221000 / 2500 = 88.4 -> rounds
        // down to 88 -> bucket 220000.
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 221000m, 2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_PricesInDifferentBuckets_ProduceDifferentHashes()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 225000m, 2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_LocationVariations_ProduceSameHash()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("  PORTO,   cedofeita  ", 220000m, 2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentBeds_ProduceDifferentHashes()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 3);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_NullBedsVsZeroBeds_ProduceSameHash()
    {
        // null beds is coalesced to 0 in the hash input, so they must collide.
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, null);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 0);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_NullBedsVsOneBed_ProduceDifferentHashes()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, null);
        var hash2 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 1);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Compute_DifferentLocations_ProduceDifferentHashes()
    {
        var hash1 = DedupHashGenerator.Compute("Porto, Cedofeita", 220000m, 2);
        var hash2 = DedupHashGenerator.Compute("Lisboa, Alvalade", 220000m, 2);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData("porto, cedofeita")]
    [InlineData("Porto, Cedofeita")]
    [InlineData("  PORTO,   cedofeita  ")]
    public void NormalizeLocation_CaseWhitespaceVariations_NormalizeToSameValue(string input)
    {
        var baseline = "porto, cedofeita";
        var actual = DedupHashGenerator.NormalizeLocation(input);

        Assert.Equal(baseline, actual);
    }

    [Fact]
    public void NormalizeLocation_NfcAndNfdComposedAccents_NormalizeToSameValue()
    {
        // "é" as a single precomposed codepoint (NFC) vs "e" + combining acute
        // accent (NFD) — both must normalize identically since the method
        // strips diacritics via NFD-decomposition.
        var nfc = "Évora".Normalize(System.Text.NormalizationForm.FormC);
        var nfd = "Évora".Normalize(System.Text.NormalizationForm.FormD);

        var normalizedNfc = DedupHashGenerator.NormalizeLocation(nfc);
        var normalizedNfd = DedupHashGenerator.NormalizeLocation(nfd);

        Assert.Equal(normalizedNfc, normalizedNfd);
        Assert.Equal("evora", normalizedNfc);
    }

    [Fact]
    public void NormalizeLocation_StripsDiacritics_LowercasesAndCollapsesWhitespace()
    {
        var result = DedupHashGenerator.NormalizeLocation("  Setúbal,   Almada  ");

        Assert.Equal("setubal, almada", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeLocation_NullEmptyOrWhitespace_ReturnsEmptyString(string? input)
    {
        var result = DedupHashGenerator.NormalizeLocation(input);

        Assert.Equal(string.Empty, result);
    }
}
