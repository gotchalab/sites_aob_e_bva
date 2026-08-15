# Plano de Reconstrução — aobarcelos.pt e bva-p.aobarcelos.pt

> **Documento criado em 2026-07-15** após compromisso dos sites Joomla no VPS OVH `51.83.40.43`.
>
> **Objetivo**: substituir os dois sites Joomla por uma solução moderna com **backend .NET Core**, **frontend React** e **backoffice**, mantendo tudo num VPS controlado por nós. Preparado para evoluir para funcionalidades transacionais (registo de sócios online, pedidos de anilhas com pagamento, etc.).

---

## 1. Contexto

### 1.1 Situação atual (o que foi comprometido)

**VPS OVH Debian 11** hospeda:

| Site | Framework | Estado | Conteúdo |
|---|---|---|---|
| `aobarcelos.pt` | Joomla 3.9.10 (EOL) | **Comprometido** | 125 artigos, 26 categorias, 123 downloads, ~275 MB media |
| `bva-p.aobarcelos.pt` | Joomla 3.9.10 (EOL) | **Comprometido** | 32 artigos, 25 categorias, 65 downloads, ~74 MB media |
| `socios.aobarcelos.pt` | .NET (SparkleIT.BackupManager.UI) | Não afetado — mantém-se | app dotnet na porta 5002 |

**Vetores de ataque identificados nos logs Apache:**
- `com_jce` (JCE Editor) — `task=profiles.import` — 18 POSTs recentes
- `com_sppagebuilder` — `task=asset.uploadCustomIcon` — 10 POSTs
- Plugin `nrframework` — 8+ POSTs (upload em `tmp`, `media`, `images`, `components`)
- Brute-force ao `/administrator/index.php` — 36 POSTs

**Malware encontrado**: dezenas de webshells PHP em `/tmp/`, `/images/`, `/images/stories/`. `.htaccess` malicioso configurado para executar `.txt` como PHP. Compromisso limitado ao user `www-data` (sem escalação para root aparente).

### 1.2 Objetivos da reconstrução

1. **Substituir** os dois sites Joomla por versões modernas
2. **Migrar** todo o conteúdo relevante (artigos, imagens, downloads)
3. **Preservar** URLs antigos → SEO (redirects 301)
4. **Ter backoffice próprio em .NET Core** para gestão de conteúdo
5. **Preparar arquitetura para evolução**:
   - Registo de sócios online (com pagamento?)
   - Pedidos de anilhas com fluxo de aprovação
   - Área reservada para sócios
   - Inscrições em exposições
6. **Manter tudo num VPS** — controlo total, mesma máquina para os 2 sites
7. **Endurecer segurança** para não repetir o incidente

### 1.3 Restrições e decisões

- Stack: **backend ASP.NET Core**, **frontend React**, **BD PostgreSQL**
- Ambiente dev: **Windows 11** + Visual Studio ou VS Code
- Cada site deve ser desenvolvível **em separado** (dois frontends, uma API partilhada ou APIs separadas — a decidir)
- Hospedagem **VPS** (novo ou o atual reinstalado)

---

## 2. Stack tecnológico

### 2.1 Escolhas principais

| Camada | Tecnologia | Justificação |
|---|---|---|
| **Backend API** | **ASP.NET Core 10 Web API** | Moderno, performático, ecosistema maduro (já instalado localmente) |
| **ORM** | **Entity Framework Core 10** | Standard .NET, migrations automáticas |
| **Base de dados** | **PostgreSQL 16** (nativo) | Robusto, open-source, suporte JSON nativo. Instalação `.msi` no Windows dev, `apt install postgresql` no VPS Debian |
| **Autenticação** | **ASP.NET Core Identity** + **JWT** (para SPA) | Standard, integrado com EF Core |
| **Backoffice** | **Blazor Server** (integrado na API) | Mesma stack .NET, produtividade alta, ideal para painel interno |
| **Frontend público** | **Next.js 15** (App Router, TypeScript) | SSR/SSG para SEO, React nativo, ecosistema forte |
| **Styling** | **Tailwind CSS 4** | Rápido, sem manutenção CSS, shadcn/ui compatível |
| **Componentes UI** | **shadcn/ui** + **@radix-ui** | Acessíveis, copy-paste (não vendor lock-in) |
| **Content (posts)** | **CKEditor 5** no backoffice → **HTML sanitizado** guardado na BD → **rich rendering** em Next.js | Editor WYSIWYG standard, seguro (sanitização com Ganss.XSS) |
| **Uploads** | **API endpoint** → validação MIME + ClamAV → **filesystem** (`/var/www/uploads`) | Nunca servidos como PHP; Nginx serve com `Content-Type: application/octet-stream` |
| **Media processing** | **ImageSharp** (.NET) | Redimensionar/converter para WebP no upload |
| **Search** | **PostgreSQL FTS** (Full-Text Search) integrado | Sem infraestrutura extra; bom para 100-200 artigos |
| **Cache** | **In-memory** (`IMemoryCache`) ou **Redis nativo** (`apt install redis`) — só se preciso | Provavelmente desnecessário na v1 |
| **Envio de email** | **Resend** ou SMTP (Exim4 já instalado no VPS) | Formulários, notificações de sócios |
| **Reverse proxy** | **Nginx** (instalação nativa Debian) | Standard, TLS, gzip, static caching |
| **TLS** | **Let's Encrypt / Certbot** (`apt install certbot`) | Grátis, automático, renewal via systemd timer |
| **Processo Node.js** | **PM2** ou **systemd service** | Gerir processos Next.js em produção sem crash |
| **Processo .NET** | **systemd service** | Standard Linux para daemons .NET |
| **CI/CD** | **GitHub Actions** → build + `dotnet publish` + `rsync` para VPS + `systemctl restart` | Deploy incremental, rápido |
| **Monitoring** | **Serilog** → ficheiros rotativos + **Uptime Kuma** (nativo) | Logs estruturados; monitorização básica |
| **WAF / DDoS** (opcional) | **Cloudflare** free plan | Adicionar depois do go-live se necessário |

**Nota importante — sem Docker por decisão:** Todo o stack corre nativamente. No dev (Windows), instala-se PostgreSQL como serviço Windows via installer. Em produção (VPS Debian), tudo via `apt` e `systemd`. Menos abstração, mais controlo, menos overhead de RAM (relevante num VPS de 2-4 GB).

### 2.2 Estrutura de projetos .NET (recomendada)

```
AOB.Backend/
├── AOB.Api/                    ← Web API (endpoints REST)
├── AOB.Admin/                  ← Blazor Server (backoffice)
├── AOB.Core/                   ← domínio (entities, interfaces)
├── AOB.Infrastructure/         ← EF Core, repositories, integrações externas
├── AOB.Application/            ← use cases (services, DTOs, validators)
└── AOB.Tests/                  ← testes unitários e integração
```

Padrão **Clean Architecture / Onion** — Core não depende de nada, tudo depende do Core. Facilita testes, evolução, e troca de tecnologia.

### 2.3 Estrutura Next.js (frontends)

Dois projetos separados (um por site), partilhando componentes num package:

```
frontends/
├── shared-ui/                  ← componentes React partilhados (Button, Card, etc.)
├── aobarcelos/                 ← Next.js 15 (App Router)
└── bva-portugal/               ← Next.js 15 (App Router)
```

Alternativa: **um só Next.js multi-tenant** (rotas por domínio) — mais complexo, só faz sentido se muitos sites. Para 2 sites, dois projetos é mais claro.

---

## 3. Arquitetura completa

### 3.1 Diagrama alto nível

```
                          ┌───────────────────────┐
                          │      Cloudflare       │ (OPCIONAL, adiar)
                          │  (WAF, CDN, TLS edge) │
                          └───────────┬───────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────┐
│                       VPS OVH (Debian 12 fresco)             │
│                                                              │
│   ┌────────────────────┐                                     │
│   │   Nginx (nativo)   │  (reverse proxy + TLS + gzip)       │
│   │  aobarcelos.pt     │                                     │
│   │  bva-p.aobarcelos.pt                                     │
│   │  admin.aobarcelos.pt                                     │
│   │  api.aobarcelos.pt                                       │
│   └──────┬─────────────┘                                     │
│          │                                                    │
│          ├───► [Next.js aobarcelos]     :3000  systemd/pm2   │
│          ├───► [Next.js bva-portugal]   :3001  systemd/pm2   │
│          ├───► [Blazor Admin (.NET)]    :5001  systemd       │
│          └───► [ASP.NET Core API]       :5000  systemd       │
│                       │                                       │
│                       ├──► [PostgreSQL 16]  :5432 (nativo)   │
│                       └──► [Filesystem]  /var/www/uploads    │
│                                                              │
│   ┌─────────────────────────────────────────────────────┐   │
│   │  Filesystem layout:                                   │   │
│   │   /opt/aob/api/           ← binários API (dotnet)    │   │
│   │   /opt/aob/admin/         ← binários Blazor          │   │
│   │   /opt/aob/aobarcelos/    ← build Next.js            │   │
│   │   /opt/aob/bva-portugal/  ← build Next.js            │   │
│   │   /var/www/uploads/       ← media (Nginx no-exec)    │   │
│   │   /var/lib/postgresql/    ← dados PG                 │   │
│   │   /etc/letsencrypt/       ← certificados             │   │
│   │   /etc/systemd/system/    ← .service files           │   │
│   └─────────────────────────────────────────────────────┘   │
│                                                              │
│   Manutenção: unattended-upgrades (patches auto)             │
│   Firewall: ufw (só 22, 80, 443)                             │
│   fail2ban + sshd só por chave                               │
└─────────────────────────────────────────────────────────────┘
```

