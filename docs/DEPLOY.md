# Deploy — AOB / BVA

Referência de deploy para produção (VPS OVH, Debian 11).

O deploy vive em dois scripts distintos:

| Script | Quando | Comportamento |
|---|---|---|
| **[`infra/deploy/deploy.py`](../infra/deploy/deploy.py)** | Deploy corrente do dia-a-dia | Publica binários / builds e recarrega serviços. Migrations correm dentro do target `api`. |
| **[`infra/deploy/deploy_inicial.py`](../infra/deploy/deploy_inicial.py)** | **Só bootstrap** de um VPS novo (ou disaster recovery) | Instala dotnet/next, cria users/dirs/PG role, restaura BD dev → prod. **Destrutivo.** Recusa correr sem `AOB_ALLOW_BOOTSTRAP=1`. |

Ver [CONTRIBUTING.md](../CONTRIBUTING.md) para o fluxo git (`dev` → `main` → tag → deploy).

---

## Pré-requisitos (uma só vez)

### Local (Windows / Linux / macOS)

- **.NET SDK 10** (`dotnet --version` → `10.x`).
- **Node.js 20+** e `npm` (para os frontends Next.js).
- **Python 3.10+** com `paramiko`:
  ```bash
  pip install paramiko
  ```
- **Chave SSH** em `~/.ssh/id_ed25519` autorizada no VPS (ou define `AOB_SSH_KEY`).
- **`npm install`** feito em `frontends/aobarcelos/` e `frontends/bva-portugal/` (o deploy usa o `node_modules` local para o build).
- Ficheiros `.env.production` populados em cada frontend (ver secção “Variáveis de ambiente”).

### VPS (feito pelo `deploy_inicial.py` — **não repetir**)

Já foi feito em 2026-08 no VPS actual (`51.83.40.43`). Só voltar a correr se provisionar um VPS novo — ver secção “Bootstrap inicial” abaixo.

Instala `.NET 10`, `next@15.5.4` global em `/usr/bin/next`, cria users `aob-api`/`aob-admin`/`aob-web`, diretórios em `/opt/aob/`, `/var/www/uploads/`, role e BD PostgreSQL.

Ficheiros `/etc/aob/*.env` (secrets prod) **têm de ser criados manualmente** a partir de `infra/deploy/env-samples/` — nenhum dos scripts de deploy os cria.

---

## Fluxo normal (deploy de release)

```bash
# 1) Merge dev -> main e tag (ver CONTRIBUTING.md)
git checkout main && git pull
git merge --no-ff dev -m "release: vX.Y.Z"
git tag -a vX.Y.Z -m "Descrição curta"
git push origin main --follow-tags

# 2) Deploy: builds locais + upload SFTP + restart systemd
python infra/deploy/deploy.py infra api admin aobarcelos bva services
```

`AOB_SSH_HOST` tem default `51.83.40.43` — para outro host, `AOB_SSH_HOST=... python infra/deploy/deploy.py ...`.

O `deploy.py` **avisa** se estiveres num branch diferente de `main` (não bloqueia). Para bloquear estritamente (ex.: CI):
```bash
AOB_STRICT_BRANCH=1 python infra/deploy/deploy.py ...
```

---

## Targets disponíveis (`deploy.py` — fluxo corrente)

Passar um ou mais como argumentos:

| Target | O que faz |
|---|---|
| `infra` | Sincroniza `infra/` → `/opt/aob/infra/`, copia nginx confs / systemd units, valida e recarrega nginx. Também copia a página de manutenção do `bva-p-socios`. |
| `api` | `dotnet publish AOB.Api` + `AOB.Migrator` → `/opt/aob/api/`, **corre `AOB.Migrator db-update` automaticamente** e depois restart `aob-api`. Se a migração falhar, o restart não acontece e a API antiga continua. |
| `admin` | `dotnet publish AOB.Admin` → `/opt/aob/admin/` + restart `aob-admin`. |
| `aobarcelos` | `npm run build` local → upload `.next/` + `public/` + `package.json` + `next.config.mjs` → `/opt/aob/aobarcelos/` + restart `aob-aobarcelos`. |
| `bva` | Idem, para `bva-p.aobarcelos.pt`. |
| `uploads` | Sincroniza `/uploads/` local → `/var/www/uploads/` (usa `--keep-newer-files`, nunca sobrescreve ficheiros mais recentes no VPS). Raro — só quando se semeou algo local que não passa pela BD. |
| `migrations` | Corre `AOB.Migrator db-update` sozinho. Raramente útil — o target `api` já o faz. Só usar quando queres aplicar migrations sem substituir os binários da API. |
| `services` | Pára Apache2, arranca nginx + todos os `aob-*`. |
| `all` | `infra api admin uploads aobarcelos bva services` (todos por esta ordem). |

