using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metaspesa.Database.Migrations;

public partial class UseObservationDates : Migration {
  protected override void Up(MigrationBuilder migrationBuilder) {
    migrationBuilder.Sql("""
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at DROP DEFAULT;
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at TYPE date
        USING (observed_at AT TIME ZONE 'UTC')::date;
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at
        SET DEFAULT (now() AT TIME ZONE 'UTC')::date;
      """);
  }

  protected override void Down(MigrationBuilder migrationBuilder) {
    migrationBuilder.Sql("""
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at DROP DEFAULT;
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at TYPE timestamp with time zone
        USING (observed_at::timestamp AT TIME ZONE 'UTC');
      ALTER TABLE market.price_snapshots ALTER COLUMN observed_at SET DEFAULT now();
      """);
  }
}