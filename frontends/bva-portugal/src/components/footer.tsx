import Link from "next/link";
import { api } from "@/lib/api";

export async function Footer() {
  const site = await api.site();
  return (
    <footer className="mt-20 bg-ink-900 text-sand-100">
      <div className="mx-auto grid max-w-6xl gap-8 px-4 py-14 md:grid-cols-4">
        <div className="md:col-span-2">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src="/logo.png" alt={site.name} className="mb-4 h-14 w-auto brightness-110" />
          <div className="font-display text-lg font-semibold text-white">{site.name}</div>
          {site.description && (
            <p className="mt-2 max-w-md text-sm leading-relaxed text-sand-100/70">{site.description}</p>
          )}
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm uppercase tracking-widest text-white/90">
            Navegação
          </h3>
          <ul className="space-y-2 text-sm">
            <li><Link href="/" className="text-sand-100/75 transition hover:text-brand-400">Início</Link></li>
            <li><Link href="/quem-somos" className="text-sand-100/75 transition hover:text-brand-400">Quem somos</Link></li>
            <li><Link href="/artigos" className="text-sand-100/75 transition hover:text-brand-400">Artigos</Link></li>
            <li><Link href="/downloads" className="text-sand-100/75 transition hover:text-brand-400">Downloads</Link></li>
            <li><Link href="/contacto" className="text-sand-100/75 transition hover:text-brand-400">Contacto</Link></li>
          </ul>
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm uppercase tracking-widest text-white/90">
            Sócios
          </h3>
          <ul className="space-y-2 text-sm">
            <li><Link href="/inscricao-socio" className="text-sand-100/75 transition hover:text-brand-400">Quero ser sócio</Link></li>
            <li><Link href="/socio" className="text-sand-100/75 transition hover:text-brand-400">Área reservada</Link></li>
            {site.contactEmail && (
              <li>
                <a href={`mailto:${site.contactEmail}`} className="text-sand-100/75 transition hover:text-brand-400">
                  {site.contactEmail}
                </a>
              </li>
            )}
          </ul>
        </div>
      </div>

      <div className="border-t border-ink-700/60">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-4 text-xs text-sand-100/50 md:flex-row">
          <div suppressHydrationWarning>© {new Date().getFullYear()} {site.name}. Todos os direitos reservados.</div>
          <div>{site.domain}</div>
        </div>
      </div>
    </footer>
  );
}
