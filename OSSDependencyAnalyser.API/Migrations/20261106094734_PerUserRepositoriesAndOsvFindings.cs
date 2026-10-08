using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OSSDependencyAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class PerUserRepositoriesAndOsvFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VulnerabilitySnapshots_RepositoryId",
                table: "VulnerabilitySnapshots");

            migrationBuilder.DropIndex(
                name: "IX_VulnerabilitySnapshots_SnapshotDate",
                table: "VulnerabilitySnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_GithubUrl",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Dependencies_RepositoryId",
                table: "Dependencies");

            migrationBuilder.AddColumn<double>(
                name: "RiskScore",
                table: "VulnerabilitySnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "AdvisoryId",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Aliases",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUrl",
                table: "Vulnerabilities",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryOwner",
                table: "Repositories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryName",
                table: "Repositories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "GithubUrl",
                table: "Repositories",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Repositories",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Repositories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LowVulnerabilities",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ManifestCount",
                table: "Repositories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "RiskScore",
                table: "Repositories",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "Stars",
                table: "Repositories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Repositories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDevDependency",
                table: "Dependencies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManifestPath",
                table: "Dependencies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedVersion",
                table: "Dependencies",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilitySnapshots_RepositoryId_SnapshotDate",
                table: "VulnerabilitySnapshots",
                columns: new[] { "RepositoryId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Status",
                table: "Repositories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_UserId_GithubUrl",
                table: "Repositories",
                columns: new[] { "UserId", "GithubUrl" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_UserAccounts_UserId",
                table: "Repositories",
                column: "UserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_UserAccounts_UserId",
                table: "Repositories");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropIndex(
                name: "IX_VulnerabilitySnapshots_RepositoryId_SnapshotDate",
                table: "VulnerabilitySnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Status",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_UserId_GithubUrl",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "VulnerabilitySnapshots");

            migrationBuilder.DropColumn(
                name: "AdvisoryId",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "Aliases",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "ReferenceUrl",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "LowVulnerabilities",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "ManifestCount",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "Stars",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "IsDevDependency",
                table: "Dependencies");

            migrationBuilder.DropColumn(
                name: "ManifestPath",
                table: "Dependencies");

            migrationBuilder.DropColumn(
                name: "ResolvedVersion",
                table: "Dependencies");

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryOwner",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryName",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "GithubUrl",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilitySnapshots_RepositoryId",
                table: "VulnerabilitySnapshots",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilitySnapshots_SnapshotDate",
                table: "VulnerabilitySnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_GithubUrl",
                table: "Repositories",
                column: "GithubUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dependencies_RepositoryId",
                table: "Dependencies",
                column: "RepositoryId");
        }
    }
}
