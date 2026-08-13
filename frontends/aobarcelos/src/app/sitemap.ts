import type { MetadataRoute } from "next";
import { api } from "@/lib/api";

/**
 * Sitemap dinâmico com todos os artigos + rotas estáticas.
 * `lastmod` de cada artigo vem do `publishedAt` real na BD.
 * Regenerado com o mesmo TTL da homepage (60s).
 */
export const revalidate = 60;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const site = await api.site().catch(() => null);
  const base = site?.domain ? `https://${site.domain}` : "https://aobarcelos.pt";
  const now = new Date();

  const staticRoutes: MetadataRoute.Sitemap = [
    { url: `${base}/`, lastModified: now, changeFrequency: "daily", priority: 1 },
    { url: `${base}/quem-somos`, lastModified: now, changeFrequency: "monthly", priority: 0.8 },
    { url: `${base}/artigos`, lastModified: now, changeFrequency: "daily", priority: 0.9 },
    { url: `${base}/downloads`, lastModified: now, changeFrequency: "weekly", priority: 0.7 },
    { url: `${base}/contacto`, lastModified: now, changeFrequency: "monthly", priority: 0.5 },
    { url: `${base}/inscricao-socio`, lastModified: now, changeFrequency: "monthly", priority: 0.6 },
  ];

  // Todos os artigos publicados. Um único pedido de páginação grande (a BD tem ~125 artigos).
  let articles: MetadataRoute.Sitemap = [];
  try {
    const list = await api.articles({ pageSize: 500 });
    articles = list.items.map((a) => ({
      url: `${base}/artigos/${a.slug}`,
      lastModified: a.publishedAt ? new Date(a.publishedAt) : now,
      changeFrequency: "weekly" as const,
      priority: a.isFeatured ? 0.8 : 0.6,
    }));
  } catch {
    // se a API estiver em baixo, servimos apenas as rotas estáticas
  }

  return [...staticRoutes, ...articles];
}
