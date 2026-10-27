using OSSDependencyAnalyzer.API.Infrastructure;

namespace OSSDependencyAnalyzer.Tests.Domain;

public class DatabaseUrlTests
{
    [Fact]
    public void ConvertsRenderStyleUrl() =>
        Assert.Equal(
            "Host=dpg-abc.oregon-postgres.render.com;Port=5432;Database=depscan;Username=depscan_user;Password=s3cr3t",
            DatabaseUrl.ToNpgsqlConnectionString("postgres://depscan_user:s3cr3t@dpg-abc.oregon-postgres.render.com/depscan"));

    [Fact]
    public void ConvertsPortSslModeAndEscapedCredentials() =>
        Assert.Equal(
            "Host=db.example.com;Port=6543;Database=app;Username=user@corp;Password=p@ss:word;SSL Mode=Require",
            DatabaseUrl.ToNpgsqlConnectionString("postgresql://user%40corp:p%40ss%3Aword@db.example.com:6543/app?sslmode=require"));

    [Theory]
    [InlineData("Host=localhost;Database=x")]
    [InlineData("")]
    [InlineData(null)]
    public void LeavesNonUrlValuesUntouched(string? value) =>
        Assert.Equal(value, DatabaseUrl.ToNpgsqlConnectionString(value));
}
