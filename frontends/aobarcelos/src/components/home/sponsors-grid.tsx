import type { Sponsor } from "@/lib/api-types";

const TIER_LABELS: Record<Sponsor["tier"], string> = {
  Principal: "Patrocinadores principais",
  Institucional: "Apoios institucionais",
  Apoio: "Com o apoio de",
  Parceiro: "Parceiros",
};

const TIER_ORDER: Sponsor["tier"][] = ["Principal", "Institucional", "Apoio", "Parceiro"];

function groupByTier(sponsors: Sponsor[]) {
  const map = new Map<Sponsor["tier"], Sponsor[]>();
  for (const s of sponsors) {
    const arr = map.get(s.tier) ?? [];
    arr.push(s);
    map.set(s.tier, arr);
  }
  return TIER_ORDER
    .filter((t) => map.has(t))
    .map((t) => ({ tier: t, items: (map.get(t) ?? []).sort((a, b) => a.sortOrder - b.sortOrder) }));
}

function SponsorLogo({ sponsor }: { sponsor: Sponsor }) {
  const content = (
    <div className="group flex h-24 items-center justify-center rounded-xl border border-black/5 bg-white p-4 transition hover:border-brand-500/30 hover:shadow-md">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={sponsor.logoPath}
        alt={sponsor.name}
        loading="lazy"
        className="max-h-14 w-auto max-w-full object-contain grayscale opacity-70 transition duration-500 group-hover:grayscale-0 group-hover:opacity-100"
      />
    </div>
  );

  if (sponsor.clickUrl) {
    return (
      <a
        href={sponsor.clickUrl}
        target="_blank"
        rel="noopener noreferrer sponsored"
        title={sponsor.name}
        aria-label={sponsor.name}
      >
        {content}
      </a>
    );
  }
  return <div title={sponsor.name}>{content}</div>;
}

export function SponsorsGrid({ sponsors }: { sponsors: Sponsor[] }) {
  if (!sponsors || sponsors.length === 0) return null;
  const groups = groupByTier(sponsors);

  return (
    <section className="border-b border-gold-500/20 bg-cream-100/40">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-12 text-center">
          <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
            Quem nos apoia
          </div>
          <h2 className="font-display text-3xl font-semibold leading-tight text-earth-900 md:text-4xl">
            Patrocinadores e parceiros.
          </h2>
        </div>

        <div className="space-y-12">
          {groups.map((g) => (
            <div key={g.tier}>
              <div className="mb-6 text-center">
                <div className="inline-block border-b border-earth-800/15 px-4 pb-2 text-xs font-semibold uppercase tracking-widest text-earth-700/70">
                  {TIER_LABELS[g.tier]}
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
                {g.items.map((s) => (
                  <SponsorLogo key={s.id} sponsor={s} />
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
