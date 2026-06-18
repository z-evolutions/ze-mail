using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZeMail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class feat_caldav_calendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CalDavUrl",
                table: "Calendars",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "CalDavSync",
                table: "Calendars",
                newName: "ServerUrl");

            migrationBuilder.AddColumn<string>(
                name: "CalDavSyncToken",
                table: "Calendars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAtUtc",
                table: "Calendars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordEncrypted",
                table: "Calendars",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncIntervalMinutes",
                table: "Calendars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Calendars",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalDavSyncToken",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "PasswordEncrypted",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "SyncIntervalMinutes",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Calendars");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Calendars",
                newName: "CalDavUrl");

            migrationBuilder.RenameColumn(
                name: "ServerUrl",
                table: "Calendars",
                newName: "CalDavSync");
        }
    }
}
