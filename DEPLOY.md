# Deploy — AOB Frontends

Referência rápida para desenvolvimento local e deploy para produção.

---

## Comandos do dia-a-dia

### Desenvolvimento local

```bash
# aobarcelos.pt  →  http://localhost:3000
cd frontends/aobarcelos
npm run dev

# bva-p.aobarcelos.pt  →  http://localhost:3001
cd frontends/bva-portugal
npm run dev
```

Os ficheiros `.env.local` já apontam para `http://localhost:5135` (API local).

### Deploy para produção

```bash
# 1. Compilar e montar pacote auto-suficiente
cd frontends/aobarcelos  &&  npm run build:prod
cd frontends/bva-portugal &&  npm run build:prod

# 2. Enviar para a VPS (para, faz upload, arranca, testa)
cd d:/PROJETOS/aob
python infra/deploy/_redeploy_frontends.py
```

Só isto. A VPS não executa nenhum `npm install`.

---

## O que o `build:prod` faz

```
npm run build:prod
    │
    ├─ next build          (produção, sem eval, sem source maps)
    ├─ patch-build.mjs     (corrige chunk path se necessário — bug Windows)
    └─ build-prod.mjs
           ├─ cria dist/
           ├─ copia dist/.next/         ← build output
           ├─ copia dist/public/        ← assets estáticos
           ├─ copia dist/node_modules/  ← react, react-dom, lucide-react
           │    (copiados do node_modules local, sem reinstalar)
           └─ copia dist/next.config.mjs + package.json
```

O pacote `dist/` (~40 MB) é auto-suficiente. O `next` não está incluído porque já está instalado globalmente na VPS (`/usr/lib/node_modules/next`).

---

## Ambientes e variáveis

| Ficheiro | Quando é usado |
|---|---|
| `.env.local` | `npm run dev` (dev local) |
| `.env.production` | `npm run build` / `npm run build:prod` (produção) |

**aobarcelos** `.env.production`:
```
NEXT_PUBLIC_API_URL=https://aobarcelos.pt
NEXT_PUBLIC_SITE_SLUG=aob
NEXT_PUBLIC_TURNSTILE_SITEKEY=1x00000000000000000000AA   ← substituir por chave real
```

**bva-portugal** `.env.production`:
```
NEXT_PUBLIC_API_URL=https://bva-p.aobarcelos.pt
NEXT_PUBLIC_SITE_SLUG=bva
NEXT_PUBLIC_TURNSTILE_SITEKEY=1x00000000000000000000AA   ← substituir por chave real
```

As variáveis `NEXT_PUBLIC_*` são compiladas no bundle — alterar requer rebuild.

---

## Porquê não usar `output: "standalone"`

O modo `standalone` do Next.js cria symlinks em `node_modules/` durante o build. No Windows sem **Developer Mode** activo, a criação de symlinks sem admin falha com `EPERM`.

A solução adoptada (`build:prod`) contorna isto copiando directamente as dependências de runtime do `node_modules` local para `dist/`, usando `realpathSync` para resolver os symlinks do pnpm antes de copiar.

Se activares o Developer Mode (Settings → Sistema → Para Programadores → Modo de Programador: ON), podes mudar para `output: "standalone"` em `next.config.mjs` — o deploy fica mais simples e o pacote mais pequeno.

---

## Backend (.NET)

```bash
# Build + deploy do backoffice (admin.aobarcelos.pt)
python infra/deploy/deploy.py admin

# Build + deploy da API (api.aobarcelos.pt)
python infra/deploy/deploy.py api
```

---

## Serviços na VPS

| Serviço | Porta | URL |
|---|---|---|
| `aob-api` | 5000 | `https://api.aobarcelos.pt` |
| `aob-admin` | 5001 | `https://admin.aobarcelos.pt` |
| `aob-aobarcelos` | 3000 | `https://aobarcelos.pt` |
| `aob-bva-portugal` | 3001 | `https://bva-p.aobarcelos.pt` |

```bash
# Ver estado de todos os serviços
python infra/deploy/_final_verify.py
```

---

## Troubleshooting rápido

| Sintoma | Causa | Fix |
|---|---|---|
| `Cannot find module 'react/jsx-runtime'` | `node_modules/react` ausente na VPS | Usar `npm run build:prod` em vez de `npm run build` |
| `EvalError: Code generation from strings disallowed` | Build gerou `eval()` no middleware | `devtool: false` em `next.config.mjs` — já configurado |
| `Cannot find module './435.js'` | Chunk path errado no webpack-runtime | `patch-build.mjs` corre no postbuild — já configurado |
| Site retorna 500 após deploy | Ver logs: `sudo journalctl -u aob-aobarcelos -n 30` | — |
| Artigo apagado continua no frontend | ISR cache — admin chama revalidação automaticamente | — |
