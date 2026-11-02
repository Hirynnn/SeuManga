using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeuManga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnimeLibraryStatusAndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "AnimeLibrary",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AnimeLibrary",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "AnimeLibrary");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AnimeLibrary");
        }
    }
}
