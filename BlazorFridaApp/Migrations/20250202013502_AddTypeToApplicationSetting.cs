using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFridaApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeToApplicationSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ProcessName",
                table: "LockedAddresses",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ValueType",
                table: "LockedAddresses",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "ProcessSettingsId",
                table: "LockedAddresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ProcessName",
                table: "ScanProfiles",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ScanProfiles",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Mask",
                table: "ScanProfiles",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Pattern",
                table: "ScanProfiles",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "ProcessSettingsId",
                table: "ScanProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProcessSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LockedAddresses_ProcessSettingsId_Address",
                table: "LockedAddresses",
                columns: new[] { "ProcessSettingsId", "Address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessSettings_ProcessName",
                table: "ProcessSettings",
                column: "ProcessName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScanProfiles_ProcessSettingsId_Name",
                table: "ScanProfiles",
                columns: new[] { "ProcessSettingsId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LockedAddresses_ProcessSettings_ProcessSettingsId",
                table: "LockedAddresses",
                column: "ProcessSettingsId",
                principalTable: "ProcessSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScanProfiles_ProcessSettings_ProcessSettingsId",
                table: "ScanProfiles",
                column: "ProcessSettingsId",
                principalTable: "ProcessSettings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LockedAddresses_ProcessSettings_ProcessSettingsId",
                table: "LockedAddresses");

            migrationBuilder.DropForeignKey(
                name: "FK_ScanProfiles_ProcessSettings_ProcessSettingsId",
                table: "ScanProfiles");

            migrationBuilder.DropTable(
                name: "ProcessSettings");

            migrationBuilder.DropIndex(
                name: "IX_LockedAddresses_ProcessSettingsId_Address",
                table: "LockedAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ScanProfiles_ProcessSettingsId_Name",
                table: "ScanProfiles");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "ProcessSettingsId",
                table: "LockedAddresses");

            migrationBuilder.DropColumn(
                name: "ProcessSettingsId",
                table: "ScanProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ProcessName",
                table: "LockedAddresses",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "ValueType",
                table: "LockedAddresses",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ProcessName",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Pattern",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.AlterColumn<string>(
                name: "Mask",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 255);
        }
    }
}
