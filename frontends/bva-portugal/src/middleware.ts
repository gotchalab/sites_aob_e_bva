import { NextResponse, type NextRequest } from "next/server";

const COOKIE_ACCESS = "bva_socio_at";
const COOKIE_REFRESH = "bva_socio_rt";
const COOKIE_USER_ID = "bva_socio_uid";
const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
const ACCESS_COOKIE_MARGIN_MS = 30_000;

const BYPASS_PATHS = ["/socio/login", "/socio/api/login", "/socio/api/logout"];

export async function middleware(req: NextRequest) {
  const path = req.nextUrl.pathname;
  if (!path.startsWith("/socio") || BYPASS_PATHS.some((p) => path === p || path.startsWith(p + "/"))) {
    return NextResponse.next();
  }

  const isApi = path.startsWith("/socio/api/");
  const access = req.cookies.get(COOKIE_ACCESS)?.value;
  const refresh = req.cookies.get(COOKIE_REFRESH)?.value;
  const userId = req.cookies.get(COOKIE_USER_ID)?.value;

  if (!refresh || !userId) {
    return unauthorized(req, path, isApi);
  }

  if (access) {
    return NextResponse.next();
  }

  try {
    const res = await fetch(`${API_URL}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ userId, refreshToken: refresh }),
      cache: "no-store",
    });
    if (!res.ok) {
      return clearAndFail(req, path, isApi);
    }
    const data = (await res.json()) as {
      accessToken: string;
      refreshToken: string;
      expiresAt: string;
    };

    req.cookies.set(COOKIE_ACCESS, data.accessToken);
    req.cookies.set(COOKIE_REFRESH, data.refreshToken);

    const response = NextResponse.next({ request: req });
    const isProd = process.env.NODE_ENV === "production";
    const common = { httpOnly: true, secure: isProd, sameSite: "lax" as const, path: "/" };
    const jwtExpires = new Date(data.expiresAt).getTime();
    const accessExpires = new Date(jwtExpires - ACCESS_COOKIE_MARGIN_MS);
    response.cookies.set(COOKIE_ACCESS, data.accessToken, { ...common, expires: accessExpires });
    response.cookies.set(COOKIE_REFRESH, data.refreshToken, { ...common, maxAge: 60 * 60 * 24 * 30 });
    response.cookies.set(COOKIE_USER_ID, userId, { ...common, maxAge: 60 * 60 * 24 * 30 });
    return response;
  } catch {
    return clearAndFail(req, path, isApi);
  }
}

function unauthorized(req: NextRequest, path: string, isApi: boolean) {
  if (isApi) {
    return NextResponse.json({ ok: false, error: "Sessão expirada" }, { status: 401 });
  }
  // Behind nginx: `next start -H 127.0.0.1` makes req.url reconstruct with the
  // internal host, so absolute redirects would leak "localhost:3001" to browsers.
  // Prefer X-Forwarded-* to build the public URL.
  const proto = req.headers.get("x-forwarded-proto") ?? req.nextUrl.protocol.replace(":", "");
  const host = req.headers.get("x-forwarded-host") ?? req.headers.get("host") ?? req.nextUrl.host;
  const url = new URL("/socio/login", `${proto}://${host}`);
  url.searchParams.set("redirect", path);
  return NextResponse.redirect(url);
}

function clearAndFail(req: NextRequest, path: string, isApi: boolean) {
  const res = unauthorized(req, path, isApi);
  res.cookies.delete(COOKIE_ACCESS);
  res.cookies.delete(COOKIE_REFRESH);
  res.cookies.delete(COOKIE_USER_ID);
  return res;
}

export const config = {
  matcher: ["/socio/:path*"],
};
