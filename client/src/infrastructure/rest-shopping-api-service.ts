import { z } from 'zod';

import {
  zCheckoutShoppingListResponse,
  zCreateShoppingListResponse,
  zShoppingItemResponse,
  zShoppingListCollectionResponse,
  zShoppingListResponse,
} from '@/infrastructure/openapi_generated/zod.gen';
import { getRestApiUrl } from '@/lib/rest-api-url';
import {
  ProductMessage,
  ShoppingItemUpdateMessage,
  ShoppingListMessage,
  ShoppingListSummaryMessage,
} from '@/lib/shopping-list-contracts';

type Fetcher = (input: string, init: RequestInit) => Promise<Response>;

const shoppingSummarySchema = zShoppingListCollectionResponse.extend({
  items: z.array(
    zShoppingListCollectionResponse.shape.items.element.extend({
      name: z.string().nullish(),
    }),
  ),
});
const shoppingDetailSchema = zShoppingListResponse.extend({
  items: z.array(
    zShoppingItemResponse.extend({ imageUrl: z.string().nullish() }),
  ),
  name: z.string().nullish(),
});

export class ShoppingApiError extends Error {
  public constructor(
    message: string,
    public readonly status: number,
    public readonly code?: string,
  ) {
    super(message);
  }
}

export default class RestShoppingApiService {
  private readonly baseUrl: string;

  public constructor(
    private readonly token?: string,
    baseUrl = getRestApiUrl(),
    private readonly fetcher: Fetcher = globalThis.fetch.bind(globalThis),
  ) {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  public async getShoppingListSummaries(): Promise<
    ShoppingListSummaryMessage[]
  > {
    const response = await this.request('/shopping-lists', 'GET');
    const body = shoppingSummarySchema.parse(await response.json());
    return body.items.map(item => ({
      id: item.id,
      isTemporary: item.isTemporary,
      name: item.name ?? undefined,
    }));
  }

  public async getShoppingList(listId: string): Promise<ShoppingListMessage> {
    const response = await this.request(`/shopping-lists/${listId}`, 'GET');
    const body = shoppingDetailSchema.parse(await response.json());
    return {
      id: body.id,
      name: body.name ?? undefined,
      products: body.items.map(item => ({
        checked: item.checked,
        name: item.productName,
        price: item.unitPrice.amount * item.amount,
        quantity: `${item.amount} × ${item.quantity.amount} ${item.quantity.unit}`,
        shoppingItemId: item.id,
      })),
    };
  }

  public async createShoppingList(name?: string): Promise<string> {
    const response = await this.request(
      '/shopping-lists',
      'POST',
      name === undefined ? {} : { name },
    );
    return zCreateShoppingListResponse.parse(await response.json()).id;
  }

  public async renameShoppingList(listId: string, name: string): Promise<void> {
    await this.request(`/shopping-lists/${listId}`, 'PATCH', { name });
  }

  public async addItemsToList(
    listId: string,
    items: Pick<ProductMessage, 'productFormatUid' | 'checked'>[],
  ): Promise<void> {
    await this.request(`/shopping-lists/${listId}/items`, 'POST', {
      items: items.map(item => ({
        amount: 1,
        checked: item.checked,
        productFormatId: item.productFormatUid,
      })),
    });
  }

  public async updateItem(
    listId: string,
    shoppingItemId: string,
    update: ShoppingItemUpdateMessage,
  ): Promise<void> {
    await this.request(
      `/shopping-lists/${listId}/items/${shoppingItemId}`,
      'PATCH',
      update,
    );
  }

  public async removeItem(
    listId: string,
    shoppingItemId: string,
  ): Promise<void> {
    await this.request(
      `/shopping-lists/${listId}/items/${shoppingItemId}`,
      'DELETE',
    );
  }

  public async checkoutShoppingList(listId: string): Promise<string> {
    const response = await this.request(
      `/shopping-lists/${listId}/checkouts`,
      'POST',
    );
    return zCheckoutShoppingListResponse.parse(await response.json())
      .checkoutId;
  }

  private async request(
    path: string,
    method: 'GET' | 'POST' | 'PATCH' | 'DELETE',
    body?: unknown,
  ): Promise<Response> {
    const response = await this.fetcher(`${this.baseUrl}${path}`, {
      cache: 'no-store',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
      },
      method,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    if (!response.ok) {
      let title = `Shopping request failed (${response.status}).`;
      let code: string | undefined;
      try {
        const problem = (await response.json()) as {
          title?: unknown;
          code?: unknown;
        };
        const { title: problemTitle, code: problemCode } = problem;
        if (typeof problemTitle === 'string' && problemTitle) {
          title = problemTitle;
        }
        if (typeof problemCode === 'string') {
          code = problemCode;
        }
      } catch {
        // Preserve status-based fallback for malformed problem responses.
      }
      throw new ShoppingApiError(title, response.status, code);
    }
    return response;
  }
}
