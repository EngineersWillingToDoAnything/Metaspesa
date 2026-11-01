import { describe, expect, it, vi } from 'vitest';
import { ZodError } from 'zod';

import RestMarketApiService from '@/infrastructure/rest-market-api-service';

describe('RestMarketApiService', () => {
  const marketId = '00000000-0000-7000-8000-000000000001';

  it('reads market items from REST API', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(
        Response.json({ items: [{ id: marketId, name: 'Mercadona' }] }),
      );
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    const markets = await service.getMarkets();

    expect(markets).toEqual([{ id: marketId, name: 'Mercadona' }]);
  });

  it('sends repeatable market IDs and all product filters', async () => {
    const fetcher = vi.fn().mockResolvedValue(
      Response.json({
        items: [],
        page: 2,
        pageSize: 24,
        totalItems: 0,
        totalPages: 0,
      }),
    );
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    await service.getMarketProducts({
      brand: 'Hacendado',
      marketId: [marketId, '00000000-0000-7000-8000-000000000002'],
      page: 2,
      pageSize: 24,
      query: 'milk',
      sort: 'priceAsc',
    });

    expect(fetcher).toHaveBeenCalledWith(
      'https://api.example/api/v1/products',
      expect.objectContaining({
        body: JSON.stringify({
          brand: 'Hacendado',
          marketId: [marketId, '00000000-0000-7000-8000-000000000002'],
          page: 2,
          pageSize: 24,
          query: 'milk',
          sort: 'priceAsc',
        }),
        credentials: 'include',
        headers: {
          Accept: 'application/json',
          'Content-Type': 'application/json',
        },
        method: 'QUERY',
      }),
    );
  });

  it('rejects failed market responses', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 503 }));
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    await expect(service.getMarketProducts({})).rejects.toThrow(
      'Market request failed (503).',
    );
  });

  it('reads calendar observation dates from product responses', async () => {
    const fetcher = vi.fn().mockResolvedValue(
      Response.json({
        items: [
          {
            brand: 'Hacendado',
            formats: [
              {
                currentPrice: { amount: 1.25, currency: 'EUR' },
                id: '00000000-0000-7000-8000-000000000003',
                observedOn: '2026-08-20',
                quantity: { amount: 1000, unit: 'ml' },
              },
            ],
            id: '00000000-0000-7000-8000-000000000002',
            market: { id: marketId, name: 'Mercadona' },
            name: 'Whole milk',
          },
        ],
        page: 1,
        pageSize: 24,
        totalItems: 1,
        totalPages: 1,
      }),
    );
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    const products = await service.getMarketProducts();

    expect(products.items[0].formats[0].observedOn).toBe('2026-08-20');
  });

  it('rejects timestamps in calendar observation date fields', async () => {
    const fetcher = vi.fn().mockResolvedValue(
      Response.json({
        items: [
          {
            brand: 'Hacendado',
            formats: [
              {
                currentPrice: { amount: 1.25, currency: 'EUR' },
                id: '00000000-0000-7000-8000-000000000003',
                observedOn: '2026-08-20T00:00:00Z',
                quantity: { amount: 1000, unit: 'ml' },
              },
            ],
            id: '00000000-0000-7000-8000-000000000002',
            market: { id: marketId, name: 'Mercadona' },
            name: 'Whole milk',
          },
        ],
        page: 1,
        pageSize: 24,
        totalItems: 1,
        totalPages: 1,
      }),
    );
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    await expect(service.getMarketProducts()).rejects.toBeInstanceOf(ZodError);
  });

  it('rejects a successful response that breaks the OpenAPI schema', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(Response.json({ items: [{ name: 'Mercadona' }] }));
    const service = new RestMarketApiService(
      'https://api.example/api/v1',
      fetcher,
    );

    await expect(service.getMarkets()).rejects.toBeInstanceOf(ZodError);
  });
});
