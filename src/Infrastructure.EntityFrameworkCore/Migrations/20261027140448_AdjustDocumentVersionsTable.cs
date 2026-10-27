using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AdjustDocumentVersionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_DocumentId_ContentHash",
                table: "DocumentVersions");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ContentHash",
                table: "DocumentVersions",
                column: "ContentHash",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentVersions_ContentHash",
                table: "DocumentVersions");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_ContentHash",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "ContentHash" },
                unique: true);
        }
    }
}
