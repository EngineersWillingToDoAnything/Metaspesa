using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations {
  /// <inheritdoc />
  public partial class MergePurchasingAndShoppingSchemas : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      migrationBuilder.RenameTable(
          name: "purchases",
          schema: "purchasing",
          newName: "purchases",
          newSchema: "shopping");

      migrationBuilder.RenameTable(
          name: "purchase_items",
          schema: "purchasing",
          newName: "purchase_items",
          newSchema: "shopping");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.EnsureSchema(
          name: "purchasing");

      migrationBuilder.RenameTable(
          name: "purchases",
          schema: "shopping",
          newName: "purchases",
          newSchema: "purchasing");

      migrationBuilder.RenameTable(
          name: "purchase_items",
          schema: "shopping",
          newName: "purchase_items",
          newSchema: "purchasing");
    }
  }
}