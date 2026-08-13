import Link from "next/link";
import { api } from "@/lib/api";
import { Nav } from "./nav";

export async function Header() {
  const [site, menu] = await Promise.all([api.site(), api.menu().catch(() => [])]);
  return (
    <header className="sticky top-0 z-40 border-b border-gold-500/20 bg-cream-100/80 backdrop-blur supports-[backdrop-filter]:bg-cream-100/70">
      <div className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 md:h-20 md:gap-6">
        <Link href="/" className="flex shrink-0 items-center gap-3" aria-label={site.name}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src="/logo.png"
            alt={site.name}
            className="h-10 w-auto md:h-14"
          />
        </Link>

        <div className="flex min-w-0 flex-1 items-center justify-end">
          <Nav items={menu} />
        </div>

        <div className="hidden shrink-0 items-center gap-1 lg:flex">
          <Link
            href="/contacto"
            aria-label="Contactar"
            title="Contactar"
            className="flex h-10 w-10 items-center justify-center rounded-full text-earth-800 transition hover:bg-cream-300/60 hover:text-brand-700"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
              <rect x="3" y="5" width="18" height="14" rx="2" />
              <path d="m3 7 9 6 9-6" />
            </svg>
          </Link>
          <Link
            href="/inscricao-socio"
            className="ml-1 inline-flex items-center gap-2 rounded-full border border-brand-500/50 bg-white px-4 py-2 text-sm font-medium text-brand-700 shadow-sm transition hover:bg-brand-500/10"
          >
            Quero ser sócio
          </Link>
          <Link
            href="/socio"
            className="ml-1 inline-flex items-center gap-2 rounded-full bg-brand-500 px-5 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
              <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
              <circle cx="12" cy="7" r="4" />
            </svg>
            Área de sócio
          </Link>
        </div>
      </div>
    </header>
  );
}