**Bootstrap-only** (`setup`, `db`) já não vive em `deploy.py` — foram para `deploy_inicial.py` para eliminar o risco de correr algo destrutivo por engano no dia-a-dia.

**Deploy corrente típico:** `python infra/deploy/deploy.py infra api admin aobarcelos bva services` (as migrations correm dentro de `api`).

### Migrations automáticas

O target `api` corre `AOB.Migrator db-update` no VPS entre o upload dos binários e o restart do `aob-api`:

- Executado como `aob-api` com `/etc/aob/api.env` carregado (mesma connection string que a API).
- Idempotente — só aplica migrations pendentes (o comando lista quais antes de aplicar).
- Se falhar, o `deploy_api` aborta antes do restart e a API antiga continua a servir com o schema antigo (rollback natural).
- Para saltar (raríssimo, ex.: hotfix só do binário sem tocar em schema): `AOB_SKIP_MIGRATIONS=1 python infra/deploy/deploy.py api`.

O comando `AOB.Migrator migrate` (sem `db-update`) faz outra coisa completamente diferente — migra dados do Joomla legacy. **Não confundir.**

---

## Serviços na VPS

| Serviço systemd | Porta interna | URL público |
|---|---|---|
| `aob-api` | 5000 | `https://api.aobarcelos.pt` |
| `aob-admin` | 5001 | `https://admin.aobarcelos.pt` |
| `aob-aobarcelos` | 3000 | `https://aobarcelos.pt` |
| `aob-bva-portugal` | 3001 | `https://bva-p.aobarcelos.pt` |
| _(nenhum)_ | — | `https://bva-p-socios.aobarcelos.pt` → **503 estático** (área de sócios legacy desactivada por compromisso de segurança; ver `infra/nginx/bva-p-socios.aobarcelos.pt.conf`) |

Cada `aob-*.service` está em `infra/systemd/`.

---

## Variáveis de ambiente

### Frontends — build (compilado no bundle, alterar exige rebuild)

Ficheiros `.env.production` **de cada frontend** (`frontends/aobarcelos/`, `frontends/bva-portugal/`). São lidos pelo `next build` local.

- Valores dev-only vão em `.env.development.local` (não em `.env.local`, senão o `next build` usa-os em produção).
- Não pôr `NEXT_PUBLIC_*` na shell antes de correr o build — o Next dá prioridade à shell e o `.env.production` é ignorado silenciosamente. O `postbuild` (`patch-build.mjs`) valida a coincidência entre `.env.production` e o output; se algum valor não aparecer nos chunks, o build **falha** e o deploy aborta.

### Backend — runtime (lidos pelo processo `dotnet`)

Em `/etc/aob/api.env`, `/etc/aob/admin.env` no VPS (criados manualmente a partir de `infra/deploy/env-samples/*.env.sample`). Contêm connection string PostgreSQL, credenciais SMTP, chaves de API, etc.

---

## O que o `patch-build.mjs` faz (postbuild dos frontends)

Corre automaticamente após `next build`:

1. **Corrige o chunk path do `webpack-runtime.js`** — bug de builds Windows sem *Developer Mode* activo.
2. **Injecta `dataRoutes` / `staticRoutes` / `dynamicRoutes` vazios em `routes-manifest.json`** — bug do Next 15.5 no Windows em builds só-App-Router. Sem isto o `next start` no Linux crasha com `TypeError: routesManifest.dataRoutes is not iterable` em loop e o nginx devolve 502.
3. **Valida que os `NEXT_PUBLIC_*` de `.env.production` ficaram embutidos** no build. Se algum não aparecer nos chunks, falha o postbuild (deploy aborta por `set -euo pipefail` / `check=True` no paramiko).

---

## Troubleshooting

### `502 Bad Gateway` num frontend Next.js
```bash
sudo journalctl -u aob-aobarcelos -n 50
sudo journalctl -u aob-bva-portugal -n 50
```

- **`TypeError: routesManifest.dataRoutes is not iterable`** → build Windows sem o patch. Actualiza o `patch-build.mjs` (já corrigido nesta versão), rebuild e redeploy só do frontend afectado.
- **`Cannot find module 'react/jsx-runtime'`** → next global no VPS em versão errada. `next --version` deve dar `15.5.4`.
- **Envs erradas embutidas** → verificar `.env.production` e re-fazer `npm run build` (o postbuild bloqueia se estiver desalinhado).

