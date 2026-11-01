using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations {
  /// <inheritdoc />
  public partial class EstablishMasterData : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
      migrationBuilder.AlterDatabase()
          .Annotation("Npgsql:CollationDefinition:reference_name", "und-u-ks-level2,und-u-ks-level2,icu,False");

      migrationBuilder.Sql("""
                DO $migration$
                DECLARE conflicts text;
                BEGIN
                  IF EXISTS (SELECT 1 FROM market.super_markets
                             WHERE name = '' OR name ~ '^[[:space:]]|[[:space:]]$')
                     OR EXISTS (SELECT 1 FROM market.product_brands
                                WHERE name = '' OR name ~ '^[[:space:]]|[[:space:]]$') THEN
                    RAISE EXCEPTION 'Market/Brand master data contains blank or untrimmed names.'
                      USING HINT = 'Correct the existing names explicitly before rerunning migrations; preserve IDs and foreign keys.';
                  END IF;

                  SELECT string_agg(ids, '; ') INTO conflicts FROM (
                    SELECT string_agg(id::text || ' (' || name || ')', ', ') AS ids
                    FROM market.super_markets
                    GROUP BY name COLLATE reference_name HAVING count(*) > 1
                  ) duplicates;
                  IF conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'Conflicting Market names under case-insensitive matching: %', conflicts
                      USING HINT = 'Reconcile the listed master records and their product foreign keys explicitly, then retry. No records are merged automatically.';
                  END IF;

                  SELECT string_agg(ids, '; ') INTO conflicts FROM (
                    SELECT string_agg(id::text || ' (' || name || ')', ', ') AS ids
                    FROM market.product_brands
                    GROUP BY name COLLATE reference_name HAVING count(*) > 1
                  ) duplicates;
                  IF conflicts IS NOT NULL THEN
                    RAISE EXCEPTION 'Conflicting Brand names under case-insensitive matching: %', conflicts
                      USING HINT = 'Reconcile the listed master records and their product foreign keys explicitly, then retry. No records are merged automatically.';
                  END IF;
                END $migration$;
                """);

      migrationBuilder.AlterColumn<string>(
          name: "name",
          schema: "market",
          table: "super_markets",
          type: "text",
          nullable: false,
          collation: "reference_name",
          oldClrType: typeof(string),
          oldType: "text");

      migrationBuilder.AlterColumn<string>(
          name: "name",
          schema: "market",
          table: "product_brands",
          type: "text",
          nullable: false,
          collation: "reference_name",
          oldClrType: typeof(string),
          oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.AlterColumn<string>(
          name: "name",
          schema: "market",
          table: "super_markets",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text",
          oldCollation: "reference_name");

      migrationBuilder.AlterColumn<string>(
          name: "name",
          schema: "market",
          table: "product_brands",
          type: "text",
          nullable: false,
          oldClrType: typeof(string),
          oldType: "text",
          oldCollation: "reference_name");

      // Remove the collation only after both columns and their indexes stop using it.
      migrationBuilder.AlterDatabase()
          .OldAnnotation("Npgsql:CollationDefinition:reference_name", "und-u-ks-level2,und-u-ks-level2,icu,False");
    }
  }
}