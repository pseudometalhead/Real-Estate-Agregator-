using EstateAggregator.Utilities;

namespace EstateAggregator.Tests;

public class DistrictAliasesTests
{
    [Theory]
    [InlineData("Lisbon", "Lisboa")]
    [InlineData("Oporto", "Porto")]
    [InlineData("Evora", "Évora")]
    [InlineData("Setubal", "Setúbal")]
    [InlineData("Santarem", "Santarém")]
    [InlineData("Braganca", "Bragança")]
    public void ToPortuguese_KnownEnglishAlias_ReturnsPortugueseForm(string alias, string expected)
    {
        var result = DistrictAliases.ToPortuguese(alias);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("lisbon", "Lisboa")]
    [InlineData("OPORTO", "Porto")]
    public void ToPortuguese_KnownAliasCaseInsensitive_ReturnsPortugueseForm(string alias, string expected)
    {
        var result = DistrictAliases.ToPortuguese(alias);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Lisboa")]
    [InlineData("Porto")]
    [InlineData("Évora")]
    public void ToPortuguese_AlreadyPortuguese_PassesThroughUnchanged(string district)
    {
        var result = DistrictAliases.ToPortuguese(district);

        Assert.Equal(district, result);
    }

    [Fact]
    public void ToPortuguese_UnknownDistrict_PassesThroughUnchanged()
    {
        var result = DistrictAliases.ToPortuguese("Faro");

        Assert.Equal("Faro", result);
    }

    [Fact]
    public void ToPortuguese_UnrecognizedInput_ReturnsInputUnchangedIncludingWhitespace()
    {
        // ToPortuguese only trims for the dictionary lookup key; on a miss it
        // returns the original (untrimmed) input string as-is.
        var result = DistrictAliases.ToPortuguese("  Faro  ");

        Assert.Equal("  Faro  ", result);
    }
}
