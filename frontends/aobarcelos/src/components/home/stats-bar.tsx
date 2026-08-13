import type { HomeConfig } from "@/lib/api-types";

export function StatsBar({ home }: { home: HomeConfig }) {
  const items = [
    home.foundedYear ? { label: "Fundada em", value: String(home.foundedYear) } : null,
    home.memberCount ? { label: "Sócios ativos", value: home.memberCount.toLocaleString("pt-PT") } : null,
    home.ringsPerYear ? { label: "Anilhas / ano", value: home.ringsPerYear.toLocaleString("pt-PT") } : null,
    home.speciesCount ? { label: "Espécies", value: String(home.speciesCount) } : null,
  ].filter((x): x is { label: string; value: string } => x !== null);

  if (items.length === 0) return null;

  return (
    <section className="border-b border-gold-500/20 bg-cream-100/60">
      <div className="mx-auto max-w-6xl px-4 py-10 md:py-12">
        <div className="grid gap-6 sm:grid-cols-2 md:grid-cols-4">
          {items.map((it) => (
            <div key={it.label} className="text-center md:text-left">
              <div className="font-display text-4xl font-bold leading-none text-brand-600 md:text-5xl">
                {it.value}
              </div>
              <div className="mt-2 text-xs uppercase tracking-widest text-earth-700/70">
                {it.label}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
