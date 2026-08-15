import Link from "next/link";
import { api } from "@/lib/api";
import { Nav } from "./nav";

export async function Header() {
  const [site, menu] = await Promise.all([api.site(), api.menu().catch(() => [])]);
  return (
    <header className="sticky top-0 z-40 border-b border-gold-500/20 bg-cream-100">
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
        </div>
      </div>
    </header>
  );
}