### `aob-api` / `aob-admin` não arrancam
```bash
sudo journalctl -u aob-api -n 50
sudo systemctl status aob-api
```
- Erros de connection string ou credenciais → verificar `/etc/aob/api.env`.
- Se por algum motivo o target `api` foi corrido com `AOB_SKIP_MIGRATIONS=1` e há schema em falta → correr `python infra/deploy/deploy.py migrations` (aplica pendentes com o `AOB.Migrator` já no VPS).

### nginx: `nginx -t` falha após `deploy_infra`
- Alteração num `.conf` inválida — o `deploy.py` já valida antes de `reload`; se falhar, o config **não é aplicado** e o nginx continua com a versão anterior.

### Deploy relata sucesso mas o site continua 502
Os serviços marcam “active” no arranque do processo, mas podem crashar 1s depois. Confirmar sempre com curl externo:
```bash
for u in aobarcelos.pt bva-p.aobarcelos.pt api.aobarcelos.pt admin.aobarcelos.pt; do
  echo -n "$u → "; curl -s -o /dev/null -w "%{http_code}\n" -m 10 https://$u/
done
```

### Página de manutenção do `bva-p-socios` sem body
- O ficheiro `infra/nginx/bva-p-socios-maintenance.html` tem de estar em `/var/www/aob-maintenance/bva-p-socios/_maintenance.html` no VPS. O `deploy_infra` copia-o automaticamente.

---

## Rollback

```bash
# Reverter um ou mais commits e re-deploy
git checkout main
git revert <sha>...
git push origin main
python infra/deploy/deploy.py <alvo>
```

Ou, para reverter só um frontend rapidamente sem rebuild:
```bash
git checkout <tag-anterior> -- frontends/<name>/
python infra/deploy/deploy.py <aobarcelos|bva>
```

---

## Bootstrap inicial (VPS novo — não voltar aqui)

Só se tiveres de reprovisionar do zero (novo VPS, disaster recovery). O `deploy_inicial.py` recusa-se a correr sem `AOB_ALLOW_BOOTSTRAP=1` para evitar acidentes.

```bash
# Contra VPS vazio
AOB_ALLOW_BOOTSTRAP=1 python infra/deploy/deploy_inicial.py all
```

Targets do `deploy_inicial.py`:

| Target | O que faz |
|---|---|
| `setup` | Instala `.NET 10` em `/opt/dotnet`, `next@15.5.4` global, cria users (`aob-api`/`aob-admin`/`aob-web`), diretórios em `/opt/aob/`, `/var/www/uploads/`, role `aobapp` e BD `aob_prod` em PostgreSQL, instala nginx. Idempotente. |
| `db` | **DESTRUTIVO.** Faz `pg_dump` da BD local dev e restaura em `aob_prod` no VPS com `--clean --if-exists`. Só útil em bootstrap ou refresh consciente da BD prod. |
| `all` | `setup db` |

Passos manuais no VPS após o bootstrap:

1. Criar `/etc/aob/api.env`, `/etc/aob/admin.env`, `/etc/aob/aobarcelos.env`, `/etc/aob/bva-portugal.env` (ver `infra/deploy/env-samples/*.env.sample`).
2. `certbot --nginx -d aobarcelos.pt -d www.aobarcelos.pt -d bva-p.aobarcelos.pt -d api.aobarcelos.pt -d admin.aobarcelos.pt`.
3. Correr o deploy corrente para pôr o código actual: `python infra/deploy/deploy.py infra api admin aobarcelos bva services` (migrations correm dentro de `api`).

### Bootstrap-only no `AOB.Migrator`

Os comandos do Migrator relacionados com a migração inicial de dados do Joomla estão em [`backend/src/AOB.Migrator/Commands/Bootstrap/`](../backend/src/AOB.Migrator/Commands/) e listados em `dotnet run --project AOB.Migrator -- help` como *Bootstrap-only*. Nunca precisam de correr novamente em produção corrente.

---

## Notas históricas (não usar)

- **`infra/deploy/deploy.sh`** — versão bash que exige `rsync` no PATH. Não funciona em Windows sem WSL/MSYS2 com rsync. Mantido só para referência; usar `deploy.py`.
- **`frontends/*/scripts/build-prod.mjs` + `dist/`** — abordagem alternativa que empacotava `dist/` com `node_modules` seleccionados. Não é usada pelo `deploy.py` actual (envia `.next/` directo; o `next` global do VPS traz react/react-dom bundleados).
- **`infra/deploy/_archive/`** — scripts one-off de debug de sessões anteriores. Podem ser removidos.
