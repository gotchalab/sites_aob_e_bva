import { api } from "@/lib/api";

/**
 * Server Component isolado que gera o JSON-LD Organization no <head>.
 * Fica encapsulado para não obrigar o RootLayout a ser async.
 */
export async function OrgJsonLd() {
  const site = await api.site().catch(() => null);
  const url = site?.domain ? `https://${site.domain}` : "https://bva-p.aobarcelos.pt";

  const orgJsonLd = {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: site?.name ?? "BVA Portugal",
    url,
    logo: site?.logoUrl ? new URL(site.logoUrl, url).toString() : `${url}/icon.png`,
    email: site?.contactEmail ?? undefined,
    description: site?.tagline ?? site?.description ?? undefined,
    address: { "@type": "PostalAddress", addressCountry: "PT" },
  };

  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: JSON.stringify(orgJsonLd) }}
    />
  );
}
