import { api } from "@/lib/api";

export const revalidate = 300;

function escapeXml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

export async function GET() {
  const [site, list] = await Promise.all([
    api.site().catch(() => null),
    api.articles({ pageSize: 30 }).catch(() => ({ items: [], total: 0, page: 1, pageSize: 30 })),
  ]);

  const base = site?.domain ? `https://${site.domain}` : "https://bva-p.aobarcelos.pt";
  const title = site?.name ?? "BVA Portugal";
  const description = site?.tagline ?? site?.description ?? "";
  const buildDate = new Date().toUTCString();

  const items = list.items
    .map((a) => {
      const url = `${base}/artigos/${a.slug}`;
      const pubDate = a.publishedAt ? new Date(a.publishedAt).toUTCString() : buildDate;
      const excerpt = a.excerpt ?? "";
      return `
    <item>
      <title>${escapeXml(a.title)}</title>
      <link>${url}</link>
      <guid isPermaLink="true">${url}</guid>
      <pubDate>${pubDate}</pubDate>
      <category>${escapeXml(a.categoryName)}</category>
      <description>${escapeXml(excerpt)}</description>
    </item>`;
    })
    .join("");

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">
  <channel>
    <title>${escapeXml(title)}</title>
    <link>${base}</link>
    <description>${escapeXml(description)}</description>
    <language>pt-PT</language>
    <lastBuildDate>${buildDate}</lastBuildDate>
    <atom:link href="${base}/rss.xml" rel="self" type="application/rss+xml"/>${items}
  </channel>
</rss>`;

  return new Response(xml, {
    headers: {
      "Content-Type": "application/rss+xml; charset=utf-8",
      "Cache-Control": "public, s-maxage=300, stale-while-revalidate=3600",
    },
  });
}
