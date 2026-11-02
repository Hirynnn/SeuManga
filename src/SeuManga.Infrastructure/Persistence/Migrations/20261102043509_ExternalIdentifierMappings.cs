using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeuManga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalIdentifierMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalIdentifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContentKind = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MatchedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIdentifiers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentifiers_ContentKind_LocalId_Provider",
                table: "ExternalIdentifiers",
                columns: new[] { "ContentKind", "LocalId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentifiers_Provider_ExternalId",
                table: "ExternalIdentifiers",
                columns: new[] { "Provider", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalIdentifiers");
        }
    }
}
