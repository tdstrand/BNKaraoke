using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BNKaraoke.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicBrainzIdAndAudioFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropColumn(
                name: "RequestTime",
                table: "QueueItems");

            migrationBuilder.DropColumn(
                name: "SingerId",
                table: "QueueItems");

            migrationBuilder.AlterColumn<string>(
                name: "SpotifyId",
                table: "Songs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestDate",
                table: "Songs",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "Popularity",
                table: "Songs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Energy",
                table: "Songs",
                type: "text",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AlterColumn<string>(
                name: "Danceability",
                table: "Songs",
                type: "text",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AlterColumn<float>(
                name: "Bpm",
                table: "Songs",
                type: "real",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "real");

            migrationBuilder.AddColumn<string>(
                name: "Decade",
                table: "Songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastFmPlaycount",
                table: "Songs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mood",
                table: "Songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicBrainzId",
                table: "Songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EventId",
                table: "QueueItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string[]>(
                name: "Requests",
                table: "QueueItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "Singers",
                table: "QueueItems",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException("Migration rollback is disabled to prevent dropping columns that may contain data. To revert, manually drop the columns 'MusicBrainzId', 'Bpm', 'Danceability', 'Energy', 'Mood', and 'LastFmPlaycount' from the 'Songs' table after backing up the data.");
        }
    }
}
