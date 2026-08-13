import { test, expect } from '@playwright/test';
import { meta, loginAsSocio } from './helpers';

test.describe('Middleware refresh token', () => {
  test('após apagar cookie de access, navegação renova via middleware', async ({ page, context }, info) => {
    const m = meta(info);
    await loginAsSocio(page, m);

    const before = await context.cookies();
    const accessBefore = before.find((c) => c.name === `${m.cookiePrefix}_at`)?.value;
    const refreshBefore = before.find((c) => c.name === `${m.cookiePrefix}_rt`)?.value;
    expect(accessBefore, 'access cookie inicial').toBeTruthy();
    expect(refreshBefore, 'refresh cookie inicial').toBeTruthy();

    // Simula expiração do access cookie apagando-o (o refresh continua válido)
    await context.clearCookies({ name: `${m.cookiePrefix}_at` });
    const clearedAt = (await context.cookies()).find((c) => c.name === `${m.cookiePrefix}_at`);
    expect(clearedAt, 'access cookie apagado').toBeUndefined();

    // Nova navegação — middleware deve refrescar transparentemente
    await page.goto('/socio/anilhas');
    await expect(page).toHaveURL(/\/socio\/anilhas$/);
    await expect(page.getByRole('heading', { name: /anilhas/i })).toBeVisible();

    // Access foi reposto e é diferente; refresh também rodou
    const after = await context.cookies();
    const accessAfter = after.find((c) => c.name === `${m.cookiePrefix}_at`)?.value;
    const refreshAfter = after.find((c) => c.name === `${m.cookiePrefix}_rt`)?.value;
    expect(accessAfter, 'access cookie reposto pelo middleware').toBeTruthy();
    expect(accessAfter).not.toBe(accessBefore);
    expect(refreshAfter, 'refresh cookie rodado').not.toBe(refreshBefore);
  });

  test('sem cookies: /socio/api/* devolve 401 JSON (não redirect)', async ({ context, request }, info) => {
    const m = meta(info);
    // Sem sessão — clear tudo
    await context.clearCookies();
    const baseURL = info.project.use.baseURL!;
    const res = await request.put(`${baseURL}/socio/api/perfil`, {
      data: { nomeCompleto: 'x', especiesInteresse: [] },
      headers: { 'Content-Type': 'application/json' },
      failOnStatusCode: false,
    });
    expect(res.status()).toBe(401);
    const body = await res.json().catch(() => null);
    expect(body?.ok).toBe(false);
  });
});