**Serviços systemd**:
- `aob-api.service` → `dotnet /opt/aob/api/AOB.Api.dll`
- `aob-admin.service` → `dotnet /opt/aob/admin/AOB.Admin.dll`
- `aob-aobarcelos.service` → `node /opt/aob/aobarcelos/server.js`
- `aob-bva-portugal.service` → `node /opt/aob/bva-portugal/server.js`
- `postgresql.service` (do apt)
- `nginx.service` (do apt)

### 3.2 Modelo de dados (entidades principais)

**Postgres, gerido por EF Core migrations.**

```csharp
// Site — multi-tenancy (para os 2 sites partilharem API)
Site { Id, Slug, Name, Domain, Config (jsonb) }

// Utilizadores (auth + admin)
ApplicationUser : IdentityUser {
    FullName, IsAdmin, Role, PreferredSiteId
}

// Categorias hierárquicas
Category { Id, SiteId, Slug, Name, ParentId, Description, Order, IsPublished }

// Artigos (posts do blog)
Article {
    Id, SiteId, CategoryId, Slug, Title, Excerpt, Content (html),
    CoverImage, PublishedAt, UpdatedAt, AuthorId, IsPublished, Tags[],
    ViewCount, MetaTitle, MetaDescription, LegacyId (int?)  // p/ redirects
}

// Downloads (equivalente phocadownload)
Download {
    Id, SiteId, CategoryId, Slug, Title, Description,
    FilePath, FileSize, FileType, DownloadCount, IsPublished, PublishedAt
}

// Menus (estrutura de navegação)
MenuItem { Id, SiteId, Parent, Title, Url, Order, TargetType }

// Formulários — submissões guardadas + email enviado
FormSubmission {
    Id, SiteId, FormType (Contact/InscricaoSocio/PedidoAnilhas),
    Data (jsonb), SubmittedAt, Status, HandledBy, IpAddress
}

// FUTURO — Sócios
Socio {
    Id, SiteId, UserId, NumeroSocio, NomeCompleto, NIF, DataNascimento,
    Morada, Telefone, Email, DataInscricao, EstadoQuota,
    Foto, EspeciesInteresse[]
}

Quota { Id, SocioId, Ano, ValorEuros, DataPagamento, Metodo, Recibo }
PedidoAnilha { Id, SocioId, EspecieCientifica, Ano, Diametro, Quantidade, Estado, DataPedido, DataEntrega }
```

**Sanitização de HTML**: usar **HtmlSanitizer** (Ganss.XSS) ao guardar `Article.Content` — remove `<script>`, event handlers, `javascript:` URLs, etc.

### 3.3 Fluxo típico — publicar um artigo

1. Admin faz login em `admin.aobarcelos.pt` (Blazor)
2. Escreve artigo em CKEditor → clica "Publicar"
3. Blazor envia para `AOB.Api` (ou chama serviço diretamente)
4. `ArticleService` sanitiza HTML → `EF Core.Save()` → PostgreSQL
5. (opcional) Invalida cache ISR do Next.js via webhook: `POST https://aobarcelos.pt/api/revalidate?path=/artigos/<slug>`
6. Next.js regenera a página estática → utilizadores veem o novo artigo

### 3.4 Fluxo típico — visitante lê blog

1. Utilizador acede `https://aobarcelos.pt/artigos/diametro-das-anilhas`
2. Cloudflare edge → Nginx no VPS → Next.js
3. Next.js verifica cache ISR — se válido, serve HTML pré-gerado
4. Se não, chama `api.aobarcelos.pt/articles/diametro-das-anilhas`
5. API responde JSON → Next.js renderiza HTML → guarda em cache
6. Response ao utilizador

**Vantagem**: quase tudo é servido do cache. API só é chamada quando conteúdo muda ou o cache expira.

---

## 4. Plano de migração de conteúdo

### 4.1 Extração (do VPS comprometido)

**Passo 1 — Dump da BD MariaDB (via SSH)**

```bash
# no VPS (Apache já está parado)
mysqldump -uaobarcelosuser -p aobarcelos_site \
  --skip-lock-tables --no-tablespaces --single-transaction \
  --ignore-table=aobarcelos_site.u8zjq_session \
  > /tmp/aob.sql

mysqldump -ubvauser -p bva_site \
  --skip-lock-tables --no-tablespaces --single-transaction \
  --ignore-table=bva_site.h1a3c_session \
  > /tmp/bva.sql

# do local
scp debian@51.83.40.43:/tmp/aob.sql d:/PROJETOS/aob/data/
scp debian@51.83.40.43:/tmp/bva.sql d:/PROJETOS/aob/data/
```

**Passo 2 — Cópia filtrada das imagens (só formatos seguros, sem executáveis)**

Extensões a copiar: `.jpg .jpeg .png .gif .svg .webp .pdf .doc .docx .xls .xlsx .zip`
Extensões a **descartar**: `.php .phtml .phar .pht .txt .htaccess .ini .html` (podem ser shells)

```bash
# do local (Windows PowerShell ou WSL)
rsync -av \
  --include='*/' \
  --include='*.jpg' --include='*.JPG' --include='*.jpeg' --include='*.png' \
  --include='*.gif' --include='*.svg' --include='*.webp' --include='*.pdf' \
  --exclude='*' \
  debian@51.83.40.43:/var/www/aobarcelos_site/images/ \
  ./data/aob-images/

rsync -av \
  --include='*/' \
  --include='*.pdf' --include='*.doc' --include='*.docx' \
  --include='*.xls' --include='*.xlsx' --include='*.zip' \
  --exclude='*' \
  debian@51.83.40.43:/var/www/aobarcelos_site/phocadownload/ \
  ./data/aob-downloads/

# repetir para bva_site
```

**Passo 3 — Antivírus sobre extração**

Passar Windows Defender ou ClamAV sobre `./data/` antes de importar. Descartar qualquer ficheiro suspeito.

### 4.2 Transformação (Joomla → PostgreSQL)

Script **.NET Console App** — `AOB.Migrator/Program.cs`:

```csharp
// Pseudocódigo simplificado

using MySqlConnector;
using AOB.Infrastructure;   // AppDbContext, entities

async Task MigrateArticles(string sqlDumpPath, int siteId)
{
    // 1. Importar dump MariaDB para MariaDB local (Docker)
    // 2. Ler dados via MySqlConnector
    var articles = await ReadJoomlaArticles(mariaDbConn);

    // 3. Para cada artigo:
    foreach (var joomlaArticle in articles)
    {
        var html = joomlaArticle.IntroText + joomlaArticle.FullText;

        // Sanitizar HTML
        html = new HtmlSanitizer().Sanitize(html);

        // Reescrever paths de imagens (images/xxx.jpg → /uploads/legacy/xxx.jpg)
        html = RewriteImagePaths(html);

        var article = new Article
        {
            SiteId = siteId,
            CategoryId = MapCategoryId(joomlaArticle.CatId),
            Slug = joomlaArticle.Alias,
            Title = joomlaArticle.Title,
            Content = html,
            PublishedAt = joomlaArticle.PublishUp,
            IsPublished = joomlaArticle.State == 1,
            LegacyId = joomlaArticle.Id,           // preservar para redirects
            AuthorId = adminUserId,
            MetaTitle = joomlaArticle.MetaTitle,
            MetaDescription = joomlaArticle.MetaDesc,
        };
        _context.Articles.Add(article);
    }
    await _context.SaveChangesAsync();
}

async Task MigrateDownloads() { /* similar para phocadownload */ }
async Task MigrateCategories() { /* preservar hierarquia lft/rgt */ }
async Task CopyMediaFiles() { /* copiar imagens filtradas para /data/uploads/legacy/ */ }
```

**Ordem de execução**:
1. `MigrateCategories()` (aob → SiteId=1, bva → SiteId=2)
2. `CopyMediaFiles()` (para paths ficarem prontos)
3. `MigrateArticles()`
4. `MigrateDownloads()`
5. `GenerateRedirectMap()` (para Nginx)

### 4.3 Redirects (SEO)

Gerar ficheiro **`nginx/redirects.map`** a partir do `LegacyId` → `Slug`:

```
# gerado por script
~^/index\.php\?option=com_content&view=article&id=6.*   /artigos/diametro-das-anilhas;
~^/index\.php\?option=com_content&view=article&id=7.*   /artigos/pedido-de-anilhas;
...
```

