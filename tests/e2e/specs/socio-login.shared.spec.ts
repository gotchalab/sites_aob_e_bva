import { test, expect } from '@playwright/test';
import { meta, loginAsSocio, logoutSocio } from './helpers';

test.describe('Login e logout do sócio', () => {
  test('login com credenciais válidas leva ao dashboard', async ({ page }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);

    // Confirma número de sócio e sidebar
    await expect(page.getByText(new RegExp(`Nº\\s+${m.socioNumero}`))).toBeVisible();
    const nav = page.getByRole('navigation');
    await expect(nav.getByRole('link', { name: 'Dashboard' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Os meus dados' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Quotas', exact: true })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Anilhas' })).toBeVisible();

    // Cookies foram gravados
    const cookies = await page.context().cookies();
    const names = cookies.map((c) => c.name);
    expect(names).toContain(`${m.cookiePrefix}_at`);
    expect(names).toContain(`${m.cookiePrefix}_rt`);
    expect(names).toContain(`${m.cookiePrefix}_uid`);
  });

  test('login com credenciais inválidas mostra erro', async ({ page }, info) => {
    const m = meta(info);
    await page.goto('/socio/login');
    await page.getByLabel('Email').fill(m.socioEmail);
    await page.getByLabel('Password').fill('password-errada');
    await page.getByRole('button', { name: /entrar/i }).click();
    await expect(page.getByText(/credenciais inv/i)).toBeVisible();
    await expect(page).toHaveURL(/\/socio\/login/);
  });

  test('logout limpa cookies e redireciona', async ({ page }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);
    await logoutSocio(page);

    const cookies = await page.context().cookies();
    const names = cookies.map((c) => c.name);
    expect(names).not.toContain(`${m.cookiePrefix}_at`);
    expect(names).not.toContain(`${m.cookiePrefix}_rt`);

    // Após logout, /socio deve voltar a redirecionar para login
    await page.goto('/socio');
    await expect(page).toHaveURL(/\/socio\/login/);
  });
});
