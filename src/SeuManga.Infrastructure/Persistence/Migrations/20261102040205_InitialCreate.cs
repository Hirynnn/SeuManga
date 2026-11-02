using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeuManga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimeLibrary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JikanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TotalEpisodes = table.Column<int>(type: "INTEGER", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimeLibrary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "History",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MangaDexId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ChapterId = table.Column<string>(type: "TEXT", nullable: false),
                    ChapterNumber = table.Column<string>(type: "TEXT", nullable: true),
                    LastPage = table.Column<int>(type: "INTEGER", nullable: false),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_History", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Library",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MangaDexId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastReadAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastChapterId = table.Column<string>(type: "TEXT", nullable: true),
                    LastChapterNumber = table.Column<string>(type: "TEXT", nullable: true),
                    LastPage = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Library", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchedEpisodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JikanId = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    WatchedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomListItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ListId = table.Column<int>(type: "INTEGER", nullable: false),
                    MangaDexId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CoverUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomListItems_CustomLists_ListId",
                        column: x => x.ListId,
                        principalTable: "CustomLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimeLibrary_JikanId",
                table: "AnimeLibrary",
                column: "JikanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomListItems_ListId_MangaDexId",
                table: "CustomListItems",
                columns: new[] { "ListId", "MangaDexId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomLists_Name",
                table: "CustomLists",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_History_MangaDexId_ChapterId",
                table: "History",
                columns: new[] { "MangaDexId", "ChapterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Library_MangaDexId",
                table: "Library",
                column: "MangaDexId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEpisodes_JikanId_EpisodeNumber",
                table: "WatchedEpisodes",
                columns: new[] { "JikanId", "EpisodeNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimeLibrary");

            migrationBuilder.DropTable(
                name: "CustomListItems");

            migrationBuilder.DropTable(
                name: "History");

            migrationBuilder.DropTable(
                name: "Library");

            migrationBuilder.DropTable(
                name: "WatchedEpisodes");

            migrationBuilder.DropTable(
                name: "CustomLists");
        }
    }
}
