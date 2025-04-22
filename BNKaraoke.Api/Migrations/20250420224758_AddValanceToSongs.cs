using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BNKaraoke.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddValanceToSongs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Valence",
                table: "Songs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Valence",
                table: "Songs");
        }
    }
}
