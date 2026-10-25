import { expect, it } from 'vitest';

import RestMarketApiService from '@/infrastructure/rest-market-api-service';

import { describeIfRest, restApiUrl } from './rest-test-client';

const service = new RestMarketApiService(restApiUrl);

describeIfRest('market REST integration', () => {
  it('returns seeded markets', async () => {
    const markets = await service.getMarkets();

    expect(markets.map(market => market.name)).toEqual(
      expect.arrayContaining(['Integration Market A', 'Integration Market B']),
    );
  });

  it('filters products by name', async () => {
    const result = await service.getMarketProducts({ query: 'milk' });

    expect(result.items.map(product => product.name)).toContain(
      'Integration Milk 1L',
    );
  });

  it('returns product formats with prices and quantities', async () => {
    const result = await service.getMarketProducts({ query: 'Integration' });
    const milk = result.items.find(
      product => product.name === 'Integration Milk 1L',
    );

    expect(milk?.formats).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          currentPrice: { amount: 1.25, currency: 'EUR' },
          quantity: { amount: 1000, unit: 'ml' },
        }),
      ]),
    );
  });
});
