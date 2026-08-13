// Smoke test do fluxo Next.js /socio/*:
// - login via /socio/api/login (grava cookies httpOnly)
// - navegação a /socio (middleware valida sessão)
// - middleware refresh: apagar cookie access, navegar, ver que refresca
// - POST /socio/api/pedidos-anilhas
// - logout limpa cookies + revoga refresh

let pass = 0, fail = 0;
function check(name, cond, extra = '') {
  if (cond) { console.log(`[PASS] ${name} ${extra}`); pass++; }
  else      { console.log(`[FAIL] ${name} ${extra}`); fail++; }
}

class Jar {
  constructor() { this.cookies = new Map(); }
  ingest(setCookieHeaders) {
    if (!setCookieHeaders) return;
    // In Node fetch, response.headers.getSetCookie() returns array
    for (const h of setCookieHeaders) {
      const [nameValue] = h.split(';');
      const eq = nameValue.indexOf('=');
      if (eq < 0) continue;
      const name = nameValue.slice(0, eq).trim();
      const value = nameValue.slice(eq + 1).trim();
      const lower = h.toLowerCase();
      if (lower.includes('max-age=0') || /expires=thu, 01 jan 1970/i.test(h) || value === '') {
        this.cookies.delete(name);
      } else {
        this.cookies.set(name, value);
      }
    }
  }
  header() {
    return [...this.cookies.entries()].map(([k, v]) => `${k}=${v}`).join('; ');
  }
  get(name) { return this.cookies.get(name); }
  delete(name) { this.cookies.delete(name); }
  size() { return this.cookies.size; }
}

async function fetchWithJar(url, jar, opts = {}) {
  const headers = new Headers(opts.headers ?? {});
  const cookieHeader = jar.header();
  if (cookieHeader) headers.set('cookie', cookieHeader);
  const res = await fetch(url, { ...opts, headers, redirect: 'manual' });
  jar.ingest(res.headers.getSetCookie?.() ?? []);
  const text = await res.text();
  let body = null;
  try { body = text ? JSON.parse(text) : null; } catch {}
  return { status: res.status, body, text, headers: res.headers };
}

