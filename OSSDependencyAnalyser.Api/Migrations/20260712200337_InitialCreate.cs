using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OSSDependencyAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GithubUrl = table.Column<string>(type: "text", nullable: false),
                    RepositoryOwner = table.Column<string>(type: "text", nullable: false),
                    RepositoryName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAnalysedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    TotalDependencies = table.Column<int>(type: "integer", nullable: false),
                    HighVulnerabilities = table.Column<int>(type: "integer", nullable: false),
                    CriticalVulnerabilities = table.Column<int>(type: "integer", nullable: false),
                    MediumVulnerabilities = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageName = table.Column<string>(type: "text", nullable: false),
                    CurrentVersion = table.Column<string>(type: "text", nullable: false),
                    LatestVersion = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dependencies_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VulnerabilitySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriticalCount = table.Column<int>(type: "integer", nullable: false),
                    HighCount = table.Column<int>(type: "integer", nullable: false),
                    MediumCount = table.Column<int>(type: "integer", nullable: false),
                    LowCount = table.Column<int>(type: "integer", nullable: false),
                    TotalVulnerabilities = table.Column<int>(type: "integer", nullable: false),
                    AverageRiskScore = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VulnerabilitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VulnerabilitySnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DependencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CveId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    CvssScore = table.Column<decimal>(type: "numeric", nullable: true),
                    CvssVector = table.Column<string>(type: "text", nullable: true),
                    AffectedVersionRange = table.Column<string>(type: "text", nullable: true),
                    RemediationVersion = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisclosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsExploitable = table.Column<bool>(type: "boolean", nullable: false),
                    ExploitUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_Dependencies_DependencyId",
                        column: x => x.DependencyId,
                        principalTable: "Dependencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_RepositoryId",
                table: "Dependencies",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_RepositoryId_PackageName",
                table: "Dependencies",
                columns: new[] { "RepositoryId", "PackageName" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_GithubUrl",
                table: "Repositories",
                column: "GithubUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_CveId",
                table: "Vulnerabilities",
                column: "CveId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_DependencyId",
                table: "Vulnerabilities",
                column: "DependencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Severity",
                table: "Vulnerabilities",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilitySnapshots_RepositoryId",
                table: "VulnerabilitySnapshots",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilitySnapshots_SnapshotDate",
                table: "VulnerabilitySnapshots",
                column: "SnapshotDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vulnerabilities");

            migrationBuilder.DropTable(
                name: "VulnerabilitySnapshots");

            migrationBuilder.DropTable(
                name: "Dependencies");

            migrationBuilder.DropTable(
                name: "Repositories");
        }
    }
}
