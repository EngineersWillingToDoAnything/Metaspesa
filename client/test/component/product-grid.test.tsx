/* @vitest-environment jsdom */
import { ToastProvider } from '@/app/(protected)/components/toast-provider';
import { MarketSection } from '@/app/(protected)/markets/components/market-section';
import ProductGrid from '@/app/(protected)/markets/components/product-grid';
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const navigationMocks = vi.hoisted(() => ({
  pathname: '/markets',
  push: vi.fn(),
  searchParams: new URLSearchParams(),
}));

const marketServiceMocks = vi.hoisted(() => ({ getMarketProducts: vi.fn() }));

vi.mock('@/infrastructure/rest-market-api-service', () => ({
  default: class {
    getMarketProducts = marketServiceMocks.getMarketProducts;
  },
}));

vi.mock('next/navigation', () => ({
  usePathname: () => navigationMocks.pathname,
  useRouter: () => ({
    push: navigationMocks.push,
  }),
  useSearchParams: () => navigationMocks.searchParams,
}));

async function renderAuthenticatedProductGrid() {
  render(
    <ToastProvider>
      <ProductGrid
        isAuthenticated
        shoppingListSummaries={[{}, { name: 'Weekly' }]}
      />
    </ToastProvider>,
  );

  fireEvent.click(await screen.findByRole('button', { name: /add/i }));
}

describe('product grid component', () => {
  beforeEach(() => {
    navigationMocks.pathname = '/markets';
    navigationMocks.push.mockReset();
    navigationMocks.searchParams = new URLSearchParams();
    marketServiceMocks.getMarketProducts.mockReset().mockResolvedValue({
      items: [
        {
          brand: 'Hacendado',
          formats: [
            {
              currentPrice: { amount: 1.29, currency: 'EUR' },
              id: '10',
              observedOn: '2026-08-20',
              quantity: { amount: 1, unit: 'l' },
            },
          ],
          id: '41',
          market: { id: '1', name: 'Mercadona' },
          name: 'Whole Milk',
        },
      ],
      page: 1,
      pageSize: 24,
      totalItems: 1,
      totalPages: 1,
    });
  });
  afterEach(cleanup);

  it('opens add-to-list dialog when authenticated user adds product', async () => {
    await renderAuthenticatedProductGrid();

    expect(screen.getByRole('dialog')).toBeVisible();
  });

  it('shows selected product name in add-to-list dialog', async () => {
    await renderAuthenticatedProductGrid();

    expect(
      within(screen.getByRole('dialog')).getByText('Whole Milk'),
    ).toBeVisible();
  });

  it('renders each product format with its own quantity', () => {
    render(
      <MarketSection
        marketName="Mercadona"
        onAddProduct={() => undefined}
        products={[
          {
            brand: 'Hacendado',
            formats: [
              {
                currentPrice: { amount: 1.29, currency: 'EUR' },
                id: '10',
                observedOn: '2026-08-20',
                quantity: { amount: 1, unit: 'l' },
              },
              {
                currentPrice: { amount: 2.29, currency: 'EUR' },
                id: '11',
                observedOn: '2026-08-20',
                quantity: { amount: 2, unit: 'l' },
              },
            ],
            id: '41',
            market: { id: '1', name: 'Mercadona' },
            name: 'Whole Milk',
          },
        ]}
        showDivider
      />,
    );

    expect(screen.getAllByRole('button', { name: /add/i })).toHaveLength(2);
  });

  it('passes only UUID market filters to product query', async () => {
    const firstMarketId = '00000000-0000-7000-8000-000000000001';
    const secondMarketId = '00000000-0000-7000-8000-000000000002';
    navigationMocks.searchParams = new URLSearchParams([
      ['marketId', firstMarketId],
      ['marketId', 'not-a-uuid'],
      ['marketId', secondMarketId],
    ]);

    render(
      <ToastProvider>
        <ProductGrid isAuthenticated={false} shoppingListSummaries={[]} />
      </ToastProvider>,
    );

    await waitFor(() =>
      expect(marketServiceMocks.getMarketProducts).toHaveBeenCalledWith(
        expect.objectContaining({
          marketId: [firstMarketId, secondMarketId],
        }),
      ),
    );
  });
});
