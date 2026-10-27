using OSSDependencyAnalyzer.API.Integrations.Osv;

namespace OSSDependencyAnalyzer.Tests.Integrations;

public class GenericVersionComparerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.2", "1.10")]            // numeric, not lexical
    [InlineData("4.17.4", "4.17.12")]
    [InlineData("1.0.0-beta", "1.0.0")]    // pre-release sorts first
    [InlineData("1.0.0-alpha", "1.0.0-beta")]
    [InlineData("0", "0.0.1")]
    [InlineData("v1.9.0", "v1.10.0")]
    [InlineData("2.0rc1", "2.0")]
    public void OrdersVersions(string lower, string higher)
    {
        var comparer = GenericVersionComparer.Instance;
        Assert.True(comparer.Compare(lower, higher) < 0, $"{lower} < {higher}");
        Assert.True(comparer.Compare(higher, lower) > 0, $"{higher} > {lower}");
    }

    [Theory]
    [InlineData("1.0", "1.0.0")]
    [InlineData("1.0.0+build.5", "1.0.0")]
    [InlineData("v2.1.0", "2.1.0")]
    public void TreatsEquivalentVersionsAsEqual(string a, string b) =>
        Assert.Equal(0, GenericVersionComparer.Instance.Compare(a, b));

    [Fact]
    public void HandlesNulls()
    {
        Assert.True(GenericVersionComparer.Instance.Compare(null, "1") < 0);
        Assert.True(GenericVersionComparer.Instance.Compare("1", null) > 0);
        Assert.Equal(0, GenericVersionComparer.Instance.Compare(null, null));
    }
}