E carregar em `nginx.conf`:

```nginx
map $request_uri $legacy_redirect {
    include /etc/nginx/redirects.map;
}

server {
    listen 443 ssl http2;
    server_name aobarcelos.pt;

    if ($legacy_redirect) {
        return 301 $legacy_redirect;
    }

    location / {
        proxy_pass http://nextjs-aob:3000;
        # ...
    }
}
```

Também tratar aliases legacy (URLs SEF do Joomla):
- `/index.php/pedidos-de-anilhas` → `/pedidos-de-anilhas`
- `/index.php/anilhas` → `/anilhas`

---

## 5. Segurança — não repetir o incidente

### 5.1 O que correu mal antes (para não repetir)

| Problema | Solução no novo sistema |
|---|---|
| Joomla 3.9.10 sem updates há 3 anos | .NET 10 + `dotnet list package --outdated` no CI + Dependabot |
| Permissões 777 em `/var/www/` | `/var/www/uploads/` com ownership `www-data:www-data` e chmod 750; API é dona |
| PHP a executar em qualquer pasta | Sem PHP no sistema (não instalar) |
| `.htaccess` com `AddHandler .txt .php` | Nginx serve `/uploads/` com `Content-Type: application/octet-stream` — nunca executa |
| PostgreSQL exposto a `0.0.0.0` | PG `listen_addresses = 'localhost'` em `postgresql.conf` |
| SSH com password | SSH só por chave (`PasswordAuthentication no`) |
| Sem WAF | (Opcional) Cloudflare grátis quando decidirmos |
| Sem fail2ban | `apt install fail2ban` + jail para SSH e Nginx auth |
| Sem atualizações automáticas | `unattended-upgrades` para patches de segurança |
| Compromise só detetado pelo utilizador | Uptime Kuma (self-hosted) + alertas email/discord em erros 500 |

### 5.2 Hardening do VPS

- [ ] **Reinstalar SO** — Debian 12 (Bookworm) fresco, ou continuar com Debian 11 patched
- [ ] **SSH**:
  - `PasswordAuthentication no`
  - `PermitRootLogin no`
  - Chave SSH por user
  - Porta diferente da 22 (opcional)
- [ ] **Firewall**: `ufw` — só 22 (SSH), 80, 443
- [ ] **fail2ban** — jails para SSH e Nginx auth
- [ ] **unattended-upgrades** — patches automáticos de segurança
- [ ] **Cloudflare** à frente:
  - Domínios via Cloudflare DNS
  - Proxy on (IP real escondido)
  - Rules: bloquear países de origem suspeitos (se aplicável)
  - Rate limiting: 60 req/min por IP
- [ ] **Isolamento de serviços (systemd)**:
  - Cada serviço com user próprio (ex: `aob-api`, `aob-web`)
  - Directivas systemd: `PrivateTmp=true`, `ProtectHome=true`, `ProtectSystem=strict`, `NoNewPrivileges=true`
  - Só o serviço API tem write access a `/var/www/uploads/`
- [ ] **Uploads**:
  - Validação MIME server-side (magic bytes, não confiar em extensão)
  - **ClamAV** nativo (`apt install clamav-daemon`), API chama scan antes de guardar
  - Nunca serve com `Content-Type` executável
  - Path `/var/www/uploads/` — Nginx `add_header Content-Disposition attachment` + `X-Content-Type-Options: nosniff`
  - Não executar Nginx com PHP interpreter para esta location (nem sequer instalado)
- [ ] **Logs**:
  - Serilog structured logging
  - Envio para container Seq (localhost)
  - Alertas via Discord/Slack webhook em erros críticos
- [ ] **Backups**:
  - `pg_dump` diário → volume separado + upload S3 (Cloudflare R2)
  - Snapshot semanal do VPS via OVH
  - Retention: 7 dias diários, 4 semanais, 6 mensais

### 5.3 Segurança da aplicação

- [ ] **ASP.NET Core Identity**:
  - Password policy forte (12+ chars, complexidade)
  - 2FA obrigatório para admins (TOTP)
  - Lockout após 5 tentativas
- [ ] **JWT**:
  - Curto expiry (15 min), refresh tokens
  - Signed + validated
- [ ] **HTTPS everywhere**:
  - HSTS 1 ano
  - Redirect HTTP → HTTPS
- [ ] **CSP** headers estritos
- [ ] **Anti-CSRF** tokens (Blazor tem por default; API usa JWT + SameSite)
- [ ] **Input validation** — FluentValidation em todos os DTOs
- [ ] **SQL injection** — EF Core parametriza tudo (nunca `FromRawSql` com concat)
- [ ] **XSS**:
  - HtmlSanitizer no server ao guardar
  - React escapa por default no cliente
  - CKEditor configurado com whitelist limitada
- [ ] **Upload restrictions**:
  - Whitelist de MIME types
  - Max 10 MB por ficheiro (config)
  - Nunca guardar com extensão original crua
- [ ] **Rate limiting** no ASP.NET Core (`AspNetCoreRateLimit`)
- [ ] **Turnstile** em todos os formulários públicos
- [ ] **Secrets**:
  - `.env` fora do repo
  - Docker secrets ou HashiCorp Vault (futuro)
- [ ] **Dependabot** ativo no repo
- [ ] **CodeQL** GitHub Actions
- [ ] **`dotnet list package --vulnerable`** no CI

---

## 6. Estrutura do repositório (monorepo)

```
d:/PROJETOS/aob/
├── PLANO-RECONSTRUCAO.md              ← este documento
├── README.md
├── .gitignore
├── .env.example                        ← template de variáveis
├── backup-vps-2026-07-15/              ← backup do VPS antigo (não commitar)
│
├── backend/                           ← solução .NET
│   ├── AOB.sln
│   ├── src/
│   │   ├── AOB.Api/                   ← Web API (endpoints REST) — porta 5000
│   │   ├── AOB.Admin/                 ← Blazor Server (backoffice) — porta 5001
│   │   ├── AOB.Core/                  ← domínio (entities, interfaces)
│   │   ├── AOB.Infrastructure/        ← EF Core, integrações
│   │   ├── AOB.Application/           ← use cases, services, DTOs
│   │   └── AOB.Migrator/              ← console app: Joomla → PG
│   └── tests/
│       ├── AOB.UnitTests/
│       └── AOB.IntegrationTests/
│
├── frontends/
│   ├── shared-ui/                     ← componentes React partilhados
│   │   ├── package.json
│   │   ├── src/components/
│   │   └── src/lib/api-client.ts     ← client TS gerado do OpenAPI
│   │
│   ├── aobarcelos/                    ← Next.js 15 — porta 3000
│   │   ├── package.json
│   │   ├── next.config.mjs
│   │   └── src/
│   │       ├── app/                   ← App Router
│   │       │   ├── page.tsx           ← home
│   │       │   ├── artigos/[slug]/page.tsx
│   │       │   ├── categoria/[slug]/page.tsx
│   │       │   ├── downloads/page.tsx
│   │       │   ├── contacto/page.tsx
│   │       │   └── layout.tsx
│   │       ├── components/            ← específicos deste site
│   │       ├── lib/
│   │       └── styles/
│   │
│   └── bva-portugal/                  ← Next.js 15 — porta 3001
│
├── infra/
│   ├── nginx/
│   │   ├── aobarcelos.pt.conf
│   │   ├── bva-p.aobarcelos.pt.conf
│   │   ├── api.aobarcelos.pt.conf
│   │   ├── admin.aobarcelos.pt.conf
│   │   └── redirects.map              ← gerado do migrator
│   ├── systemd/
│   │   ├── aob-api.service
│   │   ├── aob-admin.service
│   │   ├── aob-aobarcelos.service
│   │   └── aob-bva-portugal.service
│   └── deploy/
│       ├── setup-vps.sh               ← bootstrap: apt install postgresql nginx nodejs dotnet-runtime...
│       ├── deploy.sh                  ← rsync + systemctl restart
│       └── backup.sh                  ← pg_dump + upload S3 (cron diário)
│
├── scripts/
│   ├── extract-from-old-vps.sh        ← já feito (backup-vps-2026-07-15/)
│   ├── generate-typescript-client.sh  ← nswag: OpenAPI → TS client
│   └── seed-dev-data.sh
│
├── .github/
│   └── workflows/
│       ├── backend-ci.yml             ← build + tests
│       ├── frontend-aob-ci.yml
│       ├── frontend-bva-ci.yml
│       └── deploy-prod.yml            ← SSH deploy no push para main (rsync + systemctl)
│
└── docs/
    ├── DESENVOLVIMENTO.md
    ├── ARQUITETURA.md
    ├── DEPLOY.md
    ├── MIGRACAO.md
    └── SEGURANCA.md
```

---

## 7. Roadmap de desenvolvimento

