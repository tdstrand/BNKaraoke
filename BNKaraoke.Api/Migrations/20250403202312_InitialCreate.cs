using System;
using Microsoft.EntityFrameworkCore.Migrations;
using BNKaraoke.Api.Data;

#nullable disable

namespace BNKaraoke.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "YouTubeUrl",
                table: "Songs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Genre",
                table: "Songs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "Songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<float>(
                name: "Bpm",
                table: "Songs",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Danceability",
                table: "Songs",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "Energy",
                table: "Songs",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "Popularity",
                table: "Songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestDate",
                table: "Songs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                table: "Songs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpotifyId",
                table: "Songs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "Valence",
                table: "Songs",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Bpm",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Danceability",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Energy",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Popularity",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "RequestDate",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "RequestedBy",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "SpotifyId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Valence",
                table: "Songs");

            migrationBuilder.AlterColumn<string>(
                name: "YouTubeUrl",
                table: "Songs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Genre",
                table: "Songs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
