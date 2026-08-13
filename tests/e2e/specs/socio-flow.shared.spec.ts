import { test, expect } from '@playwright/test';
import { meta, loginAsSocio } from './helpers';

test.describe('Fluxo sócio: dados, quotas, pedido', () => {
  test('editar dados persiste após navegação', async ({ page }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);

    await page.getByRole('navigation').getByRole('link', { name: 'Os meus dados' }).click();
    await expect(page).toHaveURL(/\/socio\/dados/);

    const novoTelefone = '900' + Math.floor(100000 + Math.random() * 899999);
    const novaMorada = `Rua Playwright ${Date.now()}`;

    await page.locator('input[name="telefone"]').fill(novoTelefone);
    await page.locator('input[name="morada"]').fill(novaMorada);
    await page.getByRole('button', { name: /guardar/i }).click();
    await expect(page.getByText(/dados atualizados/i)).toBeVisible();

    // Reload da mesma página confirma persistência
    await page.reload();
    await expect(page.locator('input[name="telefone"]')).toHaveValue(novoTelefone);
    await expect(page.locator('input[name="morada"]')).toHaveValue(novaMorada);
  });

  test('página de quotas mostra 3 registos', async ({ page }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);
    await page.getByRole('navigation').getByRole('link', { name: 'Quotas', exact: true }).click();
    await expect(page).toHaveURL(/\/socio\/quotas/);
    // Tabela tem 3 linhas de body
    const rows = page.locator('tbody tr');
    await expect(rows).toHaveCount(3);
  });

  test('criar novo pedido de anilhas aparece na lista', async ({ page }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);
    await page.getByRole('navigation').getByRole('link', { name: 'Novo pedido' }).click();
    await expect(page).toHaveURL(/\/socio\/pedir-anilhas/);

    const marker = `PLAYWRIGHT-${Date.now()}`;
    await page.locator('input[name="especieCientifica"]').fill('Agapornis fischeri');
    await page.locator('input[name="especieNomeComum"]').fill('Fischeri');
    await page.locator('input[name="diametro"]').fill('4');
    await page.locator('input[name="quantidade"]').fill('15');
    await page.locator('textarea[name="observacoes"]').fill(marker);

    await Promise.all([
      page.waitForURL('**/socio/anilhas'),
      page.getByRole('button', { name: /submeter pedido/i }).click(),
    ]);

    // Confirma que o pedido aparece na lista com estado Pendente
    await expect(page.getByText(marker)).toBeVisible();
    const item = page.locator('li').filter({ hasText: marker });
    await expect(item.getByText('Pendente')).toBeVisible();
    await expect(item.getByText('Agapornis fischeri')).toBeVisible();
  });
});
