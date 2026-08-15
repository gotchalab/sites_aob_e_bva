// Executado automaticamente após "next build" via "postbuild" no package.json
// Corrige o chunk path do webpack-runtime (bug de builds Windows sem Developer Mode)
import { existsSync, readFileSync, writeFileSync } from "fs";
import { resolve } from "path";

const runtimePath = resolve(".next/server/webpack-runtime.js");

if (!existsSync(runtimePath)) {
  console.log("patch-build: webpack-runtime.js não encontrado, a saltar");
  process.exit(0);
}

let content = readFileSync(runtimePath, "utf8");
if (content.includes('"" + chunkId + ".js"')) {
  content = content.replaceAll('"" + chunkId + ".js"', '"chunks/" + chunkId + ".js"');
  writeFileSync(runtimePath, content, "utf8");
  console.log('patch-build: corrigido chunk path em webpack-runtime.js');
} else {
  console.log("patch-build: chunk path OK, sem patches necessários");
}
