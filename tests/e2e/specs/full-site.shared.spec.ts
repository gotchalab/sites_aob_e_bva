import { test, expect, type APIRequestContext } from '@playwright/test';

/**
 * Crawl automático: pede à API a lista de artigos/categorias/downloads
 * e visita cada URL correspondente no frontend para garantir status 200 e ausência
 * de mensagens de erro Next.js ("Runtime Error", "Jest worker", "Failed to").
 */

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';

type Category = { slug: string; name: string; children?: Category[] };
type Article = { slug: string };
type Download = { slug: string };

async function apiJson<T>(request: APIRequestContext, path: string): Promise<T> {
  const res = await request.get(`${API_URL}${path}`);
  if (!res.ok()) throw new Error(`API ${res.status()} ${path}`);
  return (await res.json()) as T;
}

function flattenCategories(nodes: Category[]): Category[] {
  const out: Category[] = [];
  const walk = (list: Category[]) => {
    for (const n of list) {
      out.push(n);
      if (n.children?.length) walk(n.children);
    }
  };
  walk(nodes);
  return out;
}

function siteSlug(baseURL: string): 'aob' | 'bva' {
  return baseURL.includes(':3001') ? 'bva' : 'aob';
}

async function assertPageOk(page: import('@playwright/test').Page, url: string) {
  const res = await page.goto(url, { waitUntil: 'domcontentloaded' });
  const status = res?.status() ?? 0;
  expect(status, `${url} status`).toBe(200);
  const html = await page.content();
  const badMarkers = [
    'Jest worker',
    'Runtime Error',
    'Application error: a server-side exception',
    '"statusCode":500',
    'Cannot read properties of',
    'TypeError:',
  ];
  for (const marker of badMarkers) {
    expect(html.includes(marker), `${url} contains "${marker}"`).toBe(false);
  }
}

test.describe('Crawl completo de rotas públicas', () => {
  test('páginas estáticas (/, /artigos, /contacto)', async ({ page, baseURL }) => {
    for (const path of ['/', '/artigos', '/contacto']) {
      await assertPageOk(page, `${baseURL}${path}`);
    }
  });

  test('todas as categorias renderizam', async ({ page, request, baseURL }) => {
    const slug = siteSlug(baseURL!);
    const cats = await apiJson<Category[]>(request, `/api/sites/${slug}/categories?kind=articles`);
    const flat = flattenCategories(cats);
    expect(flat.length, 'API devolveu categorias').toBeGreaterThan(0);
    console.log(`[${slug}] a testar ${flat.length} categorias`);
    for (const cat of flat) {
      await assertPageOk(page, `${baseURL}/categoria/${cat.slug}`);
    }
  });

  test('primeiros 10 artigos renderizam', async ({ page, request, baseURL }) => {
    const slug = siteSlug(baseURL!);
    const listing = await apiJson<{ items: Article[]; total: number }>(
      request,
      `/api/sites/${slug}/articles?page=1&pageSize=10`,
    );
    expect(listing.items.length, 'API devolveu artigos').toBeGreaterThan(0);
    console.log(`[${slug}] a testar ${listing.items.length} de ${listing.total} artigos`);
    for (const article of listing.items) {
      await assertPageOk(page, `${baseURL}/artigos/${article.slug}`);
    }
  });

  test('página de detalhe do último artigo tem conteúdo', async ({ page, request, baseURL }) => {
    const slug = siteSlug(baseURL!);
    const listing = await apiJson<{ items: Article[] }>(
      request,
      `/api/sites/${slug}/articles?page=1&pageSize=1`,
    );
    expect(listing.items.length).toBeGreaterThan(0);
    const first = listing.items[0];
    await page.goto(`${baseURL}/artigos/${first.slug}`);
    await expect(page.locator('h1').first()).toBeVisible();
    // Corpo do artigo (prose) presente
    await expect(page.locator('.prose-article, article, main').first()).toBeVisible();
  });
});

test.describe('Rotas específicas do BVA', () => {
  test.skip(({ baseURL }) => siteSlug(baseURL!) !== 'bva', 'só BVA tem /downloads');

  test('página /downloads renderiza', async ({ page, baseURL }) => {
    await assertPageOk(page, `${baseURL}/downloads`);
  });

  test('primeiros 5 downloads renderizam', async ({ page, request, baseURL }) => {
    const listing = await apiJson<{ items: Download[]; total: number }>(
      request,
      `/api/sites/bva/downloads?page=1&pageSize=5`,
    );
    expect(listing.items.length).toBeGreaterThan(0);
    console.log(`[bva] a testar ${listing.items.length} de ${listing.total} downloads`);
    for (const d of listing.items) {
      await assertPageOk(page, `${baseURL}/downloads/${d.slug}`);
    }
  });
});

test.describe('Menus dinâmicos', () => {
  test('links do menu principal apontam para rotas válidas', async ({ page, request, baseURL }) => {
    const slug = siteSlug(baseURL!);
    type MenuItem = { title: string; url: string; children?: MenuItem[] };
    const menu = await apiJson<MenuItem[]>(request, `/api/sites/${slug}/menu?type=mainmenu`);
    const flat: MenuItem[] = [];
    const walk = (list: MenuItem[]) => {
      for (const m of list) {
        flat.push(m);
        if (m.children?.length) walk(m.children);
      }
    };
    walk(menu);
    // Só rotas internas (excluir absolutas http://)
    const internal = flat.filter((m) => m.url && !/^https?:\/\//.test(m.url));
    console.log(`[${slug}] menu tem ${internal.length} rotas internas`);
    for (const item of internal.slice(0, 20)) {
      const url = item.url.startsWith('/') ? item.url : `/${item.url}`;
      await assertPageOk(page, `${baseURL}${url}`);
    }
  });
});
