import { api } from "@/lib/api";
import { ContactForm } from "./contact-form";

export const revalidate = 3600;

export const metadata = { title: "Contacto" };

export default async function ContactoPage() {
  const site = await api.site();
  return (
    <>
      <section className="hero-pattern border-b border-sand-300">
        <div className="mx-auto max-w-5xl px-4 py-12 md:py-16">
          <div className="mb-3 inline-block rounded-full border border-brand-500/40 bg-white/60 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700">
            Fale connosco
          </div>
          <h1 className="font-display text-4xl font-bold leading-tight text-ink-900 md:text-5xl">
            Contacto
          </h1>
          <p className="mt-4 max-w-2xl text-lg leading-relaxed text-ink-700">
            Envia-nos uma mensagem — respondemos com a maior brevidade possível.
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-5xl px-4 py-12 md:py-16">
        <div className="grid gap-8 md:grid-cols-[1fr_2fr]">
          <aside className="space-y-6">
            {site.contactEmail && (
              <div className="rounded-2xl border border-sand-300 bg-white/70 p-6 shadow-sm">
                <div className="flex items-center gap-3">
                  <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-500/10 text-brand-600">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
                      <rect x="3" y="5" width="18" height="14" rx="2" />
                      <path d="m3 7 9 6 9-6" />
                    </svg>
                  </span>
                  <div>
                    <div className="text-[11px] font-medium uppercase tracking-widest text-ink-500">
                      Email direto
                    </div>
                    <a
                      className="font-medium text-ink-900 hover:text-brand-600"
                      href={`mailto:${site.contactEmail}`}
                    >
                      {site.contactEmail}
                    </a>
                  </div>
                </div>
              </div>
            )}

            <div className="rounded-2xl border border-ink-900/10 bg-ink-900 p-6 text-white shadow-sm">
              <div className="flex items-center gap-3">
                <span className="flex h-10 w-10 items-center justify-center rounded-full bg-accent-400/15 text-accent-400">
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-5 w-5">
                    <circle cx="12" cy="12" r="10" />
                    <path d="M12 6v6l4 2" />
                  </svg>
                </span>
                <h2 className="font-display text-lg font-semibold text-accent-400">
                  Contactos técnicos
                </h2>
              </div>
              <dl className="mt-4 space-y-2 text-sm">
                <div className="flex items-baseline justify-between gap-4 border-b border-white/10 pb-2">
                  <dt className="text-white/70">Inscrições / anilhas</dt>
                  <dd className="font-medium text-white">Via formulário</dd>
                </div>
                <div className="flex items-baseline justify-between gap-4">
                  <dt className="text-white/70">Assuntos técnicos</dt>
                  <dd className="font-medium text-accent-400">Direcção BVA</dd>
                </div>
              </dl>
              <p className="mt-4 text-xs leading-relaxed text-white/60">
                Para assuntos relacionados com sócios ou anilhas, indica o teu
                número de sócio ou STAM na mensagem.
              </p>
            </div>
          </aside>

          <div className="rounded-2xl border border-sand-300 bg-white p-6 shadow-lg md:p-8">
            <ContactForm />
          </div>
        </div>
      </div>
    </>
  );
}
