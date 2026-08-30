// Executado automaticamente apos "next build" via "postbuild" no package.json
// Faz tres coisas:
//   1. Corrige o chunk path do webpack-runtime (bug de builds Windows sem Developer Mode)
//   2. Injecta dataRoutes/staticRoutes/dynamicRoutes vazios em routes-manifest.json
//      quando missing. Next 15.5 no Windows omite estas chaves em builds so-App-Router
//      e o `next start` crasha com "routesManifest.dataRoutes is not iterable".
//   3. Valida que os NEXT_PUBLIC_* de .env.production ficaram embutidos no build.
//      Env vars passadas pela shell sobrescrevem o .env.production silenciosamente
//      (foi assim que uma vez fomos para producao com o Turnstile testing sitekey em
//      vez do real). Se algum valor esperado nao aparecer, o build falha e o deploy
//      aborta (deploy.sh tem set -euo pipefail).
import { existsSync, readFileSync, writeFileSync, readdirSync, statSync } from "fs";
import { resolve, join } from "path";

const runtimePath = resolve(".next/server/webpack-runtime.js");
if (existsSync(runtimePath)) {
  let content = readFileSync(runtimePath, "utf8");
  if (content.includes('"" + chunkId + ".js"')) {
    content = content.replaceAll('"" + chunkId + ".js"', '"chunks/" + chunkId + ".js"');
    writeFileSync(runtimePath, content, "utf8");
    console.log("patch-build: corrigido chunk path em webpack-runtime.js");
  } else {
    console.log("patch-build: chunk path OK, sem patches necessarios");
  }
} else {
  console.log("patch-build: webpack-runtime.js nao encontrado, a saltar patch");
}

const routesManifestPath = resolve(".next/routes-manifest.json");
if (existsSync(routesManifestPath)) {
  const manifest = JSON.parse(readFileSync(routesManifestPath, "utf8"));
  const missing = [];
  for (const key of ["dataRoutes", "staticRoutes", "dynamicRoutes"]) {
    if (!(key in manifest)) { manifest[key] = []; missing.push(key); }
  }
  if (missing.length > 0) {
    writeFileSync(routesManifestPath, JSON.stringify(manifest), "utf8");
    console.log(`patch-build: injectadas chaves vazias em routes-manifest.json: ${missing.join(", ")}`);
  } else {
    console.log("patch-build: routes-manifest.json OK");
  }
} else {
  console.log("patch-build: routes-manifest.json nao encontrado, a saltar patch");
}

const envPath = resolve(".env.production");
if (!existsSync(envPath)) {
  console.log("patch-build: .env.production nao encontrado, a saltar verificacao NEXT_PUBLIC_*");
  process.exit(0);
}

const expected = {};
for (const raw of readFileSync(envPath, "utf8").split(/\r?\n/)) {
  const line = raw.trim();
  if (!line || line.startsWith("#")) continue;
  const m = line.match(/^(NEXT_PUBLIC_\w+)=(.*)$/);
  if (!m) continue;
  const value = m[2].replace(/^["']|["']$/g, "").trim();
  if (value) expected[m[1]] = value;
}

if (Object.keys(expected).length === 0) {
  console.log("patch-build: sem NEXT_PUBLIC_* em .env.production, a saltar verificacao");
  process.exit(0);
}

function collectJs(dir) {
  const out = [];
  if (!existsSync(dir)) return out;
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) out.push(...collectJs(full));
    else if (name.endsWith(".js")) out.push(full);
  }
  return out;
}

const jsFiles = [
  ...collectJs(resolve(".next/static/chunks")),
  ...collectJs(resolve(".next/server")),
];

let allJs = "";
for (const f of jsFiles) {
  try { allJs += readFileSync(f, "utf8") + "\n"; } catch {}
}

const missing = [];
for (const [key, value] of Object.entries(expected)) {
  if (!allJs.includes(value)) missing.push(`${key}=${value}`);
}

if (missing.length > 0) {
  console.error("");
  console.error("patch-build: ERRO — as seguintes NEXT_PUBLIC_* de .env.production");
  console.error("             NAO ficaram embutidas no build:");
  for (const m of missing) console.error(`  - ${m}`);
  console.error("");
  console.error("Causa provavel: env vars passadas pela shell sobrescreveram o .env.production.");
  console.error("Solucao: correr 'next build' SEM NEXT_PUBLIC_* na shell (deixar o Next.js");
  console.error("         ler do .env.production).");
  console.error("");
  process.exit(1);
}

console.log(`patch-build: verificados ${Object.keys(expected).length} NEXT_PUBLIC_* embutidos no build`);
