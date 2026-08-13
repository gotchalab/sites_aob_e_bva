import { defineConfig, devices } from '@playwright/test';

const AOB_URL = process.env.AOB_URL ?? 'http://localhost:3000';
const BVA_URL = process.env.BVA_URL ?? 'http://localhost:3001';

export default defineConfig({
  testDir: './specs',
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ignoreHTTPSErrors: true,
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
  },
  projects: [
    {
      name: 'aob',
      testMatch: /.*\.(aob|shared)\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: AOB_URL,
      },
      metadata: {
        socioEmail: 'teste.socio@example.pt',
        socioPassword: 'TesteSocio123!',
        socioNumero: 'S9999',
        cookiePrefix: 'aob_socio',
      },
    },
    {
      name: 'bva',
      testMatch: /.*\.(bva|shared)\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: BVA_URL,
      },
      metadata: {
        socioEmail: 'teste.socio.bva@example.pt',
        socioPassword: 'TesteSocio123!',
        socioNumero: 'B9999',
        cookiePrefix: 'bva_socio',
      },
    },
  ],
});
