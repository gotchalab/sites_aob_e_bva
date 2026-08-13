import Link from "next/link";
import Image from "next/image";
import { Feather } from "lucide-react";
import type { HomeConfig } from "@/lib/api-types";

export function MissionBlock({ home }: { home: HomeConfig }) {
  if (!home.mission) return null;

  const highlights = [
    home.foundedYear ? { label: "Fundada em", value: String(home.foundedYear) } : null,
    home.memberCount ? { label: "Sócios ativos", value: home.memberCount.toLocaleString("pt-PT") } : null,
    home.ringsPerYear ? { label: "Anilhas emitidas / ano", value: home.ringsPerYear.toLocaleString("pt-PT") } : null,
    home.speciesCount ? { label: "Espécies acompanhadas", value: String(home.speciesCount) } : null,
  ].filter((x): x is { label: string; value: string } => x !== null);

  const paragraphs = home.mission.split(/\n{2,}|\n/).filter((p) => p.trim().length > 0);

  return (
    <section className="border-b border-gold-500/20 bg-white">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-28">
        <div className="grid gap-12 md:grid-cols-[1fr,1fr] md:items-start md:gap-16">
          <div>
            <div className="mb-4 text-[11px] font-semibold uppercase tracking-[0.2em] text-brand-600">
              Quem somos
            </div>
            <h2 className="font-display text-3xl font-semibold leading-[1.15] tracking-tight text-earth-900 md:text-[2.5rem]">
              {home.missionTitle ?? "A tua referência para criar aves com confiança em Barcelos."}
            </h2>
            <div className="mt-8 space-y-4 text-[15px] leading-relaxed text-earth-800/80 md:text-base">
              {paragraphs.map((p, i) => (
                <p key={i}>{p}</p>
              ))}
            </div>
            <div className="mt-10">
              <Link
                href="/quem-somos"
                className="inline-flex items-center gap-1.5 border-b border-brand-500/40 pb-0.5 text-sm font-semibold text-brand-600 transition hover:border-brand-600 hover:text-brand-700"
              >
                Sobre a associação
                <span aria-hidden>→</span>
              </Link>
            </div>
          </div>

          <MissionAside home={home} highlights={highlights} />
        </div>
      </div>
    </section>
  );
}

function MissionAside({
  home,
  highlights,
}: {
  home: HomeConfig;
  highlights: { label: string; value: string }[];
}) {
  if (home.missionImageUrl) {
    return (
      <div className="relative aspect-[5/4] w-full overflow-hidden rounded-md bg-cream-200 shadow-sm ring-1 ring-black/5">
        <Image
          src={home.missionImageUrl}
          alt=""
          fill
          sizes="(min-width: 768px) 50vw, 100vw"
          className="object-cover"
        />
      </div>
    );
  }

  return (
    <aside className="md:pt-4">
      <div className="border-l-2 border-brand-500 pl-6 md:pl-8">
        <Feather className="h-6 w-6 text-brand-500" strokeWidth={1.5} aria-hidden />
        <p className="mt-5 font-display text-xl leading-snug text-earth-900 md:text-2xl">
          {home.missionQuote ?? (
            <>
              Na AOB tens anilhas federadas, exposições reconhecidas e uma comunidade activa —
              <span className="text-earth-800/70"> tudo o que precisas para criar aves com suporte em Barcelos.</span>
            </>
          )}
        </p>
      </div>

      {highlights.length > 0 && (
        <dl className="mt-10 grid grid-cols-2 gap-x-6 gap-y-8 md:pl-8">
          {highlights.map((h) => (
            <div key={h.label}>
              <dt className="text-[11px] font-semibold uppercase tracking-[0.18em] text-earth-700/60">
                {h.label}
              </dt>
              <dd className="mt-1 font-display text-3xl font-semibold text-earth-900">{h.value}</dd>
            </div>
          ))}
        </dl>
      )}
    </aside>
  );
}