async function testFrontend({ site, base, email, cookiePrefix, nsoc }) {
  console.log(`\n=== ${site.toUpperCase()} — Next.js em ${base} ===`);
  const jar = new Jar();

  // Sem sessão: /socio deve redirect para /socio/login
  const noSession = await fetchWithJar(`${base}/socio`, jar);
  check('sem sessao: /socio redirect', noSession.status === 307 || noSession.status === 302, `(${noSession.status})`);
  check('sem sessao: Location = /socio/login', (noSession.headers.get('location') ?? '').includes('/socio/login'));

  // Login via /socio/api/login
  const login = await fetchWithJar(`${base}/socio/api/login`, jar, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'TesteSocio123!' }),
  });
  check('login /socio/api/login 200', login.status === 200, `(${login.status})`);
  check(`cookie ${cookiePrefix}_at gravado`, !!jar.get(`${cookiePrefix}_at`));
  check(`cookie ${cookiePrefix}_rt gravado`, !!jar.get(`${cookiePrefix}_rt`));
  check(`cookie ${cookiePrefix}_uid gravado`, !!jar.get(`${cookiePrefix}_uid`));

  // Com sessão: /socio dashboard renderiza HTML
  const dash = await fetchWithJar(`${base}/socio`, jar);
  check('/socio dashboard 200', dash.status === 200, `(${dash.status})`);
  check('/socio dashboard contem nome', dash.text.includes('Teste Socio') || dash.text.includes(nsoc));
  check('/socio dashboard contem "Quota"', dash.text.includes('Quota'));

  // /socio/dados renderiza form
  const dados = await fetchWithJar(`${base}/socio/dados`, jar);
  check('/socio/dados 200', dados.status === 200);
  check('/socio/dados contem "meus dados"', dados.text.toLowerCase().includes('meus dados'));

  // POST /socio/api/perfil (proxy para PUT /api/me)
  const putBody = {
    nomeCompleto: `Teste Socio (${site.toUpperCase()})`,
    telefone: '922333444',
    nif: null,
    dataNascimento: null,
    morada: 'Rua Frontend Test, 99',
    codigoPostal: '4700-000',
    localidade: 'Braga',
    especiesInteresse: ['Melopsittacus undulatus'],
  };
  const putRes = await fetchWithJar(`${base}/socio/api/perfil`, jar, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(putBody),
  });
  check('PUT /socio/api/perfil 200', putRes.status === 200, `(${putRes.status})`);

  // Confirmar persistência via nova request
  const dadosApos = await fetchWithJar(`${base}/socio/dados`, jar);
  check('/socio/dados reflete telefone novo', dadosApos.text.includes('922333444'), '(check via HTML)');
  check('/socio/dados reflete morada nova', dadosApos.text.includes('Rua Frontend Test, 99'));

  // Middleware refresh: apagar cookie access, navegar novamente — deve refrescar automaticamente
  const oldAccess = jar.get(`${cookiePrefix}_at`);
  const oldRefresh = jar.get(`${cookiePrefix}_rt`);
  jar.delete(`${cookiePrefix}_at`);
  check('cookie access apagado antes do refresh test', !jar.get(`${cookiePrefix}_at`));

  const dashRefresh = await fetchWithJar(`${base}/socio`, jar);
  check('/socio pos-refresh 200', dashRefresh.status === 200, `(${dashRefresh.status})`);
  const newAccess = jar.get(`${cookiePrefix}_at`);
  const newRefresh = jar.get(`${cookiePrefix}_rt`);
  check('middleware repos cookie access', !!newAccess);
  check('novo access difere do antigo', newAccess !== oldAccess);
  check('middleware roda refresh token', newRefresh !== oldRefresh);
  check('/socio pos-refresh renderiza sem redirect', dashRefresh.text.includes('Quota'));

  // POST /socio/api/pedidos-anilhas com access renovado pelo middleware
  const pedidoBody = {
    especieCientifica: 'Nymphicus hollandicus',
    especieNomeComum: 'Caturra',
    ano: 2026,
    diametro: 5.5,
    quantidade: 5,
    observacoes: `Frontend smoke ${site}`,
  };
  const pedidoRes = await fetchWithJar(`${base}/socio/api/pedidos-anilhas`, jar, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(pedidoBody),
  });
  check('POST pedido via Next 200', pedidoRes.status === 200, `(${pedidoRes.status})`);
  check('POST pedido devolve id', pedidoRes.body?.id > 0, `(id=${pedidoRes.body?.id})`);

  // Middleware para /socio/api/* sem sessão devolve 401 JSON (não redirect)
  const evilJar = new Jar();
  const apiNoAuth = await fetchWithJar(`${base}/socio/api/perfil`, evilJar, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(putBody),
  });
  check('sem sessao: /socio/api/* devolve 401', apiNoAuth.status === 401, `(${apiNoAuth.status})`);
  check('401 body eh JSON', apiNoAuth.body != null);

  // Logout: limpa cookies + revoga refresh no backend
  const logout = await fetchWithJar(`${base}/socio/api/logout`, jar, { method: 'POST' });
  check('logout 200', logout.status === 200);
  check('cookies limpos apos logout', !jar.get(`${cookiePrefix}_at`) && !jar.get(`${cookiePrefix}_rt`) && !jar.get(`${cookiePrefix}_uid`));

  // Depois de logout, tentar navegar deve redirecionar
  const afterLogout = await fetchWithJar(`${base}/socio`, jar);
  check('apos logout: /socio redirect', afterLogout.status === 307 || afterLogout.status === 302, `(${afterLogout.status})`);
}

(async () => {
  await testFrontend({
    site: 'aob', base: 'http://localhost:3000',
    email: 'teste.socio@example.pt', cookiePrefix: 'aob_socio', nsoc: 'S9999',
  });
  await testFrontend({
    site: 'bva', base: 'http://localhost:3001',
    email: 'teste.socio.bva@example.pt', cookiePrefix: 'bva_socio', nsoc: 'B9999',
  });
  console.log(`\n=== Resumo Next: ${pass} PASS / ${fail} FAIL ===`);
  process.exit(fail === 0 ? 0 : 1);
})();
