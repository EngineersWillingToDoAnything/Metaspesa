using Metaspesa.Database;
using Metaspesa.Database.Entities;
using Metaspesa.Domain.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Metaspesa.MigrationService;

internal sealed class IntegrationSeeder(MainContext context) {
  private static readonly DateTime ObservedAt =
    new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

  private static readonly Fixture[] Fixtures = [
    new("Integration Market A", "Integration Milk 1L", 1000m, "ml", 1.25m),
    new("Integration Market A", "Integration Bread", 1m, "unit", 2.10m),
    new("Integration Market B", "Integration Pasta", 1000m, "g", 3.40m),
  ];

  internal async Task SeedAsync(CancellationToken cancellationToken) {
    IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () => {
      await using IDbContextTransaction transaction =
        await context.Database.BeginTransactionAsync(cancellationToken);
      // Keep concurrent seed invocations from racing on the same natural keys.
      await context.Database.ExecuteSqlRawAsync(
        "SELECT pg_advisory_xact_lock(74203692)", cancellationToken);

      foreach (Fixture fixture in Fixtures) {
        await SeedFixtureAsync(fixture, cancellationToken);
      }

      await transaction.CommitAsync(cancellationToken);
    });
  }

  private async Task SeedFixtureAsync(Fixture fixture, CancellationToken cancellationToken) {
    SuperMarketDbEntity? market = await context.SuperMarkets.SingleOrDefaultAsync(
      value => value.Name == fixture.Market, cancellationToken);
    if (market is null) {
      market = new SuperMarketDbEntity {
        Id = Uid.Create(),
        Name = fixture.Market,
      };
      context.SuperMarkets.Add(market);
    }

    ProductBrandDbEntity? brand = await context.ProductBrands.SingleOrDefaultAsync(
      value => value.Name == "Integration Brand", cancellationToken);
    if (brand is null) {
      brand = new ProductBrandDbEntity {
        Id = Uid.Create(),
        Name = "Integration Brand",
      };
      context.ProductBrands.Add(brand);
    }

    await context.SaveChangesAsync(cancellationToken);

    ProductDbEntity? product = await context.Products.SingleOrDefaultAsync(
      value => value.Name == fixture.Product && value.SuperMarketId == market.Id &&
        value.BrandId == brand.Id, cancellationToken);
    if (product is null) {
      product = new ProductDbEntity {
        Id = Uid.Create(),
        Name = fixture.Product, SuperMarketId = market.Id, BrandId = brand.Id,
      };
      context.Products.Add(product);
      await context.SaveChangesAsync(cancellationToken);
    }

    ProductFormatDbEntity? format = await context.ProductFormats.SingleOrDefaultAsync(
      value => value.ProductId == product.Id && value.Quantity == fixture.Quantity &&
        value.UnitOfMeasure == fixture.UnitOfMeasure, cancellationToken);
    if (format is null) {
      format = new ProductFormatDbEntity {
        Id = Uid.Create(),
        ProductId = product.Id, Quantity = fixture.Quantity,
        UnitOfMeasure = fixture.UnitOfMeasure, ImageUrl = "",
      };
      context.ProductFormats.Add(format);
      await context.SaveChangesAsync(cancellationToken);
    }

    List<PriceSnapshotDbEntity> snapshots = await context.PriceSnapshots
      .Where(value => value.ProductFormatId == format.Id && value.ObservedAt == ObservedAt)
      .ToListAsync(cancellationToken);
    if (snapshots.Any(value => value.PriceAmount != fixture.Price ||
      value.CurrencyCode != "EUR")) {
      throw new InvalidOperationException(
        $"Integration fixture price conflicts with existing snapshot for '{fixture.Product}'.");
    }

    if (snapshots.Count == 0) {
      context.PriceSnapshots.Add(new PriceSnapshotDbEntity {
        Id = Uid.Create(),
        ProductFormatId = format.Id, PriceAmount = fixture.Price,
        CurrencyCode = "EUR", ObservedAt = ObservedAt,
      });
      await context.SaveChangesAsync(cancellationToken);
    }
  }

  private sealed record Fixture(
    string Market, string Product, decimal Quantity,
    string UnitOfMeasure, decimal Price);
}