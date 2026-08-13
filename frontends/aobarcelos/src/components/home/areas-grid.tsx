import type { HomeConfig } from "@/lib/api-types";
import { AreaIcon } from "./icon-map";

export function AreasGrid({ home }: { home: HomeConfig }) {
  const areas = home.areas ?? [];
  if (areas.length === 0) return null;

  return (
    <section className="border-b border-gold-500/20 bg-cream-100/40">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-12 md:text-center">
          <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
            Áreas de atuação
          </div>
          <h2 className="font-display text-3xl font-semibold leading-tight text-earth-900 md:text-4xl">
            O que fazemos pela ornitologia.
          </h2>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 md:grid-cols-4">
          {areas.map((a) => (
            <div
              key={a.title + a.href}
              className="flex h-full flex-col rounded-2xl border border-black/5 bg-white p-6 shadow-sm"
            >
              <div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600">
                <AreaIcon name={a.icon} className="h-5 w-5" />
              </div>
              <h3 className="font-display text-lg font-semibold leading-tight text-earth-900">
                {a.title}
              </h3>
              {a.description && (
                <p className="mt-2 text-sm leading-relaxed text-earth-700/80">{a.description}</p>
              )}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
