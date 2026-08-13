import { expect, type Page, type TestInfo } from '@playwright/test';

export type SocioMeta = {
  socioEmail: string;
  socioPassword: string;
  socioNumero: string;
  cookiePrefix: string;
};

export function meta(info: TestInfo): SocioMeta {
  return info.project.metadata as SocioMeta;
}

export async function loginAsSocio(page: Page, m: SocioMeta) {
  await page.goto('/socio/login');
  // Espera pela hidratação: o botão só reage ao click depois de o listener React estar montado.
  await page.waitForLoadState('networkidle');
  await page.getByLabel('Email').fill(m.socioEmail);
  await page.getByLabel('Password').fill(m.socioPassword);
  await Promise.all([
    page.waitForURL('**/socio'),
    page.getByRole('button', { name: /entrar/i }).click(),
  ]);
  await expect(page.getByRole('heading', { name: /olá/i })).toBeVisible();
}

export async function logoutSocio(page: Page) {
  await Promise.all([
    page.waitForURL('**/socio/login**'),
    page.getByRole('button', { name: /sair/i }).click(),
  ]);
}
