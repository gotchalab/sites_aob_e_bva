import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { cookieNames } from "@/lib/socio-auth";
import { API_URL } from "@/lib/config";

export async function POST() {
  const jar = await cookies();
  const access = jar.get(cookieNames.access)?.value;
  if (access) {
    fetch(`${API_URL}/api/auth/logout`, {
      method: "POST",
      headers: { Authorization: `Bearer ${access}` },
    }).catch(() => {});
  }
  jar.delete(cookieNames.access);
  jar.delete(cookieNames.refresh);
  jar.delete(cookieNames.userId);
  return NextResponse.json({ ok: true });
}
