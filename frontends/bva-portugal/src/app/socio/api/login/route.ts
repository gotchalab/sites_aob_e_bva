import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { API_URL } from "@/lib/config";
import { cookieNames } from "@/lib/socio-auth";

export async function POST(req: Request) {
  const { email, password } = await req.json().catch(() => ({ email: "", password: "" }));
  if (!email || !password) {
    return NextResponse.json({ ok: false, error: "Credenciais em falta" }, { status: 400 });
  }
  const res = await fetch(`${API_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
    cache: "no-store",
  });
  if (!res.ok) {
    return NextResponse.json({ ok: false, error: "Credenciais invalidas" }, { status: 401 });
  }
  const data = (await res.json()) as {
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
    userId: string;
    roles: string[];
    socioId: number | null;
  };
  if (!data.roles.includes("Socio") && !data.roles.includes("Admin")) {
    return NextResponse.json({ ok: false, error: "Sem permissoes de socio." }, { status: 403 });
  }
  const jar = await cookies();
  const isProd = process.env.NODE_ENV === "production";
  const common = { httpOnly: true, secure: isProd, sameSite: "lax" as const, path: "/" };
  const accessExpires = new Date(new Date(data.expiresAt).getTime() - 30_000);
  jar.set(cookieNames.access, data.accessToken, {
    ...common,
    expires: accessExpires,
  });
  jar.set(cookieNames.refresh, data.refreshToken, {
    ...common,
    maxAge: 60 * 60 * 24 * 30,
  });
  jar.set(cookieNames.userId, data.userId, { ...common, maxAge: 60 * 60 * 24 * 30 });
  return NextResponse.json({ ok: true, socioId: data.socioId });
}
