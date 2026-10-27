using OSSDependencyAnalyzer.API.Integrations.DependencyParsing;
using OSSDependencyAnalyzer.API.Integrations.DependencyParsing.Parsers;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.Tests.Parsing;

public class CsProjParserTests
{
    private readonly CsProjParser _parser = new();

    [Fact]
    public void Parse_ReadsAttributeChildElementAndOverrideForms()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageReference Include="Serilog">
                  <Version>3.1.1</Version>
                </PackageReference>
                <PackageReference Include="Polly" VersionOverride="8.2.0" />
                <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0" PrivateAssets="all" />
                <PackageReference Include="Centrally.Managed" />
                <PackageReference Update="Updated.Package" Version="1.0.0" />
                <ProjectReference Include="..\Other\Other.csproj" />
              </ItemGroup>
            </Project>
            """;

        var result = _parser.Parse(xml);

        Assert.Equal(6, result.Count);
        Assert.Contains(new ParsedDependency("Newtonsoft.Json", "13.0.1", DependencyType.NuGet), result);
        Assert.Contains(new ParsedDependency("Serilog", "3.1.1", DependencyType.NuGet), result);
        Assert.Contains(new ParsedDependency("Polly", "8.2.0", DependencyType.NuGet), result);
        Assert.Contains(new ParsedDependency("Microsoft.CodeAnalysis.NetAnalyzers", "8.0.0", DependencyType.NuGet, IsDev: true), result);
        Assert.Contains(new ParsedDependency("Centrally.Managed", "unknown", DependencyType.NuGet), result);
        Assert.Contains(result, d => d.PackageName == "Updated.Package");
    }

    [Fact]
    public void Parse_SupportsLegacyMsBuildNamespace()
    {
        const string xml = """
            <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup><PackageReference Include="EntityFramework" Version="6.4.4" /></ItemGroup>
            </Project>
            """;
        Assert.Equal("EntityFramework", Assert.Single(_parser.Parse(xml)).PackageName);
    }

    [Fact]
    public void Parse_InvalidXmlThrowsParseException() =>
        Assert.Throws<DependencyParseException>(() => _parser.Parse("<Project><ItemGroup>"));

    [Theory]
    [InlineData("Api.csproj", true)]
    [InlineData("Lib.fsproj", true)]
    [InlineData("Legacy.vbproj", true)]
    [InlineData("Api.csproj.user", false)]
    public void CanParse(string fileName, bool expected) => Assert.Equal(expected, _parser.CanParse(fileName));
}

public class DirectoryPackagesPropsParserTests
{
    [Fact]
    public void Parse_ReadsCentralPackageVersions()
    {
        const string xml = """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Centrally.Managed" Version="2.1.0" />
                <PackageVersion Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """;
        var result = new DirectoryPackagesPropsParser().Parse(xml);
        Assert.Equal(["Centrally.Managed", "xunit"], result.Select(d => d.PackageName));
        Assert.Equal("2.1.0", result[0].Version);
    }
}

public class PomXmlParserTests
{
    private readonly PomXmlParser _parser = new();

    private const string Pom = """
        <project xmlns="http://maven.apache.org/POM/4.0.0">
          <modelVersion>4.0.0</modelVersion>
          <groupId>com.example</groupId>
          <artifactId>demo</artifactId>
          <version>1.4.0</version>
          <properties>
            <jackson.version>2.9.8</jackson.version>
            <alias.version>${jackson.version}</alias.version>
          </properties>
          <dependencyManagement>
            <dependencies>
              <dependency>
                <groupId>org.slf4j</groupId>
                <artifactId>slf4j-api</artifactId>
                <version>2.0.9</version>
              </dependency>
            </dependencies>
          </dependencyManagement>
          <dependencies>
            <dependency>
              <groupId>com.fasterxml.jackson.core</groupId>
              <artifactId>jackson-databind</artifactId>
              <version>${jackson.version}</version>
            </dependency>
            <dependency>
              <groupId>com.fasterxml.jackson.core</groupId>
              <artifactId>jackson-core</artifactId>
              <version>${alias.version}</version>
            </dependency>
            <dependency>
              <groupId>org.slf4j</groupId>
              <artifactId>slf4j-api</artifactId>
            </dependency>
            <dependency>
              <groupId>com.example</groupId>
              <artifactId>sibling</artifactId>
              <version>${project.version}</version>
            </dependency>
            <dependency>
              <groupId>junit</groupId>
              <artifactId>junit</artifactId>
              <version>4.13.2</version>
              <scope>test</scope>
            </dependency>
            <dependency>
              <groupId>org.unknown</groupId>
              <artifactId>parent-managed</artifactId>
            </dependency>
          </dependencies>
          <build>
            <plugins>
              <plugin>
                <artifactId>maven-compiler-plugin</artifactId>
                <dependencies>
                  <dependency><groupId>plugin.dep</groupId><artifactId>ignored</artifactId><version>1</version></dependency>
                </dependencies>
              </plugin>
            </plugins>
          </build>
        </project>
        """;

    [Fact]
    public void Parse_UsesGroupAndArtifactAsName()
    {
        var result = _parser.Parse(Pom);
        Assert.Contains(result, d => d.PackageName == "com.fasterxml.jackson.core:jackson-databind");
        Assert.All(result, d => Assert.Equal(DependencyType.Maven, d.Type));
    }

    [Fact]
    public void Parse_ResolvesPropertiesManagementAndProjectVersion()
    {
        var result = _parser.Parse(Pom).ToDictionary(d => d.PackageName);

        Assert.Equal("2.9.8", result["com.fasterxml.jackson.core:jackson-databind"].Version);
        Assert.Equal("2.9.8", result["com.fasterxml.jackson.core:jackson-core"].Version); // nested property
        Assert.Equal("2.0.9", result["org.slf4j:slf4j-api"].Version);                     // dependencyManagement
        Assert.Equal("1.4.0", result["com.example:sibling"].Version);                      // ${project.version}
        Assert.Equal("unknown", result["org.unknown:parent-managed"].Version);
    }

    [Fact]
    public void Parse_TestScopeIsDevAndPluginDependenciesAreIgnored()
    {
        var result = _parser.Parse(Pom);
        Assert.True(result.Single(d => d.PackageName == "junit:junit").IsDev);
        Assert.DoesNotContain(result, d => d.PackageName.StartsWith("plugin.dep", StringComparison.Ordinal));
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void Parse_IncludesProfileDependencies()
    {
        const string pom = """
            <project>
              <profiles><profile><dependencies>
                <dependency><groupId>g</groupId><artifactId>a</artifactId><version>1.0</version></dependency>
              </dependencies></profile></profiles>
            </project>
            """;
        Assert.Equal("g:a", Assert.Single(_parser.Parse(pom)).PackageName);
    }

    [Fact]
    public void Parse_InvalidXmlThrowsParseException() =>
        Assert.Throws<DependencyParseException>(() => _parser.Parse("<project>"));
}

public class GradleParserTests
{
    private readonly GradleParser _parser = new();

    [Fact]
    public void Parse_GroovyAndKotlinStringNotation()
    {
        const string script = """
            plugins { id 'java' }
            dependencies {
                implementation 'com.google.guava:guava:31.1-jre'
                api "org.apache.commons:commons-lang3:3.12.0"
                implementation("io.ktor:ktor-server-core:2.3.5")
                testImplementation 'junit:junit:4.13.2'
                runtimeOnly 'org.postgresql:postgresql:42.6.0@jar'
                implementation "org.jetbrains.kotlin:kotlin-stdlib:$kotlinVersion"
                implementation project(':core')
                implementation platform('org.springframework.boot:spring-boot-dependencies:3.1.0')
            }
            """;

        var result = _parser.Parse(script);

        Assert.Contains(new ParsedDependency("com.google.guava:guava", "31.1-jre", DependencyType.Gradle), result);
        Assert.Contains(new ParsedDependency("org.apache.commons:commons-lang3", "3.12.0", DependencyType.Gradle), result);
        Assert.Contains(new ParsedDependency("io.ktor:ktor-server-core", "2.3.5", DependencyType.Gradle), result);
        Assert.Contains(new ParsedDependency("junit:junit", "4.13.2", DependencyType.Gradle, IsDev: true), result);
        Assert.Contains(new ParsedDependency("org.postgresql:postgresql", "42.6.0", DependencyType.Gradle), result);
        Assert.Contains(result, d => d is { PackageName: "org.jetbrains.kotlin:kotlin-stdlib", Version: "$kotlinVersion" });
        Assert.Equal(6, result.Count);
    }

    [Theory]
    [InlineData("build.gradle", true)]
    [InlineData("build.gradle.kts", true)]
    [InlineData("settings.gradle", false)]
    public void CanParse(string fileName, bool expected) => Assert.Equal(expected, _parser.CanParse(fileName));
}
