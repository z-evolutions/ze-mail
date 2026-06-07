using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZeMail.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPop3AccountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pop3Host",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Pop3LeaveOnServer",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Pop3Port",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pop3Host",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Pop3LeaveOnServer",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Pop3Port",
                table: "Accounts");
        }
    }
}
