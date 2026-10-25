using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations {
  /// <inheritdoc />
  public partial class PersistCanonicalUnitCodes : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      // Deployment assumes an empty database; historical unit IDs are not translated.
      migrationBuilder.DropForeignKey(
          name: "FK_product_formats_units_of_measure_unit_of_measure_id",
          schema: "market",
          table: "product_formats");

      migrationBuilder.DropTable(
          name: "units_of_measure",
          schema: "market");

      migrationBuilder.DropIndex(
          name: "idx_product_format_identity",
          schema: "market",
          table: "product_formats");

      migrationBuilder.DropIndex(
          name: "idx_product_format_unit_of_measure_id",
          schema: "market",
          table: "product_formats");

      migrationBuilder.DropColumn(
          name: "unit_of_measure_id",
          schema: "market",
          table: "product_formats");

      migrationBuilder.AddColumn<string>(
          name: "unit_of_measure",
          schema: "market",
          table: "product_formats",
          type: "character varying(4)",
          maxLength: 4,
          nullable: false);

      migrationBuilder.CreateIndex(
          name: "idx_product_format_identity",
          schema: "market",
          table: "product_formats",
          columns: ["product_id", "quantity", "unit_of_measure"],
          unique: true);

      migrationBuilder.AddCheckConstraint(
          name: "chk_product_format_unit_of_measure",
          schema: "market",
          table: "product_formats",
          sql: "unit_of_measure IN ('g', 'ml', 'unit', 'wash', 'dose')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.DropIndex(
          name: "idx_product_format_identity",
          schema: "market",
          table: "product_formats");

      migrationBuilder.DropCheckConstraint(
          name: "chk_product_format_unit_of_measure",
          schema: "market",
          table: "product_formats");

      migrationBuilder.DropColumn(
          name: "unit_of_measure",
          schema: "market",
          table: "product_formats");

      migrationBuilder.AddColumn<Guid>(
          name: "unit_of_measure_id",
          schema: "market",
          table: "product_formats",
          type: "uuid",
          nullable: false,
          defaultValue: Guid.Empty);

      migrationBuilder.CreateTable(
          name: "units_of_measure",
          schema: "market",
          columns: table => new {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "Stable unit code, e.g. ml, kg, piece"),
            name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
          },
          constraints: table => {
            table.PrimaryKey("pk_unit_of_measure", x => x.id);
          },
          comment: "Units used to describe product package format, not shopping count.\nExamples: ml, l, g, kg, piece.");

      migrationBuilder.CreateIndex(
          name: "idx_product_format_identity",
          schema: "market",
          table: "product_formats",
          columns: ["product_id", "quantity", "unit_of_measure_id"],
          unique: true);

      migrationBuilder.CreateIndex(
          name: "idx_product_format_unit_of_measure_id",
          schema: "market",
          table: "product_formats",
          column: "unit_of_measure_id");

      migrationBuilder.CreateIndex(
          name: "idx_unit_of_measure_code",
          schema: "market",
          table: "units_of_measure",
          column: "code",
          unique: true);

      migrationBuilder.AddForeignKey(
          name: "FK_product_formats_units_of_measure_unit_of_measure_id",
          schema: "market",
          table: "product_formats",
          column: "unit_of_measure_id",
          principalSchema: "market",
          principalTable: "units_of_measure",
          principalColumn: "id",
          onDelete: ReferentialAction.Restrict);
    }
  }
}