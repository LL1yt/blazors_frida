using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorFridaApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Description",
                table: "ScanProfiles",
                newName: "Mask");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Pattern",
                table: "ScanProfiles",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsed",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccessed",
                table: "LockedAddresses",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<byte[]>(
                name: "OriginalBytes",
                table: "LockedAddresses",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModified",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_ScanProfiles_Name",
                table: "ScanProfiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LockedAddresses_ProcessName_Address",
                table: "LockedAddresses",
                columns: new[] { "ProcessName", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Key",
                table: "ApplicationSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScanProfiles_Name",
                table: "ScanProfiles");

            migrationBuilder.DropIndex(
                name: "IX_LockedAddresses_ProcessName_Address",
                table: "LockedAddresses");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationSettings_Key",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "ScanProfiles");

            migrationBuilder.DropColumn(
                name: "LastUsed",
                table: "ScanProfiles");

            migrationBuilder.DropColumn(
                name: "LastAccessed",
                table: "LockedAddresses");

            migrationBuilder.DropColumn(
                name: "OriginalBytes",
                table: "LockedAddresses");

            migrationBuilder.DropColumn(
                name: "LastModified",
                table: "ApplicationSettings");

            migrationBuilder.RenameColumn(
                name: "Mask",
                table: "ScanProfiles",
                newName: "Description");

            migrationBuilder.AlterColumn<string>(
                name: "Pattern",
                table: "ScanProfiles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");
        }
    }
}
