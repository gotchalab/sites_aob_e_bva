import { cookies } from "next/headers";
import { API_URL } from "./config";

const COOKIE_ACCESS = "bva_socio_at";
const COOKIE_REFRESH = "bva_socio_rt";
const COOKIE_USER_ID = "bva_socio_uid";

export async function readSession() {
  const jar = await cookies();
  const access = jar.get(COOKIE_ACCESS)?.value;
  const refresh = jar.get(COOKIE_REFRESH)?.value;
  const userId = jar.get(COOKIE_USER_ID)?.value;
  return { access, refresh, userId };
}

export async function apiGet<T>(path: string): Promise<T | null> {
  const { access } = await readSession();
  if (!access) return null;
  const res = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${access}`, Accept: "application/json" },
    cache: "no-store",
  });
  if (res.status === 401 || res.status === 403) return null;
  if (!res.ok) throw new Error(`API ${res.status}: ${path}`);
  return res.json() as Promise<T>;
}

export async function apiSend<T>(path: string, method: "POST" | "PUT" | "DELETE", body?: unknown): Promise<T | null> {
  const { access } = await readSession();
  if (!access) return null;
  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${access}`,
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: body ? JSON.stringify(body) : undefined,
    cache: "no-store",
  });
  if (res.status === 401 || res.status === 403) return null;
  if (!res.ok) {
    const err = await res.text().catch(() => "");
    throw new Error(`API ${res.status}: ${err}`);
  }
  return res.json().catch(() => ({} as T)) as Promise<T>;
}

export const cookieNames = {
  access: COOKIE_ACCESS,
  refresh: COOKIE_REFRESH,
  userId: COOKIE_USER_ID,
};
