import type { ArticleDetail, Site } from "./api-types";

export function articleJsonLd(article: ArticleDetail, site: Site | null) {
  const baseUrl = site?.domain ? `https://${site.domain}` : "";
  const canonical = `${baseUrl}/artigos/${article.slug}`;
  const image = article.coverImagePath
    ? (article.coverImagePath.startsWith("http")
      ? article.coverImagePath
      : `${baseUrl}${article.coverImagePath}`)
    : undefined;

  return {
    "@context": "https://schema.org",
    "@type": "Article",
    mainEntityOfPage: { "@type": "WebPage", "@id": canonical },
    headline: article.title,
    description: article.metaDescription ?? article.excerpt ?? undefined,
    image: image ? [image] : undefined,
    datePublished: article.publishedAt ?? undefined,
    dateModified: article.publishedAt ?? undefined,
    author: { "@type": "Organization", name: site?.name ?? "BVA Portugal" },
    publisher: {
      "@type": "Organization",
      name: site?.name ?? "BVA Portugal",
      logo: {
        "@type": "ImageObject",
        url: `${baseUrl}/icon.png`,
      },
    },
    articleSection: article.categoryName,
    inLanguage: "pt-PT",
  };
}

export function breadcrumbJsonLd(
  items: Array<{ name: string; url: string }>,
  site: Site | null,
) {
  const baseUrl = site?.domain ? `https://${site.domain}` : "";
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: items.map((it, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: it.name,
      item: `${baseUrl}${it.url}`,
    })),
  };
}