### Fase 0 — Preparação e contenção (dia 0-1) ✅
- [x] Parar Apache do VPS comprometido
- [x] Dump BD MariaDB + rsync media filtrada + forense para `d:/PROJETOS/aob/backup-vps-2026-07-15/`
- [x] Verificação SHA-256 do backup (73/73 OK)
- [x] Decidir arquitetura: **VPS reinstalado + backend .NET + frontend React + sem Docker**
- [ ] Antivírus (Windows Defender) sobre pasta `backup-vps-2026-07-15/forense/`
- [ ] Instalar **PostgreSQL 16** para Windows (dev) — https://www.postgresql.org/download/windows/
- [ ] (Já ok) .NET 10 SDK, Node 24, pnpm, Git

### Fase 1 — Setup backend (dia 1-3)
- [ ] Criar solução .NET (`AOB.sln`) com projetos base
- [ ] Configurar EF Core + Npgsql provider + PostgreSQL local
- [ ] Definir entidades base (Site, Category, Article, Download, MenuItem, ApplicationUser)
- [ ] Primeira migration + seed com 2 sites (SiteId 1=aob, 2=bva)
- [ ] Endpoints CRUD básicos (Articles, Categories)
- [ ] Swagger/OpenAPI configurado
- [ ] Autenticação Identity + JWT + roles (Admin, Editor, Socio)
- [ ] `dotnet run` para dev; `dotnet publish` + systemd unit para prod

### Fase 2 — Migração de dados (dia 3-5)
- [ ] `AOB.Migrator` console app
- [ ] Ler dumps SQL diretamente com `MySqlConnector` (não é preciso importar MariaDB local — parseamos o `.sql` ou fazemos import temporário para PG via `pgloader`)
- [ ] Escrever mapping Joomla → nossa BD
- [ ] Sanitização HTML + reescrita de paths de imagens
- [ ] Testar com 5 artigos → validar output
- [ ] Migração completa dos 125+32 artigos + downloads
- [ ] Gerar `redirects.map` para Nginx (`legacy_id` → novo slug)
- [ ] Extrair `.tar.gz` das imagens filtradas para `/var/www/uploads/legacy/`

### Fase 3 — Backoffice Blazor (dia 5-8)
- [ ] Layout base (MudBlazor ou Radzen para componentes)
- [ ] Login + gestão de utilizadores
- [ ] Lista + edição de Artigos (CKEditor 5 integrado)
- [ ] Lista + edição de Categorias
- [ ] Lista + edição de Downloads (upload de ficheiros)
- [ ] Gestão de Menus
- [ ] Gestão de submissões de formulários
- [ ] Estatísticas simples (top artigos, views/dia)

### Fase 4 — Frontend aobarcelos.pt (dia 8-13)
- [ ] Setup Next.js 15 com TypeScript + Tailwind + shadcn/ui
- [ ] Cliente TS gerado do OpenAPI (nswag)
- [ ] Layout base (Header, Footer, Navigation)
- [ ] Página **Home** — hero + destaques + últimos artigos
- [ ] Páginas institucionais (Clube, Órgãos, Estatutos, Sede, Histórico) — carregadas da API
- [ ] Blog: listagem por categoria + página de artigo (ISR)
- [ ] Página **Downloads** com pesquisa/filtro
- [ ] Página **Pedidos de Anilhas** (com formulário)
- [ ] Página **Contactos** (form + iframe do mapa)
- [ ] Página **Exposições** com galeria por edição
- [ ] Pesquisa global (chama endpoint API que usa PG FTS)
- [ ] Sitemap dinâmico + robots.txt
- [ ] SEO: meta tags, JSON-LD (Organization, Article)

### Fase 5 — Frontend bva-p.aobarcelos.pt (dia 13-16)
- [ ] Mesma sequência da Fase 4, adaptada ao conteúdo BVA
- [ ] Menus: Quem Somos, História, Institucional, Comissão Técnica, Contactos, Associados, Anilhas, Direitos e Deveres, Parcerias, Morinha LAB, Revista AGAPORNIS info, Exposições (7ª e 8ª edições), BVA Masters, Standards, Divulgação de Criadores

