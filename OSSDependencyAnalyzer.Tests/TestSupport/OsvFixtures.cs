using OSSDependencyAnalyzer.API.Integrations.Osv;

namespace OSSDependencyAnalyzer.Tests.TestSupport;

public static class OsvFixtures
{
    public static OsvVulnerability LodashPrototypePollution() => new()
    {
        Id = "GHSA-jf85-cpcp-j695",
        Summary = "Prototype Pollution in lodash",
        Details = "Versions of lodash before 4.17.12 are vulnerable to Prototype Pollution.",
        Aliases = ["CVE-2019-10744", "SNYK-JS-LODASH-450202"],
        Published = new DateTimeOffset(2019, 7, 10, 19, 45, 23, TimeSpan.Zero),
        Severity = [new OsvSeverity { Type = "CVSS_V3", Score = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:H/A:H" }],
        DatabaseSpecific = new OsvDatabaseSpecific { Severity = "CRITICAL" },
        Affected =
        [
            new OsvAffected
            {
                Package = new OsvPackage("lodash", "npm"),
                Ranges = [new OsvRange { Type = "SEMVER", Events = [new OsvEvent { Introduced = "0" }, new OsvEvent { Fixed = "4.17.12" }] }]
            },
            new OsvAffected
            {
                Package = new OsvPackage("lodash-es", "npm"),
                Ranges = [new OsvRange { Type = "SEMVER", Events = [new OsvEvent { Introduced = "0" }, new OsvEvent { Fixed = "4.17.14" }] }]
            }
        ]
    };

    public static OsvVulnerability Unrated(string id, params string[] aliases) => new()
    {
        Id = id,
        Details = "Line one of the details.\nMore detail.",
        Aliases = aliases.ToList(),
        Published = DateTimeOffset.UtcNow
    };
}
