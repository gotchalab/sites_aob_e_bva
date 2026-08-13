import { NextResponse } from "next/server";
import { apiSend } from "@/lib/socio-auth";

export async function POST(req: Request) {
  const body = await req.json();
  const res = await apiSend("/api/me/pedidos-anilhas", "POST", body);
  if (!res) return NextResponse.json({ ok: false, error: "Sessão expirada" }, { status: 401 });
  return NextResponse.json(res);
}
