using System.Text.Json.Nodes;
using OSSDependencyAnalyzer.API.Domain;
using OSSDependencyAnalyzer.API.Models;

namespace OSSDependencyAnalyzer.API.Services;

/// <summary>Produces a CycloneDX 1.5 JSON SBOM (with VEX-style vulnerability data) for a scanned repository.</summary>
public static class SbomBuilder
{
    public static JsonObject Build(Repository repository, IReadOnlyList<Dependency> dependencies, DateTimeOffset timestamp)
    {
        var components = new JsonArray();
        var vulnerabilities = new JsonArray();

        foreach (var dependency in dependencies)
        {
            var version = dependency.ResolvedVersion ?? dependency.CurrentVersion;
            var purl = PackageUrl.Create(dependency.Type, dependency.PackageName, dependency.ResolvedVersion);

            var component = new JsonObject
            {
                ["type"] = "library",
                ["bom-ref"] = purl,
                ["name"] = dependency.PackageName,
                ["version"] = version,
                ["purl"] = purl,
                ["scope"] = dependency.IsDevDependency ? "optional" : "required"
            };
            if (dependency.ManifestPath is not null)
            {
                component["properties"] = new JsonArray(new JsonObject
                {
                    ["name"] = "depscan:manifest",
                    ["value"] = dependency.ManifestPath
                });
            }
            components.Add(component);

            foreach (var vulnerability in dependency.Vulnerabilities)
            {
                var rating = new JsonObject
                {
                    ["severity"] = vulnerability.Severity.ToString().ToLowerInvariant()
                };
                if (vulnerability.CvssScore is { } score)
                {
                    rating["score"] = score;
                    rating["method"] = vulnerability.CvssVector?.StartsWith("CVSS:3.0", StringComparison.Ordinal) == true ? "CVSSv3" : "CVSSv31";
                    rating["vector"] = vulnerability.CvssVector;
                }

                var entry = new JsonObject
                {
                    ["bom-ref"] = $"{vulnerability.CveId}:{purl}",
                    ["id"] = vulnerability.CveId,
                    ["source"] = new JsonObject
                    {
                        ["name"] = "OSV",
                        ["url"] = vulnerability.ReferenceUrl
                    },
                    ["ratings"] = new JsonArray(rating),
                    ["description"] = vulnerability.Title,
                    ["detail"] = vulnerability.Description,
                    ["published"] = vulnerability.PublishedAt.ToString("O"),
                    ["affects"] = new JsonArray(new JsonObject { ["ref"] = purl })
                };
                if (vulnerability.RemediationVersion is not null)
                    entry["recommendation"] = $"Upgrade {dependency.PackageName} to {vulnerability.RemediationVersion} or later.";
                if (vulnerability.IsExploitable)
                    entry["properties"] = new JsonArray(new JsonObject { ["name"] = "cisa:kev", ["value"] = "true" });

                vulnerabilities.Add(entry);
            }
        }

        return new JsonObject
        {
            ["bomFormat"] = "CycloneDX",
            ["specVersion"] = "1.5",
            ["serialNumber"] = $"urn:uuid:{Guid.NewGuid()}",
            ["version"] = 1,
            ["metadata"] = new JsonObject
            {
                ["timestamp"] = timestamp.ToString("O"),
                ["tools"] = new JsonObject
                {
                    ["components"] = new JsonArray(new JsonObject { ["type"] = "application", ["name"] = "DepScan" })
                },
                ["component"] = new JsonObject
                {
                    ["type"] = "application",
                    ["bom-ref"] = repository.GithubUrl,
                    ["name"] = $"{repository.RepositoryOwner}/{repository.RepositoryName}",
                    ["externalReferences"] = new JsonArray(new JsonObject { ["type"] = "vcs", ["url"] = repository.GithubUrl })
                }
            },
            ["components"] = components,
            ["vulnerabilities"] = vulnerabilities
        };
    }
}
