/**
 * build:prod — monta dist/ pronto a enviar para a VPS (sem npm install na VPS).
 *
 * Fluxo:
 *  1. next build  (produção, devtool:false)
 *  2. Cria dist/ com:
 *       dist/.next/         ← build output (sem cache/)
 *       dist/public/        ← ficheiros estáticos
 *       dist/node_modules/  ← react, react-dom, scheduler, lucide-react
 *         (copiados do node_modules local, sem precisar de npm install)
 *       dist/package.json   ← para referência
 *       dist/next.config.mjs
 *
 * Na VPS, `next start` usa o next global (/usr/lib/node_modules/next).
 * Os pacotes react/* precisam de estar em node_modules/ local porque
 * _document.js faz require('react/jsx-runtime') em runtime.
 */

import { execSync } from "child_process";
import { cpSync, existsSync, mkdirSync, readdirSync, realpathSync, rmSync, statSync } from "fs";
import { resolve } from "path";

const root = resolve(".");
const dist = resolve("dist");
const nm = resolve("node_modules");

// 1. Limpar dist/ anterior
if (existsSync(dist)) {
  console.log("→ A limpar dist/ anterior...");
  rmSync(dist, { recursive: true, force: true });
}

// 2. Build Next.js (inclui postbuild/patch-build.mjs)
console.log("→ npm run build...");
execSync("npm run build", { stdio: "inherit", cwd: root });

// 3. Montar estrutura de dist/
console.log("→ A montar dist/...");
mkdirSync(dist, { recursive: true });

// .next/ sem a cache interna
cpSync(resolve(".next"), resolve("dist/.next"), {
  recursive: true,
  verbatimSymlinks: false,
  filter: (src) => !/[\\/]\.next[\\/](cache|trace)/.test(src),
});

// public/
if (existsSync(resolve("public"))) {
  cpSync(resolve("public"), resolve("dist/public"), { recursive: true, verbatimSymlinks: false });
}

// configs
cpSync(resolve("package.json"), resolve("dist/package.json"));
cpSync(resolve("next.config.mjs"), resolve("dist/next.config.mjs"));

// 4. Copiar deps de runtime do node_modules local
//    (react, react-dom, scheduler, lucide-react)
//    A VPS tem next global; estes pacotes são os únicos necessários em runtime.
const runtimePkgs = ["react", "react-dom", "scheduler", "lucide-react"];
mkdirSync(resolve("dist/node_modules"), { recursive: true });

for (const pkg of runtimePkgs) {
  const src = resolve(`node_modules/${pkg}`);
  if (!existsSync(src)) {
    console.warn(`  AVISO: ${pkg} não encontrado em node_modules/`);
    continue;
  }
  // Resolve o symlink do pnpm para o path real antes de copiar
  const realSrc = realpathSync(src);
  const dest = resolve(`dist/node_modules/${pkg}`);
  cpSync(realSrc, dest, { recursive: true, verbatimSymlinks: false });
  console.log(`  ✓ ${pkg}`);
}

// 5. Sumário
function dirSizeMB(dir) {
  let total = 0;
  function walk(d) {
    try {
      for (const f of readdirSync(d, { withFileTypes: true })) {
        const full = `${d}/${f.name}`;
        if (f.isDirectory()) walk(full);
        else try { total += statSync(full).size; } catch {}
      }
    } catch {}
  }
  walk(dir);
  return (total / 1024 / 1024).toFixed(1);
}

console.log(`\n✓ dist/ pronto — ${dirSizeMB(dist)} MB total`);
console.log("  → para deploy: python infra/deploy/_redeploy_frontends.py");
