using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeuManga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MixedMediaCustomListItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomListItems_ListId_MangaDexId",
                table: "CustomListItems");

            migrationBuilder.RenameColumn(
                name: "MangaDexId",
                table: "CustomListItems",
                newName: "ContentId");

            migrationBuilder.AddColumn<int>(
                name: "ContentKind",
                table: "CustomListItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CustomListItems_ListId_ContentKind_ContentId",
                table: "CustomListItems",
                columns: new[] { "ListId", "ContentKind", "ContentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomListItems_ListId_ContentKind_ContentId",
                table: "CustomListItems");

            migrationBuilder.DropColumn(
                name: "ContentKind",
                table: "CustomListItems");

            migrationBuilder.RenameColumn(
                name: "ContentId",
                table: "CustomListItems",
                newName: "MangaDexId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomListItems_ListId_MangaDexId",
                table: "CustomListItems",
                columns: new[] { "ListId", "MangaDexId" },
                unique: true);
        }
    }
}
