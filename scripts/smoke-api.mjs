const API = 'http://localhost:5000';
let pass = 0, fail = 0;

function check(name, cond, extra = '') {
  if (cond) { console.log(`[PASS] ${name} ${extra}`); pass++; }
  else      { console.log(`[FAIL] ${name} ${extra}`); fail++; }
}

async function req(path, opts = {}) {
  const res = await fetch(`${API}${path}`, opts);
  const text = await res.text();
  let body = null;
  try { body = text ? JSON.parse(text) : null; } catch {}
  return { status: res.status, body, text };
}

async function testSocio(site, email, nsoc) {
  console.log(`\n=== ${site.toUpperCase()} — ${email} ===`);

  const login = await req('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'TesteSocio123!' }),
  });
  check('login status 200', login.status === 200, `(${login.status})`);
  const t = login.body ?? {};
  check('login accessToken length > 100', (t.accessToken?.length ?? 0) > 100);
  check('login refreshToken length > 20', (t.refreshToken?.length ?? 0) > 20);
  check('login roles inclui Socio', t.roles?.includes('Socio'));
  check('login socioId nao null', t.socioId != null);

  const bearer = (tk) => ({ Authorization: `Bearer ${tk}` });

  const me = await req('/api/me', { headers: bearer(t.accessToken) });
  check('/me numeroSocio', me.body?.numeroSocio === nsoc, `(got ${me.body?.numeroSocio})`);
  check('/me email', me.body?.email === email);

  const quotas = await req('/api/me/quotas', { headers: bearer(t.accessToken) });
  check('/me/quotas count=3', quotas.body?.length === 3, `(got ${quotas.body?.length})`);
  const pagas = quotas.body?.filter(q => q.dataPagamento).length;
  check('/me/quotas 2 pagas', pagas === 2, `(got ${pagas})`);

  const anilhasAntes = await req('/api/me/anilhas', { headers: bearer(t.accessToken) });
  const cntAntes = anilhasAntes.body?.length ?? 0;

  const putBody = {
    nomeCompleto: me.body.nomeCompleto,
    telefone: '911222333',
    nif: me.body.nif,
    dataNascimento: me.body.dataNascimento,
    morada: 'Rua Smoke Test, 42',
    codigoPostal: '4750-999',
    localidade: 'Barcelos',
    especiesInteresse: ['Agapornis roseicollis', 'Serinus canaria'],
  };
  const put = await req('/api/me', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...bearer(t.accessToken) },
    body: JSON.stringify(putBody),
  });
  check('PUT /me status 2xx', put.status >= 200 && put.status < 300, `(${put.status})`);
  const me2 = await req('/api/me', { headers: bearer(t.accessToken) });
  check('PUT /me telefone persistiu', me2.body?.telefone === '911222333');
  check('PUT /me morada persistiu', me2.body?.morada === 'Rua Smoke Test, 42');
  check('PUT /me especies=2', me2.body?.especiesInteresse?.length === 2);

  const pedidoBody = {
    especieCientifica: 'Agapornis roseicollis',
    especieNomeComum: 'Roseicollis',
    ano: 2026,
    diametro: 4.5,
    quantidade: 10,
    observacoes: `Smoke test ${site}`,
  };
  const novoPedido = await req('/api/me/pedidos-anilhas', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...bearer(t.accessToken) },
    body: JSON.stringify(pedidoBody),
  });
  check('POST pedido status 2xx', novoPedido.status >= 200 && novoPedido.status < 300, `(${novoPedido.status})`);
  const pedidoId = novoPedido.body?.id;
  check('POST pedido id > 0', pedidoId > 0, `(id=${pedidoId})`);
  check('POST pedido estado=Pendente', novoPedido.body?.estado === 'Pendente', `(${novoPedido.body?.estado})`);

  const anilhas = await req('/api/me/anilhas', { headers: bearer(t.accessToken) });
  check(`/me/anilhas +1 (${cntAntes} -> ${anilhas.body?.length})`, anilhas.body?.length === cntAntes + 1);
  check('/me/anilhas contem id novo', anilhas.body?.some(a => a.id === pedidoId));

  const ref = await req('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId: t.userId, refreshToken: t.refreshToken }),
  });
  check('refresh 200', ref.status === 200, `(${ref.status})`);
  check('refresh: novo accessToken', ref.body?.accessToken !== t.accessToken);
  check('refresh: rotation (novo refreshToken)', ref.body?.refreshToken !== t.refreshToken);

  const me3 = await req('/api/me', { headers: bearer(ref.body.accessToken) });
  check('novo access acede /api/me', me3.body?.numeroSocio === nsoc);

  const ret = await req('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId: t.userId, refreshToken: t.refreshToken }),
  });
  check('refresh antigo rejeitado', ret.status === 401, `(${ret.status})`);

  const logout = await req('/api/auth/logout', {
    method: 'POST',
    headers: bearer(ref.body.accessToken),
  });
  check('logout status 204', logout.status === 204, `(${logout.status})`);

  const afterLogout = await req('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userId: t.userId, refreshToken: ref.body.refreshToken }),
  });
  check('refresh apos logout rejeitado', afterLogout.status === 401, `(${afterLogout.status})`);

  return { pedidoId, userId: t.userId };
}

(async () => {
  await testSocio('aob', 'teste.socio@example.pt', 'S9999');
  await testSocio('bva', 'teste.socio.bva@example.pt', 'B9999');
  console.log(`\n=== Resumo: ${pass} PASS / ${fail} FAIL ===`);
  process.exit(fail === 0 ? 0 : 1);
})();
