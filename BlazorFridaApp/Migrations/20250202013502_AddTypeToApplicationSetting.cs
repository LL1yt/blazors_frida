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
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "LockedAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Address = table.Column<long>(type: "INTEGER", nullable: false),
                    ValueType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalBytes = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CurrentValue = table.Column<byte[]>(type: "BLOB", nullable: false),
                    IsFrozen = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LockedAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LockedAddresses_ProcessSettings_ProcessSettingsId",
                        column: x => x.ProcessSettingsId,
                        principalTable: "ProcessSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Pattern = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Mask = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Offsets = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProcessSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanProfiles_ProcessSettings_ProcessSettingsId",
                        column: x => x.ProcessSettingsId,
                        principalTable: "ProcessSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Key",
                table: "ApplicationSettings",
                column: "Key",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "LockedAddresses");

            migrationBuilder.DropTable(
                name: "ScanProfiles");

            migrationBuilder.DropTable(
                name: "ProcessSettings");
        }
    }
}
