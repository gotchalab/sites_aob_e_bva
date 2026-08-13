import { api } from "@/lib/api";

/**
 * Server Component isolado que gera o JSON-LD Organization no <head>.
 * Fica encapsulado para não obrigar o RootLayout a ser async — o que
 * podia causar problemas de hydration com o resto da árvore.
 */
export async function OrgJsonLd() {
  const site = await api.site().catch(() => null);
  const url = site?.domain ? `https://${site.domain}` : "https://aobarcelos.pt";

  const orgJsonLd = {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: site?.name ?? "Associação Ornitológica de Barcelos",
    url,
    logo: site?.logoUrl ? new URL(site.logoUrl, url).toString() : `${url}/icon.png`,
    email: site?.contactEmail ?? undefined,
    description: site?.tagline ?? site?.description ?? undefined,
    address: {
      "@type": "PostalAddress",
      addressLocality: "Barcelos",
      addressCountry: "PT",
    },
  };

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(orgJsonLd) }}
    />
  );
}
