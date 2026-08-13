import type { Metadata } from "next";
import { Inter, Playfair_Display } from "next/font/google";
import "./globals.css";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { OrgJsonLd } from "@/components/org-json-ld";
import { api } from "@/lib/api";
import { API_URL } from "@/lib/config";

const inter = Inter({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  display: "swap",
  variable: "--font-inter",
});

const playfair = Playfair_Display({
  subsets: ["latin"],
  weight: ["600", "700"],
  display: "swap",
  variable: "--font-playfair",
});

export async function generateMetadata(): Promise<Metadata> {
  const site = await api.site().catch(() => null);
  const name = site?.name ?? "AOB";
  const description = site?.tagline ?? site?.description ?? "";
  const url = site?.domain ? `https://${site.domain}` : undefined;
  return {
    metadataBase: url ? new URL(url) : undefined,
    title: { default: name, template: `%s · ${name}` },
    description,
    alternates: {
      canonical: "/",
      types: { "application/rss+xml": "/rss.xml" },
    },
    openGraph: {
      type: "website",
      locale: "pt_PT",
      siteName: name,
      title: name,
      description,
      url,
    },
    twitter: { card: "summary_large_image" },
    robots: { index: true, follow: true },
  };
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-PT" className={`${inter.variable} ${playfair.variable}`}>
      <head>
        <link rel="preconnect" href={API_URL} crossOrigin="anonymous" />
        <OrgJsonLd />
      </head>
      <body className="flex min-h-screen flex-col overflow-x-clip bg-cream-50 text-earth-900 antialiased">
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-full focus:bg-brand-500 focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-white focus:shadow-lg"
        >
          Saltar para o conteúdo
        </a>
        <Header />
        <main id="main" className="flex-1">
          {children}
        </main>
        <Footer />
      </body>
    </html>
  );
}
