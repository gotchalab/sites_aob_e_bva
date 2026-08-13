import type { MetadataRoute } from "next";
import { api } from "@/lib/api";

export const revalidate = 3600;

export default async function robots(): Promise<MetadataRoute.Robots> {
  const site = await api.site().catch(() => null);
  const base = site?.domain ? `https://${site.domain}` : "https://bva-p.aobarcelos.pt";
  return {
    rules: [
      { userAgent: "*", allow: "/", disallow: ["/socio/", "/api/"] },
    ],
    sitemap: `${base}/sitemap.xml`,
    host: base,
  };
}
