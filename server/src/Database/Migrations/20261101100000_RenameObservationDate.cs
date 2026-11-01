using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations;

public partial class RenameObservationDate : Migration {
  protected override void Up(MigrationBuilder migrationBuilder) {
    migrationBuilder.RenameColumn(
      name: "observed_at",
      schema: "market",
      table: "price_snapshots",
      newName: "observed_on");

    migrationBuilder.RenameIndex(
      name: "idx_price_snapshot_observed_at",
      schema: "market",
      table: "price_snapshots",
      newName: "idx_price_snapshot_observed_on");

    migrationBuilder.RenameIndex(
      name: "idx_price_snapshot_format_observed_at",
      schema: "market",
      table: "price_snapshots",
      newName: "idx_price_snapshot_format_observed_on");
  }

  protected override void Down(MigrationBuilder migrationBuilder) {
    migrationBuilder.RenameColumn(
      name: "observed_on",
      schema: "market",
      table: "price_snapshots",
      newName: "observed_at");

    migrationBuilder.RenameIndex(
      name: "idx_price_snapshot_observed_on",
      schema: "market",
      table: "price_snapshots",
      newName: "idx_price_snapshot_observed_at");

    migrationBuilder.RenameIndex(
      name: "idx_price_snapshot_format_observed_on",
      schema: "market",
      table: "price_snapshots",
      newName: "idx_price_snapshot_format_observed_at");
  }
}