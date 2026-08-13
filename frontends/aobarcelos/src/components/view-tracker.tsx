"use client";

import { useEffect } from "react";
import { API_URL, SITE_SLUG } from "@/lib/config";

export function ViewTracker({ slug }: { slug: string }) {
  useEffect(() => {
    const key = `aob:view:${SITE_SLUG}:${slug}`;
    try {
      if (sessionStorage.getItem(key)) return;
      sessionStorage.setItem(key, "1");
    } catch {
      // ignore storage errors (private mode, quota)
    }
    fetch(`${API_URL}/api/sites/${SITE_SLUG}/articles/${encodeURIComponent(slug)}/view`, {
      method: "POST",
      credentials: "omit",
      keepalive: true,
    }).catch(() => {});
  }, [slug]);
  return null;
}
