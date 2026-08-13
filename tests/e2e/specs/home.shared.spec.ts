import { test, expect } from '@playwright/test';

test.describe('Páginas públicas', () => {
  test('home renderiza e mostra menu', async ({ page }) => {
    const res = await page.goto('/');
    expect(res?.status(), 'home 200').toBe(200);
    // Header deve existir com pelo menos um link visível
    const anyLink = page.locator('header a').first();
    await expect(anyLink).toBeVisible();
    // Home costuma ter H1 ou destaque
    const h1 = page.locator('h1').first();
    await expect(h1).toBeVisible();
  });

  test('listagem de artigos carrega', async ({ page }) => {
    const res = await page.goto('/artigos');
    expect(res?.status()).toBe(200);
    // Pelo menos um card/link para artigo
    const article = page.locator('a[href*="/artigos/"]').first();
    await expect(article).toBeVisible();
  });

  test('sem sessão: /socio redireciona para /socio/login', async ({ page }) => {
    const res = await page.goto('/socio', { waitUntil: 'domcontentloaded' });
    await expect(page).toHaveURL(/\/socio\/login/);
    expect(res?.status()).toBe(200);
    await expect(page.getByRole('heading', { name: /área de sócio/i })).toBeVisible();
  });

  test('/socio/login mostra form', async ({ page }) => {
    await page.goto('/socio/login');
    await expect(page.getByLabel('Email')).toBeVisible();
    await expect(page.getByLabel('Password')).toBeVisible();
    await expect(page.getByRole('button', { name: /entrar/i })).toBeVisible();
  });
});