### Fase 6 — Endurecimento infraestrutura (dia 16-18)
- [ ] `setup-vps.sh`: `apt install postgresql nginx nodejs dotnet-runtime-10 fail2ban ufw certbot python3-certbot-nginx unattended-upgrades clamav-daemon`
- [ ] PostgreSQL: `listen_addresses = 'localhost'`, criar user `aobapp` com password forte
- [ ] Nginx: sites-available/*.conf, TLS Let's Encrypt via certbot, gzip, rate limit
- [ ] systemd services criados (`aob-api`, `aob-admin`, `aob-aobarcelos`, `aob-bva-portugal`) com hardening (`PrivateTmp`, `ProtectSystem`, `NoNewPrivileges`)
- [ ] Certbot auto-renewal (`systemctl status certbot.timer`)
- [ ] fail2ban jails: sshd + nginx-http-auth + nginx-limit-req
- [ ] ufw: só 22, 80, 443
- [ ] SSH: `PasswordAuthentication no`, `PermitRootLogin no`, chave publica adicionada
- [ ] unattended-upgrades ativo para patches de segurança
- [ ] Backups: cron diário `pg_dump` + rsync `/var/www/uploads` → destino externo (Cloudflare R2 grátis)
- [ ] Uptime Kuma (nativo, Node.js) em `status.aobarcelos.pt` — monitoriza os endpoints
- [ ] **(OPCIONAL, adiar)** Cloudflare: adicionar domínio, mudar NS, ativar proxy

### Fase 7 — Deploy e go-live (dia 18-21)
- [ ] Deploy inicial em VPS staging (subdomínio `staging.aobarcelos.pt`)
- [ ] Testes end-to-end (Playwright?)
- [ ] Testar redirects críticos (top 20 URLs por hits)
- [ ] Google Search Console — submeter novo sitemap, pedir revisão de segurança
- [ ] Novo dump da BD do VPS antigo (para não perder alterações)
- [ ] Corte DNS para novos endpoints
- [ ] Monitorização apertada primeiras 48h

### Fase 8 — Pós go-live (dia 21+)
- [ ] Análise de logs, erros, 404s → corrigir
- [ ] Bloquear IPs atacantes recorrentes
- [ ] Documentação final
- [ ] Formação a quem vai gerir conteúdo no backoffice
- [ ] Cancelar/reformatar VPS antigo comprometido

### Fase 9 — Evolução: registo de sócios online (futuro)
- [ ] Entidade `Socio` + `Quota` + `PedidoAnilha`
- [ ] Página pública de inscrição (form + validação NIF)
- [ ] Área reservada de sócio (login + dashboard)
- [ ] Integração pagamento (Multibanco/MB Way via IfthenPay ou Easypay)
- [ ] Fluxo de aprovação no backoffice
- [ ] Emissão de recibos PDF
- [ ] Renovação anual automática

---

## 8. Custos estimados

### Infraestrutura mensal (recorrente)

| Item | Custo |
|---|---|
| VPS OVH (mesmo tier: 4 GB RAM, 40 GB disco) | ~7-15€ |
| Cloudflare (Free plan) | 0€ |
| Domínio `aobarcelos.pt` (anualizado) | ~1€/mês |
| Backup S3 / Cloudflare R2 | 0-2€ (R2: 10 GB grátis) |
| SendGrid / Resend (100 emails/dia grátis) | 0€ |
| ClamAV (self-hosted) | 0€ |
| **Total** | **~10-18€/mês** |

Similar ao atual — mas com muito mais capacidade e segurança.

### Custos de desenvolvimento (não recorrente)

Depende de quem faz. Se for equipa profissional externa: **estimativa 4-6 semanas x 1 dev full-stack sénior**.
Se for internamente: aproveitar este plano para timeboxear.

---

## 9. Riscos e mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|---|---|---|---|
| Migração HTML→sanitized deixa artigos com formatação partida | Média | Médio | Validar top-20 por hits manualmente |
| VPS novo também comprometido | Baixa | Alto | Hardening rigoroso, WAF Cloudflare, sem PHP, updates automáticos |
| Perda de SEO por URLs mal mapeados | Média | Alto | `redirects.map` completo, Search Console monitorização |
| Backoffice complexo para não-técnicos | Média | Médio | Formação, docs, UI simples (MudBlazor pré-built) |
| Deploy manual complica manutenção | Alta se não automatizado | Médio | GitHub Actions desde início, CI/CD |
| .NET + Node + PG consomem mais RAM que Joomla+PHP | Média | Baixo | Monitorizar; VPS 4 GB deve chegar; upgrade se necessário |
| CKEditor não sanitiza tudo | Baixa | Alto | Sanitização adicional server-side (Ganss.XSS) |

---

## 10. Decisões tomadas

### 10.1 VPS — **REINSTALAR** o atual (após backup completo)

- Backup completo local em `d:/PROJETOS/aob/backup-vps-2026-07-15/` (feito 2026-07-15)
- Depois de desenvolvermos e testarmos localmente, faz-se **reset OVH** ao VPS
- Instala-se Debian 12 fresco, com hardening desde o início
- Migração via Docker deploy (todo o stack containerizado)
- **Downtime**: sim, mas os sites já estão offline de qualquer forma

### 10.2 Cloudflare — **OPCIONAL, adiar para pós go-live**

- Não bloqueia desenvolvimento — o VPS funciona sem
- Setup é simples (5 passos, ~10 min + espera DNS) mas envolve mudar nameservers no registrar
- **Fase inicial**: DNS direto para o IP do VPS, TLS via Let's Encrypt no Nginx
- **Depois de estável**: se quisermos WAF + IP escondido + CDN, adicionamos Cloudflare em modo proxy

### 10.3 Multi-tenancy — **SIM, uma API multi-tenant**

- Uma BD, uma API, um backoffice
- Coluna `SiteId` em todas as tabelas de conteúdo
- Backoffice tem seletor "estás a trabalhar em: aobarcelos.pt / bva-p.aobarcelos.pt"
- **Justificação**: metade do código, uma migration, uma manutenção. Isolamento entre sites feito ao nível dos dados (query filtrada por `SiteId`)

### 10.4 Backoffice — **Blazor Server** em `admin.aobarcelos.pt`

- Mesma stack .NET → produtividade alta
- Acesso público mas com login forte (Identity + 2FA obrigatório para role Admin)
- Rate limiting no endpoint de login
- **Sem Cloudflare Access** (para manter simples inicialmente)

### 10.5 Roles / permissões

**Três roles inicialmente:**

| Role | Pode fazer |
|---|---|
| **Admin** | Tudo — gere conteúdo, downloads, menus, categorias, formulários, sócios, configuração, outros users |
| **Editor** (opcional/futuro) | Só criar/editar/publicar artigos e downloads. Sem gestão de sócios nem config. |
| **Socio** | Área reservada: ver e editar próprios dados, ver histórico de quotas, fazer pedidos de anilhas |

**Utilizador anónimo** (sem login) pode:
- Ver conteúdo público (blog, downloads, páginas institucionais)
- Submeter formulário de contacto
- Submeter formulário "quero ser sócio" → cria `FormSubmission` pendente

**Fluxo "novo sócio":**
1. Anónimo preenche formulário público → submissão pendente
2. Admin revê no backoffice, valida dados, aprova
3. Sistema cria `ApplicationUser` (role Socio) + `Socio` entity + envia email de boas-vindas com link para definir password
4. Sócio faz login → área reservada → pode editar dados, pedir anilhas, etc.

**Sócio pode APENAS:**
- Editar os SEUS dados (nome, morada, telefone, email, foto, espécies de interesse)
- Ver as SUAS quotas e histórico
- Fazer pedido de anilhas (que fica pendente para aprovação Admin)
- Alterar a própria password / 2FA
- **NÃO pode** ver dados de outros sócios, criar conteúdo, etc.

### 10.6 Área de sócios (futuro — Fase 9)

Endpoints protegidos (role Socio):
- `GET /api/me` — os meus dados
- `PUT /api/me` — atualizar dados
- `GET /api/me/quotas` — histórico quotas
- `GET /api/me/anilhas` — histórico pedidos
- `POST /api/me/pedidos-anilhas` — novo pedido
- `POST /api/me/change-password`

Frontend específico da área reservada em `/socio/*` (rotas Next.js protegidas).

---

## 11. Estado atual e próximas ações

### Já feito ✅
1. Apache parado no VPS
2. Backup completo em `d:/PROJETOS/aob/backup-vps-2026-07-15/` (1.34 GB, 73 ficheiros, checksums OK)
3. Plano escrito e revisto
4. Toolchain local verificada (.NET 10, Node 24, Python 3.12, Git, VS Code, PostgreSQL 16)
5. **Solução .NET** criada (AOB.Api, AOB.Admin, AOB.Core, AOB.Infrastructure, AOB.Application, AOB.Migrator)
6. **Primeira migration** (`InitialSchema`) aplicada — Postgres local `aob_dev` na porta 5433
7. **Migrator completo** (`AOB.Migrator`) com SSH tunnel para MariaDB VPS:
   - `seed` — 2 sites (aob + bva) + roles (Admin/Editor/Socio) + user admin@aobarcelos.pt
   - `migrate categories` — 20 aob + 16 bva com hierarquia preservada
   - `migrate articles` — 127 aob + 32 bva com HTML sanitizado (Ganss.XSS) e paths reescritos (`/images/...` → `/uploads/{site}/images/...`)
   - `migrate downloads` — 123 aob + 65 bva + 117+65 ficheiros físicos copiados para `uploads-target/{site}/downloads/`
   - `migrate menus` — 22 aob + 21 bva com resolução de links (article/category/url)
   - `migrate images` — 871 aob + 110 bva imagens copiadas (whitelist de extensões seguras)
   - `redirects` — `infra/nginx/redirects.aob.map` e `redirects.bva.map` gerados
8. **API pública read-only** (`AOB.Api`) — minimal APIs em `/api/sites/{siteSlug}/*`:
   - `GET /` — dados do site
   - `GET /menu?type=mainmenu` — árvore de menus
   - `GET /categories?kind=articles|downloads` — categorias em árvore
   - `GET /articles?category=&search=&page=&pageSize=` — lista paginada
   - `GET /articles/{slug}` — detalhe (+ increment ViewCount)
   - `GET /downloads?category=&search=&page=&pageSize=` — lista paginada
   - `GET /downloads/{slug}` — detalhe (+ increment DownloadCount)
9. **Uploads servidos via Kestrel** em `/uploads/{site}/{kind}/*` (headers X-Content-Type-Options: nosniff)
10. **Frontends Next.js 15** (App Router + TypeScript + Tailwind 4):
    - `frontends/aobarcelos/` na porta **3000** (tema azul, SITE_SLUG=aob)
    - `frontends/bva-portugal/` na porta **3001** (tema teal, SITE_SLUG=bva)
    - Páginas: home, `/artigos`, `/artigos/[slug]`, `/categoria/[slug]`, `/downloads`, `/downloads/[slug]`
    - Cliente API (`lib/api.ts`) com `revalidate: 300` (ISR)
    - Header dinâmico via `/api/sites/{slug}/menu`, footer com dados do site
    - Rewrites de `/uploads/*` para o backend
    - SEO: OpenGraph, meta tags dinâmicas
11. **Backoffice Blazor Server** (`AOB.Admin` na porta **5035**):
    - Identity + cookie auth (`/login`, `/auth/login`, `/auth/logout`)
    - Layout com sidebar Bootstrap + `<AuthorizeView>` + `RevalidatingIdentityAuthenticationStateProvider`
    - Dashboard com stats por site
    - CRUD Articles: lista paginada, edit com CKEditor 5, upload de cover
    - CRUD Categories: lista + edit
    - CRUD Downloads: lista + edit com CKEditor 5 + upload até 50MB
    - CRUD Menus (`/menus`): lista com reorder por site+menuType, edit com resolução de parent/target
    - CRUD Users (`/users`, Admin-only): listagem, criação, gestão de roles, reset password, unlock
    - `UploadService` grava em `uploads/{site}/{kind}/{yyyy}/{mm}/{safe-name}-{HHmmss}.{ext}` com whitelist de extensões
    - Endpoint `/admin/upload-inline` para upload inline via CKEditor
12. **CKEditor 5** integrado via CDN + JS interop (`HtmlEditor.razor` + `ckeditor-init.js`):
    - Toolbar: heading, bold/italic, links, listas, blockquote, table, image, undo/redo, sourceEditing
    - Upload adapter chama `/admin/upload-inline?site={slug}` → imagem gravada e URL devolvido
13. **Shortcode processor** (`AOB.Application/Content/ShortcodeExpander.cs`):
    - `{phocadownload view=file|id=N}` → link real para o download (com título + tamanho)
    - `{phocadownload view=category|id=N}` → link para lista de downloads
    - `{loadposition ...}` → removido
    - Aplicado no endpoint `GET /api/sites/{slug}/articles/{slug}` antes de servir

14. **Formulários públicos** com Turnstile e persistência:
    - Endpoints `POST /api/sites/{slug}/forms/{contact|inscricao-socio}` guardam `FormSubmission` (jsonb) + enviam email SMTP
    - `TurnstileVerifier` valida token contra Cloudflare (bypass em dev sem secret)
    - `EmailSender` SMTP simples (log em dev sem `Smtp:Host`)
    - Frontend `/contacto` com widget Turnstile (via `NEXT_PUBLIC_TURNSTILE_SITEKEY`)
    - Backoffice `/formularios` com filtros por site/estado/tipo e ações (marcar tratado/spam/eliminar)
15. **Endurecimento**:
    - Rate limit: 120 req/min por IP em endpoints públicos, 5 req/10min em `/forms/*`
    - Security headers: `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`
    - Serilog: consola + ficheiros rotativos diários (14 dias)
16. **Deploy VPS** — infraestrutura completa em `infra/`:
    - `deploy/setup-vps.sh` — bootstrap Debian 12 (PostgreSQL 16, .NET 10, Node 22, Nginx, Certbot, ClamAV, fail2ban, ufw, users próprios por serviço, hardening SSH, unattended-upgrades)
    - `systemd/aob-{api,admin,aobarcelos,bva-portugal}.service` com `NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome=true`, `ReadWritePaths` limitados
    - `nginx/*.conf` para 4 vhosts (aobarcelos.pt, bva-p.aobarcelos.pt, api.aobarcelos.pt, admin.aobarcelos.pt) com TLS via certbot, rate limit, WebSocket para Blazor, redirects.map inclusos
    - `deploy/deploy.sh {api|admin|aobarcelos|bva|infra|all}` — rsync + systemctl restart
    - `deploy/backup.sh` — `pg_dump` + tar dos uploads → `/var/backups/aob` + opcional Cloudflare R2
    - `env-samples/*.env.sample` para `/etc/aob/`
    - Next.js configurado com `output: "standalone"` para deploy sem `node_modules`

17. **JWT auth para SPA** (`AOB.Api`):
    - `AddIdentityCore` + `AddJwtBearer` + `AddAuthorization` com policies `Socio` e `Admin`
    - `JwtService` emite HS256 tokens (15 min access + 30 dias refresh) — refresh guardado como `AuthenticationToken` do Identity
    - Endpoints `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`
    - Claim `socio_id` incluído se `ApplicationUser.SocioId` estiver preenchido
18. **Entities de sócio** (`Socio`, `Quota`, `PedidoAnilha` + migration `AddSociosQuotasPedidos`):
    - `Socio` — número único por site, ligação opcional a `ApplicationUser`, arrays de espécies de interesse (text[])
    - `Quota` — histórico por ano com pagamento, método, recibo
    - `PedidoAnilha` — máquina de estados Pendente→Aprovado→Encomendado→Entregue→Cancelado
19. **Endpoints `/api/me`** protegidos por policy `Socio`:
    - `GET /` — dados do próprio sócio
    - `PUT /` — atualizar dados
    - `GET /quotas`, `GET /anilhas` — históricos
    - `POST /pedidos-anilhas` — criar pedido (fica Pendente)
20. **ClamAV integration** (`ClamAvScanner.cs`):
    - Protocolo TCP INSTREAM contra `clamd` (127.0.0.1:3310 por default)
    - `UploadService.SaveAsync` faz scan em memória antes de gravar — lança `MalwareDetected` se apanhado
    - Bypass silencioso em dev sem `ClamAv:Host` configurado
21. **Backoffice — gestão de sócios**:
    - `/socios` — lista com filtro por site + search
    - `/socios/{id}` — CRUD + criar user Identity ligado (role Socio) + ver histórico quotas/pedidos
    - `SocioAdminService.CreateUserForSocioAsync` — provisiona `ApplicationUser` + link `SocioId` + role
22. **Área de sócio no Next.js** (`/socio/*` em ambos os frontends):
    - Middleware protege `/socio/*` — redireciona para `/socio/login` se não autenticado
    - Refresh token automático: se `at` expirou mas `rt`+`uid` existem, chama `/api/auth/refresh` e propaga cookies; cookie `at` expira 30s antes do JWT
    - `POST /socio/api/login` grava cookies `httpOnly`; `POST /socio/api/logout` limpa + chama `/api/auth/logout`
    - Páginas: `/socio` (dashboard), `/socio/dados`, `/socio/quotas`, `/socio/anilhas`, `/socio/pedir-anilhas`, `/socio/login`
    - aobarcelos usa prefix `aob_socio_*`; bva-portugal usa `bva_socio_*`
23. **Backoffice — pedidos de anilhas** (`/pedidos-anilhas`):
    - Filtro "só pendentes" default, listagem com sócio + espécie + estado
    - Ação por linha: aprovar / encomendado / entregue (auto-marca `DataEntrega`) / cancelar
24. **Backoffice — quotas** dentro de `/socios/{id}`:
    - Tabela + linha inline "Ano/Valor/Pago em" para adicionar
    - Botão × para eliminar
25. **Categorias com hierarquia no backoffice**:
    - `ContentService.ListCategoryTree` devolve árvore com `Depth` calculado; `GetDescendantIds` evita ciclos
    - `CategoryEdit` — dropdown "Categoria pai" com proteção contra ciclos; `CategoryList` — indentação `└─`
    - `ArticleEdit` / `DownloadEdit` — dropdown de categoria com prefixo `— ` por nível
26. **Modernização das homepages** (secção 12 do plano):
    - Componentes em `src/components/home/`: `AnnouncementBar`, `HeroSection`, `StatsBar`, `MissionBlock`, `AreasGrid`, `FeaturedArticles`, `NewsGrid`, `JoinCta`, `SponsorsGrid`
    - `Sponsor`, `HomeConfig`, `Announcement` no backend; CRUD de patrocinadores no backoffice
    - `parseHomeConfig` / `parseAnnouncement` no `lib/api.ts` de cada frontend
27. **Página Quem Somos — BVA Portugal** (`/quem-somos`):
    - Hero, missão dinâmica (da BD), valores, StatsBar, AreasGrid, JoinCta
28. **Formulário inscrição convoyage — campo "Nº aves a concurso"**:
    - Input numérico (1–50) em "Dados do criador" sincroniza array de aves imediatamente
    - `addBird` / `removeBird` mantêm o contador atualizado
29. **Backoffice — gestão de convoyage** (`/convoyage`):
    - `ConvoyageAdminService` com CRUD: listar anos, criar ano, ativar ano, add/delete pontos de recolha
    - `ConvoyageList.razor` — cards por ano com pontos de recolha inline (add/delete), badge "Ativo", botão "Ativar"
    - Link no NavMenu
30. **Planeamento de transportes + export Excel** (F2 do plano):
    - Entidades `TransportCarga` + `TransportCargaSubmission`; migração `AddTransportPlanning`
    - Config editável no card do ano (NumCargasAlvo, CapacidadePorCarga, MinPorCarga, mapa transportadoras)
    - Setas ↑↓ para reordenar pontos de recolha (sul→norte)
    - `TransportPlanner` (serviço puro em `AOB.Application/Convoyage/`) — FFD por zona, mantém criador junto, merge de zona quando última carga < mínimo, round-robin de transportadores por zona
    - `TransportPlanAdminService` — orquestra plan/reset/mover/actualizar/export
    - Página `/convoyage/{yearId}/transportes` com botões "Gerar plano automático", "Exportar Excel", "+ Carga vazia", "Limpar plano"; drop-downs por linha para mover inscrições entre cargas
    - Endpoint `/convoyage/{id}/transportes/export` devolve .xlsx (ClosedXML) com 3 folhas: **Transportes** (layout do ano anterior), **Inscrições** e **Aves**
    - `Microsoft.EntityFrameworkCore.Design` adicionado ao Admin para permitir migrações a partir daí

### A fazer — funcionalidades

**F1. Formulário inscrição convoyage — suporte a equipas (T)** *(decidido 2026-08-14)*
Uma equipa = 4 aves da mesma espécie/classe/mutação, com anilhas próprias, ordenadas por posição na exposição (A no topo → D no fundo).

- **Secção 1 (Dados do criador)**: substituir o campo único `Nº aves a concurso` por dois campos: `Nº aves individuais a concurso` e `Nº equipas a concurso`. Ambos podem ser 0 mas não em simultâneo (validação: mínimo 1 unidade).
- **Secção 2 (Aves inscritas)**:
  - Cabeçalho com contador vivo: `X individuais + Y equipas · Z aves`.
  - Dois botões distintos: `+ Adicionar ave individual` e `+ Adicionar equipa` (para incrementar depois do pré-preenchimento por número).
  - Card de ave individual: mantém-se como está hoje.
  - Novo `TeamCard`: espécie + tipo (fixo em `Equipa (T)`) + classe/mutação únicos + **4 slots A/B/C/D empilhados** com anilha própria em cada. Setas ↑↓ à direita de cada slot para reordenar (relabela A/B/C/D automaticamente pela posição visual).
- **Estado interno** (frontend): separar `individualBirds: BirdState[]` e `teamBirds: TeamState[]` onde `TeamState = { id, species, code, mutation, selectionLabel, anilhas: [string, string, string, string] }`.
- **DTO (`AveConvoyageDto`)**: adicionar `EquipaId: Guid?` (partilhado pelas 4 aves) e `PosicaoEquipa: string?` ("A"|"B"|"C"|"D"). Equipa serializa como 4 registos com mesmo `EquipaId` e posições distintas.
- **Entidade `ConvoyageBirdEntry`**: adicionar `EquipaId: Guid?` e `PosicaoEquipa: string?` (nullable, char(1)). Migração EF Core `AddConvoyageTeamSupport`.
- **PDF (`InscricaoConvoyagePdfGenerator`)**: agrupar aves por `EquipaId`; equipas aparecem como bloco único titulado "Equipa (T) — série X" com 4 linhas ordenadas A→D.
- **Custos**: 1 equipa conta como 4 aves para todos os cálculos (`4 × 3€` inscrição, `4 × 3€` gaiola, `4 × tarifa` transporte). Fórmula em `ConvoyagePricing.Compute` mantém-se — apenas o `numAvesConcurso` passado somará 4 por equipa.
- **Validação backend**: se `EquipaId` presente, exigir exactamente 4 aves com o mesmo `EquipaId` e posições `A,B,C,D` únicas.

### A fazer — operacional (VPS / externo)
0. **JWT Key bvaproject** — substituir `0123456789ABCDEF` por chave forte em `/home/bva/bvaproject/appsettings.json` e reiniciar `bvaproject.service`
1. **Turnstile keys reais** — quando tiver as chaves Cloudflare Turnstile:
   - Atualizar `/etc/aob/api.env` (`TURNSTILE_SECRET_KEY=`)
   - Atualizar `.env.production` dos dois frontends (`NEXT_PUBLIC_TURNSTILE_SITEKEY=`)
   - Fazer rebuild + redeploy com `deploy.sh bva` e `deploy.sh aobarcelos`
2. **Formação** — guiar responsáveis a criar sócios, aprovar pedidos, registar quotas

**Notas de desenvolvimento Windows:** `next build` com `output: "standalone"` falha na fase de trace files por não conseguir criar symlinks sem admin (EPERM). O compile e a geração de páginas passam — o problema é apenas o empacotamento standalone e não afeta `dev` nem builds em Linux (VPS). Se preciso testar standalone localmente, correr o terminal como Administrador.

### Estado do VPS após limpeza (2026-08-12) ✅

- **`https://bva-p-socios.aobarcelos.pt`** — **no ar** com SSL (Let's Encrypt, válido até 2026-11-10)
- Apache2 activo a servir só o bvaproject (.NET porta 5002)
- Joomla AOB e BVA **removidos** (`/var/www/aobarcelos_site`, `/var/www/bva_site`)
- MariaDB **removida** (dados em `backup-vps-2026-07-15/databases/`)
- PHP 7.4 **removido**
- Malware XMRig **removido** (crontab `www-data` limpo; binários já tinham sido apagados com o bva_site)
- PostgreSQL: `listen_addresses = 'localhost'` — porta 5432 fechada ao exterior
- Passwords SSH, PostgreSQL `bva` e `postgres` **alteradas** (ver `CREDENCIAIS-LOCAIS.md`)
- Backup do bva-socios em `backup-vps-2026-08-12/bva-socios/` (532 MB BD + 545 MB app)
- **Pendente:** JWT Key do bvaproject é fraca (`0123456789ABCDEF`) — trocar antes de qualquer go-live
- **Pendente:** SMTP password `MCPb8cNJUn2YK5kI` — verificar/rodar no painel Sendinblue
- **Pendente:** SSH hardening — `PasswordAuthentication no` + chave pública + fail2ban + ufw

### Estado do VPS após deploy completo (2026-08-13) ✅

- **`https://aobarcelos.pt/`** — **no ar**, HTTP 200, SSL válido até 2026-10-15
- **`https://bva-p.aobarcelos.pt/`** — **no ar**, HTTP 200, SSL válido até 2026-11-11
- **`https://bva-p.aobarcelos.pt/artigos`**, `/contacto`, `/categoria/*` — todos 200
- PostgreSQL: grants `aobapp` corrigidos — API sem erros 42501
- Frontends reconstruídos com `NEXT_PUBLIC_API_URL` HTTPS baked (`https://aobarcelos.pt` e `https://bva-p.aobarcelos.pt`)
- `app-paths-manifest.json` do bva com 28/28 rotas — corrigido na rebuild
- `webpack-runtime.js` chunk prefix `./chunks/` — corrigido na rebuild
- ISR cache: `ReadWritePaths=/opt/aob/{app}/.next` nos serviços — Next.js escreve sem erro EROFS
- **UFW**: activo — apenas portos 22, 80, 443 abertos
- **fail2ban**: activo — jails: sshd, nginx-http-auth, nginx-limit-req
- **SSH**: `PasswordAuthentication no` + `PermitRootLogin no` + só chave ED25519
- **unattended-upgrades**: activo (patches automáticos)
- **certbot timer**: activo (renovação automática)
- Nginx vhosts configurados: `aobarcelos.pt`, `bva-p.aobarcelos.pt`, `api.aobarcelos.pt`, `admin.aobarcelos.pt`, `bva-p-socios.aobarcelos.pt`
- **`https://admin.aobarcelos.pt/`** → redireciona para `/login` (302) — Blazor backoffice acessível ✅
- **`https://api.aobarcelos.pt/api/sites/aob`** e `/bva` — HTTP 200 ✅
- SSL Let's Encrypt para `admin.aobarcelos.pt` e `api.aobarcelos.pt` válido até 2026-11-11 ✅
- **Pendente:** JWT Key do bvaproject é fraca (`0123456789ABCDEF`) — trocar antes de go-live dos sócios
- **Pendente:** Turnstile keys reais — substituir `1x00000000000000000000AA` em `/etc/aob/api.env` + `.env.production` dos frontends (requer rebuild + redeploy)

---

## 12. Modernização das homepages (planeado 2026-07-17)

> Decidido em conjunto com o responsável: as homepages atuais (só hero minimal + grid de artigos) são substituídas por uma home institucional profissional. Inclui reintroduzir a secção de patrocinadores que existia nos banners Joomla.

### 12.1 Objetivos

- Transmitir uma associação séria, com história e áreas de atuação claras
- Rotas rápidas para: ser sócio, pedidos de anilhas, exposições, patrocinadores
- Visual clean e moderno, coerente com a identidade de cada site
- SEO forte (Google Discover / rich snippets) e Lighthouse mobile ≥ 90

### 12.2 Estado herdado do Joomla — patrocinadores

- **BVA** — 12 parceiros publicados na tabela `h1a3c_banners` (categoria 15, state=1). Todos com logo em `images/banners/final/` e URL de destino: C.M. de Barcelos, Conceito Animal, Deli Nature, EXP Ideias, FONP, FOP, Pastelaria Lina, Morinha LAB, Bruna Araujo, baboduarte, inovegene, Napolitano
- **AOB** — tabela `u8zjq_banners` vazia; só existem imagens genéricas sem metadados. Fica sem patrocinadores iniciais — a secção "Patrocinadores" na home só renderiza quando `sponsors.length ≥ 1`

### 12.3 Backend — novas entidades e campos

**Migration `AddHomepageEssentials`:**

```csharp
Sponsor {
    Id, SiteId, Name, Slug, LogoPath, ClickUrl,
    Tier (Principal | Institucional | Apoio | Parceiro),
    IsPublished, SortOrder, LegacyId (int?), Notes
}

Article.IsFeatured (bool)                 // + índice (SiteId, IsFeatured, PublishedAt)

Site.Tagline (string?)                    // frase curta abaixo do H1
Site.HomeConfig (jsonb)                   // ver schema abaixo
Site.Announcement (jsonb?)                // banda fina de aviso opcional
```

**Schema `Site.HomeConfig`:**

```json
{
  "mission": "…texto curto 2 parágrafos…",
  "missionImageUrl": "/uploads/aob/home/mission.jpg",
  "foundedYear": 1985,
  "memberCount": 240,
  "ringsPerYear": 12000,
  "speciesCount": 45,
  "areas": [
    { "icon": "Award",    "title": "Anilhas oficiais", "description": "…", "href": "/pedidos-anilhas" },
    { "icon": "Calendar", "title": "Exposições",       "description": "…", "href": "/categoria/exposicoes" },
    { "icon": "BookOpen", "title": "Formação",         "description": "…", "href": "/downloads" },
    { "icon": "Users",    "title": "Comunidade",       "description": "…", "href": "/quem-somos" }
  ],
  "ctaTitle": "Junta-te à associação",
  "ctaSubtitle": "…",
  "ctaHref": "/inscricao-socio",
  "ctaLabel": "Quero ser sócio"
}
```

Se `foundedYear`/`memberCount`/... forem `null`, a `<StatsBar>` esconde essas cards em vez de mostrar zeros.

**Schema `Site.Announcement`:**

```json
{ "enabled": true, "message": "8ª Exposição BVA — inscrições até 30 nov", "href": "/exposicoes", "tone": "event" }
```
Tones: `info` (azul) · `warning` (âmbar) · `event` (brand).

**Endpoints API:**

- `GET /api/sites/{slug}/sponsors` — publicados, ordenados por Tier + SortOrder
- `GET /api/sites/{slug}/articles?featured=true` — extensão do endpoint existente
- `Site` DTO passa a expor `tagline`, `homeConfig`, `announcement`
- `POST /api/revalidate?path=/&secret=…` — chamado pelo backoffice ao publicar/editar

**Backoffice Blazor:**

- `/patrocinadores` — CRUD com upload logo via `UploadService`, dropdown de Tier, reorder por SortOrder
- `ArticleEdit` — checkbox "Destaque na home"
- `/site` (nova) — form com `Tagline`, `HomeConfig` (repetidor de áreas com dropdown de ícones da whitelist), `Announcement` (toggle + editor)
- Whitelist de ícones lucide para áreas: `Award, Calendar, BookOpen, Users, Feather, Trophy, HeartHandshake, Sparkles, MapPin, GraduationCap, Landmark, Newspaper`

**Migrator (`AOB.Migrator sponsors`):**

- Lê `h1a3c_banners` do BVA onde `state=1 AND catid=15`
- Extrai `imageurl` do JSON de `params` e copia ficheiro de `backup-vps-2026-07-15/uploads-target/bva/images/banners/final/*.jpg` para `uploads/bva/sponsors/{slug}.jpg`
- Mapeia: `name → Name`, `alias → Slug`, `clickurl → ClickUrl`, `ordering → SortOrder`, `id → LegacyId`
- Tier default: `Parceiro` (admin refina depois)

**Migrator (`AOB.Migrator home-content-seed`):**

- Extrai texto das categorias com slugs tipo `quem-somos`, `clube`, `institucional`, `historia`
- Pré-popula `Site.HomeConfig.mission` com resumo (primeiros 400 chars, sanitizado)
- Backoffice avisa "conteúdo pré-populado a partir do Joomla — reveja"

### 12.4 Estrutura da homepage (comum aos 2 sites)

Ordem de secções, top→bottom:

1. **AnnouncementBar** (só se `announcement.enabled=true`)
2. **HeroSection** — H1 forte, tagline, 2 CTAs, meta ("Fundada em XXXX · N sócios"), imagem/artigo em destaque à direita
3. **StatsBar** — 4 cards com ícones (fundação, sócios, anilhas/ano, espécies)
4. **MissionBlock** — 2 col: parágrafo + imagem, link "Sobre a associação →"
5. **AreasGrid** — 3-4 cards com ícone lucide + título + descrição + link
6. **FeaturedArticles** — 1 grande + 2 pequenos, filtrados por `IsFeatured=true` (fallback: mais recentes)
7. **NewsGrid** — grid 3-col dos últimos artigos com badge `Novo` (<72h) + pill de categoria colorida + data relativa
8. **JoinCta** — banda largura total, cor de marca, texto+botão único
9. **SponsorsGrid** — logos em preto&branco, hover a cores, agrupados por Tier ("Com o apoio de" / "Parceiros") — só renderiza se `sponsors.length ≥ 1`
10. **Footer** melhorado — colunas Sobre / Rápido / Contactos / Redes sociais + link RSS

### 12.5 Realce a artigos publicados

**Múltiplas zonas na home** para dar ritmo editorial:
- Destaque hero (1) — o `IsFeatured` mais recente
- Featured strip (2-3) — outros `IsFeatured`
- Notícias recentes (6-9) — últimos publicados
- Footer "Novidades" — top-3 mais recentes por categoria "Notícias"

**Sinalização visual:**
- Badge `Novo` (pill dourada AOB / teal BVA) automático em artigos publicados nas últimas 72h
- Data em formato relativo ("Há 2 dias") para <7 dias, absoluto depois
- Pill de categoria colorida (não só texto)

**Frescura técnica:**
- ISR `revalidate: 60` na home (vs. 300s nas páginas de artigo)
- Webhook `POST /api/revalidate?path=/` disparado pelo backoffice ao publicar → invalidação imediata
- Feed `/rss.xml` e `/atom.xml` por site — subscritores + indexação Google

### 12.6 SEO — estrutura e discovery

**JSON-LD estruturado:**
- `<Organization>` no `layout.tsx` (nome, logo, endereço, sameAs redes sociais) — Knowledge Graph
- `<Article>` em cada artigo (headline, image, author, datePublished, dateModified) — Google Discover, rich snippets
- `<BreadcrumbList>` em artigo/categoria
- `<Event>` nas exposições (schema preparado, ativação futura)

**Meta:**
- Canonical URL em todas as páginas (`<link rel="canonical">`)
- OpenGraph + Twitter Card com cover do artigo (fallback: logo do site)
- `hreflang="pt-PT"` explícito
- Meta description dinâmica

**Discovery:**
- `sitemap.xml` dinâmico com `lastmod` real de cada artigo
- `robots.txt` por site apontando o sitemap
- Redirects 301 do Joomla (já no `redirects.map`) — preserva PageRank existente
- Submissão manual no Google Search Console após go-live + pedido re-índexação

### 12.7 Performance — metas Lighthouse mobile

| Categoria | Meta | Como |
|---|---|---|
| Performance | **≥ 90** | ISR + `next/image` (WebP) + Server Components + preconnect à API |
| SEO | **100** | JSON-LD + sitemap + meta + canonical |
| Acessibilidade | **≥ 95** | Contraste WCAG AA + alt obrigatório + landmarks + skip-to-content |
| Best Practices | **100** | HTTPS + security headers (X-Content-Type-Options, Referrer-Policy, CSP) |

**Técnicas:**
- `next/image` com WebP automático, `srcset`, lazy loading nativo → LCP baixo
- `aspect-ratio` nas thumbs → CLS ≈ 0
- Fonts com `next/font` self-hosted, `display: swap`, subset latino → sem FOIT
- Preconnect a `api.aobarcelos.pt` no `<head>`
- 90% Server Components; ilhas cliente só em load-more e forms
- Nginx: `gzip` + `brotli`; cache-control 1 ano imutável em `/_next/static/*`, 7 dias em `/uploads/*`
- `prefers-reduced-motion` respeitado nas transições

**Gate no CI:** `lighthouse-ci` no GitHub Actions bloqueia deploy que faça regredir alguma categoria abaixo do target.

### 12.8 Design tokens

- **AOB**: hero com gradiente earth-900 → earth-700 + textura sutil, gold como accent, headings serif `Cormorant`/`Playfair`, tracking negativo em H1
- **BVA**: hero teal-900 → teal-700 + serif nos headings, brand-100 nos accents
- Cards uniformes: `border border-black/5 rounded-2xl shadow-sm hover:shadow-md hover:-translate-y-0.5 transition`
- Secções com `py-24 md:py-32`, container `max-w-6xl`
- Skip-to-content link, foco visível `focus-visible:ring-2 ring-brand-500`

### 12.9 Faseamento

| Fase | Duração | Entregável |
|---|---|---|
| A — Backend: `Sponsor` + `IsFeatured` + `HomeConfig` + `Announcement` + endpoints + `/site` no backoffice | 0.7d | Migration aplicada, CRUD funcional |
| B — Migrator: sponsors BVA (12) + home-content-seed dos 2 sites | 0.3d | Sponsors + missão pré-populados |
| C — Kit de componentes home (por frontend, em `src/components/home/`) | 0.5d | Reutilizável |
| D — Homepage AOB reescrita | 0.5d | Home nova em dev |
| E — Homepage BVA reescrita | 0.5d | Home nova em dev |
| F — SEO & Performance checklist + `lighthouse-ci` no CI | 0.3d | Metas atingidas, gate no CI |

**Total: ~2.8 dias.**

Ordem de execução: A → B → C → D → E → F. Fases D e E podem paralelizar depois de C.

### 12.10 Componentes a criar

Por cada frontend, em `src/components/home/`:

- `AnnouncementBar.tsx` (client — cookie de dismiss opcional)
- `HeroSection.tsx` (server)
- `StatsBar.tsx` (server, esconde cards com `null`)
- `MissionBlock.tsx` (server)
- `AreasGrid.tsx` (server, ícones `lucide-react`)
- `FeaturedArticles.tsx` (server, fallback a "recentes" se `featured=[]`)
- `NewsGrid.tsx` (polish do `LoadMoreArticles` atual, badge "Novo")
- `JoinCta.tsx` (server)
- `SponsorsGrid.tsx` (server, esconde se vazio, agrupa por Tier)

Sem package `shared-ui` extra — cada site tem cópia dos componentes para permitir divergência de estilo/copy sem `if site==...`.

---

## 13. Referências

- **.NET 9 docs**: https://learn.microsoft.com/dotnet/
- **ASP.NET Core**: https://learn.microsoft.com/aspnet/core/
- **Blazor**: https://learn.microsoft.com/aspnet/core/blazor/
- **EF Core**: https://learn.microsoft.com/ef/core/
- **Next.js App Router**: https://nextjs.org/docs
- **shadcn/ui**: https://ui.shadcn.com/
- **Tailwind CSS**: https://tailwindcss.com/docs
- **HtmlSanitizer (Ganss.XSS)**: https://github.com/mganss/HtmlSanitizer
- **CKEditor 5 (open source)**: https://ckeditor.com/ckeditor-5/
- **MudBlazor**: https://mudblazor.com/
- **Cloudflare Pages/DNS/WAF**: https://developers.cloudflare.com/

---

*Documento vivo — atualizar à medida que decisões forem sendo tomadas.*
