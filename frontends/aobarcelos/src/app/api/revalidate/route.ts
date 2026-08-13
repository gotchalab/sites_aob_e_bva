import { NextResponse } from "next/server";
import { revalidatePath } from "next/cache";

export const dynamic = "force-dynamic";

/**
 * Webhook de revalidação chamado pelo AOB.Api quando o admin publica/edita conteúdo.
 * Body irrelevante — path e secret vêm em query string.
 *
 * Exemplo: POST /api/revalidate?path=/artigos/anilhas-2026&secret=...
 */
export async function POST(request: Request) {
  const url = new URL(request.url);
  const path = url.searchParams.get("path") ?? "/";
  const secret = url.searchParams.get("secret");

  const expected = process.env.REVALIDATE_SECRET;
  if (expected && secret !== expected) {
    return NextResponse.json({ ok: false, error: "Invalid secret" }, { status: 401 });
  }

  try {
    revalidatePath(path);
    return NextResponse.json({ ok: true, revalidated: path });
  } catch (err) {
    return NextResponse.json(
      { ok: false, error: err instanceof Error ? err.message : "revalidate failed" },
      { status: 500 },
    );
  }
}
