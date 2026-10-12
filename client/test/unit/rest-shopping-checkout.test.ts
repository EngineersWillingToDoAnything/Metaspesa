import { describe, expect, it, vi } from 'vitest';

import RestShoppingApiService from '@/infrastructure/rest-shopping-api-service';

describe('shopping checkout response', () => {
  const listId = '00000000-0000-7000-8000-000000000001';
  const checkoutId = '00000000-0000-7000-8000-000000000002';

  it('returns checkout ID from an accepted response', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(Response.json({ checkoutId }, { status: 202 }));
    const service = new RestShoppingApiService(
      undefined,
      'https://api.example/api/v1',
      fetcher,
    );

    const result = await service.checkoutShoppingList(listId);

    expect(result).toBe(checkoutId);
  });
});
