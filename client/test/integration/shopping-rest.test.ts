import { expect, it } from 'vitest';

import RestMarketApiService from '@/infrastructure/rest-market-api-service';
import RestShoppingApiService from '@/infrastructure/rest-shopping-api-service';
import type { ProductMessage } from '@/lib/shopping-list-contracts';

import {
  describeIfRest,
  registerAndLogin,
  restApiUrl,
} from './rest-test-client';

async function createShoppingClient(): Promise<RestShoppingApiService> {
  const { token } = await registerAndLogin();
  return new RestShoppingApiService(token, restApiUrl, (input, init) => {
    const headers = new Headers(init.headers);
    headers.set('Origin', 'http://localhost:3000');
    return fetch(input, { ...init, headers });
  });
}

async function createShoppingList(name?: string) {
  const client = await createShoppingClient();
  const listId = await client.createShoppingList(name);
  return { client, listId };
}

async function getMarketProducts(count: number): Promise<ProductMessage[]> {
  const response = await new RestMarketApiService(restApiUrl).getMarketProducts(
    {
      page: 1,
      pageSize: 20,
      query: 'Integration',
    },
  );
  const fixtures = [
    {
      market: 'Integration Market A',
      name: 'Integration Milk 1L',
      price: 1.25,
      quantity: { amount: 1000, unit: 'ml' },
    },
    {
      market: 'Integration Market A',
      name: 'Integration Bread',
      price: 2.1,
      quantity: { amount: 1, unit: 'unit' },
    },
  ];
  return fixtures.slice(0, count).map(fixture => {
    const product = response.items.find(
      value =>
        value.market.name === fixture.market && value.name === fixture.name,
    );
    const format = product?.formats.find(
      value =>
        value.quantity.amount === fixture.quantity.amount &&
        value.quantity.unit === fixture.quantity.unit,
    );
    if (
      !product ||
      !format ||
      product.brand !== 'Integration Brand' ||
      format.currentPrice.amount !== fixture.price ||
      format.id.length === 0
    ) {
      throw new Error(
        `Missing or changed integration fixture: ${fixture.name}.`,
      );
    }
    return {
      checked: false,
      name: fixture.name,
      price: fixture.price,
      productFormatUid: format.id,
      quantity: `1 × ${fixture.quantity.amount} ${fixture.quantity.unit}`,
    };
  });
}

describeIfRest('shopping REST integration', () => {
  it('rejects shopping list reads without authentication', async () => {
    const client = new RestShoppingApiService(undefined, restApiUrl);

    await expect(client.getShoppingListSummaries()).rejects.toMatchObject({
      status: 401,
    });
  });

  it('creates an unnamed temporary shopping list', async () => {
    const { client, listId } = await createShoppingList();

    await expect(client.getShoppingList(listId)).resolves.toMatchObject({
      id: listId,
      name: undefined,
      products: [],
    });
  });

  it('returns a created temporary shopping list in summaries', async () => {
    const { client, listId } = await createShoppingList();

    await expect(client.getShoppingListSummaries()).resolves.toContainEqual({
      id: listId,
      isTemporary: true,
      name: undefined,
    });
  });

  it('rejects a second temporary shopping list for the same shopper', async () => {
    const { client } = await createShoppingList();

    await expect(client.createShoppingList()).rejects.toMatchObject({
      status: 409,
    });
  });

  it('adds a shopping item', async () => {
    const [product] = await getMarketProducts(1);
    const { client, listId } = await createShoppingList();

    await client.addItemsToList(listId, [product]);

    await expect(client.getShoppingList(listId)).resolves.toMatchObject({
      products: [
        {
          checked: product.checked,
          name: product.name,
          shoppingItemId: expect.any(String),
        },
      ],
    });
  });

  it('updates only the requested shopping item fields', async () => {
    const [product] = await getMarketProducts(1);
    const { client, listId } = await createShoppingList('Integration Update');
    await client.addItemsToList(listId, [product]);
    const [item] = (await client.getShoppingList(listId)).products;

    await client.updateItem(listId, item.shoppingItemId, {
      checked: true,
    });

    await expect(client.getShoppingList(listId)).resolves.toMatchObject({
      products: [{ ...item, checked: true }],
    });
  });

  it('removes one shopping item and preserves the other', async () => {
    const [removedProduct, retainedProduct] = await getMarketProducts(2);
    const { client, listId } = await createShoppingList('Integration Remove');
    await client.addItemsToList(listId, [removedProduct, retainedProduct]);
    const items = (await client.getShoppingList(listId)).products;
    const removedItem = items.find(item => item.name === removedProduct.name);
    if (!removedItem) {
      throw new Error('Added shopping item was not returned.');
    }

    await client.removeItem(listId, removedItem.shoppingItemId);

    await expect(client.getShoppingList(listId)).resolves.toMatchObject({
      products: [{ name: retainedProduct.name }],
    });
  });

  it('checks out checked items and clears their checked state', async () => {
    const [product] = await getMarketProducts(1);
    const { client, listId } = await createShoppingList('Integration Checkout');
    await client.addItemsToList(listId, [product]);
    const [item] = (await client.getShoppingList(listId)).products;
    await client.updateItem(listId, item.shoppingItemId, {
      checked: true,
    });

    await client.checkoutShoppingList(listId);

    await expect(client.getShoppingList(listId)).resolves.toMatchObject({
      products: [{ ...item, checked: false }],
    });
  });
});
