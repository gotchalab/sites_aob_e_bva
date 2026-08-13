import Link from "next/link";
import { api } from "@/lib/api";

export async function Footer() {
  const site = await api.site();
  return (
    <footer className="mt-20 bg-earth-900 text-gold-400">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-12 md:grid-cols-3">
        <div>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/logo.png" alt={site.name} className="mb-4 h-14 w-auto brightness-110" />
          <div className="font-display text-lg font-semibold text-gold-400">{site.name}</div>
          {(site.tagline ?? site.description) && (
            <p className="mt-2 text-sm leading-relaxed text-gold-400/70">{site.tagline ?? site.description}</p>
          )}
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm uppercase tracking-widest text-white/90">
            Navegação
          </h3>
          <ul className="space-y-2 text-sm">
            <li><Link href="/" className="text-gold-400/80 transition hover:text-brand-400">Início</Link></li>
            <li><Link href="/quem-somos" className="text-gold-400/80 transition hover:text-brand-400">Quem somos</Link></li>
            <li><Link href="/artigos" className="text-gold-400/80 transition hover:text-brand-400">Artigos</Link></li>
            <li><Link href="/downloads" className="text-gold-400/80 transition hover:text-brand-400">Downloads</Link></li>
            <li><Link href="/contacto" className="text-gold-400/80 transition hover:text-brand-400">Contacto</Link></li>
            <li><Link href="/inscricao-socio" className="text-gold-400/80 transition hover:text-brand-400">Quero ser sócio</Link></li>
            <li><Link href="/socio" className="text-gold-400/80 transition hover:text-brand-400">Área de sócio</Link></li>
          </ul>
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm uppercase tracking-widest text-white/90">
            Contacto
          </h3>
          <ul className="space-y-2 text-sm text-gold-400/80">
            {site.contactEmail && (
              <li>
                <a href={`mailto:${site.contactEmail}`} className="transition hover:text-brand-400">
                  {site.contactEmail}
                </a>
              </li>
            )}
            <li>{site.domain}</li>
          </ul>
        </div>
      </div>

      <div className="border-t border-earth-700/60 bg-earth-900">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-4 text-xs text-gold-400/60 md:flex-row">
          <div>© {new Date().getFullYear()} {site.name}. Todos os direitos reservados.</div>
          <div>{site.domain}</div>
        </div>
      </div>
    </footer>
  );
}
