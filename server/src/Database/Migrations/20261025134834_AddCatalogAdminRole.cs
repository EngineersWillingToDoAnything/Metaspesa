using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations;

public partial class AddCatalogAdminRole : Migration {
  protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.InsertData(
    schema: "identity", table: "roles", columns: ["id", "description", "name"],
    values: [new Guid("00000000-0000-7000-8000-000000000003"),
      "Operator who creates Market and Brand master data", "CatalogAdmin"]);

  protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DeleteData(
    schema: "identity", table: "roles", keyColumn: "id",
    keyValue: new Guid("00000000-0000-7000-8000-000000000003"));
}