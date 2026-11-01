using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations;

public partial class EnforceProductIdentity : Migration {
  protected override void Up(MigrationBuilder migrationBuilder) {
    migrationBuilder.Sql("""
      DO $migration$
      DECLARE conflicts text;
      BEGIN
        IF EXISTS (SELECT 1 FROM market.products
                   WHERE name = '' OR name ~ '^[[:space:]]|[[:space:]]$') THEN
          RAISE EXCEPTION 'Product identities contain blank or untrimmed names.'
            USING HINT = 'Correct the existing product names explicitly before rerunning migrations; preserve IDs and foreign keys.';
        END IF;

        SELECT string_agg(ids, '; ') INTO conflicts FROM (
          SELECT string_agg(id::text || ' (' || name || ')', ', ') AS ids
          FROM market.products
          GROUP BY super_market_id, brand_id, name COLLATE reference_name
          HAVING count(*) > 1
          LIMIT 10
        ) duplicates;
        IF conflicts IS NOT NULL THEN
          RAISE EXCEPTION 'Conflicting Product identities under case-insensitive matching: %', conflicts
            USING HINT = 'Resolve the listed product identities explicitly, preserving Product, Format, PriceSnapshot and Shopping/Purchase references. No records are merged automatically.';
        END IF;
      END $migration$;
      """);

    migrationBuilder.AlterColumn<string>(
      name: "name",
      schema: "market",
      table: "products",
      type: "text",
      nullable: false,
      collation: "reference_name",
      oldClrType: typeof(string),
      oldType: "text");
  }

  protected override void Down(MigrationBuilder migrationBuilder) {
    migrationBuilder.AlterColumn<string>(
      name: "name",
      schema: "market",
      table: "products",
      type: "text",
      nullable: false,
      oldClrType: typeof(string),
      oldType: "text",
      oldCollation: "reference_name");
  }
}