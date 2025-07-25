﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class _2025061300_OrganizationReportsql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationApplication",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Applications = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationApplication", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationApplication_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationReport",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReportData = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationReport", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationReport_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_Id",
            table: "OrganizationApplication",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationApplication_OrganizationId",
            table: "OrganizationApplication",
            column: "OrganizationId");


        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_Id",
            table: "OrganizationReport",
            column: "Id");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_OrganizationId",
            table: "OrganizationReport",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationReport_OrganizationId_Date",
            table: "OrganizationReport",
            columns: ["OrganizationId", "Date"],
            descending: [false, true]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationApplication");

        migrationBuilder.DropTable(
            name: "OrganizationReport");
    }
}
