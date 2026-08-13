import Link from "next/link";
import type { HomeConfig, HomeAreaItem } from "@/lib/api-types";
import { AreaIcon } from "./icon-map";

function AreaCard({ a }: { a: HomeAreaItem }) {
  const hasLink = !!a.href;
  const inner = (
    <>
      <div className="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 transition group-hover:bg-brand-500 group-hover:text-white">
        <AreaIcon name={a.icon} className="h-5 w-5" />
      </div>
      <h3 className="font-display text-lg font-semibold leading-tight text-ink-900">{a.title}</h3>
      {a.description && (
        <p className="mt-2 text-sm leading-relaxed text-ink-700/80">{a.description}</p>
      )}
      {hasLink && (
        <div className="mt-auto pt-4 text-sm font-semibold text-brand-600 opacity-0 transition group-hover:opacity-100">
          Saber mais →
        </div>
      )}
    </>
  );

  const base = "group flex h-full flex-col rounded-2xl border border-black/5 bg-white p-6 shadow-sm";

  if (hasLink) {
    return (
      <Link
        href={a.href!}
        className={`${base} transition hover:-translate-y-0.5 hover:border-brand-500/30 hover:shadow-md`}
      >
        {inner}
      </Link>
    );
  }

  return <div className={base}>{inner}</div>;
}

export function AreasGrid({ home }: { home: HomeConfig }) {
  const areas = home.areas ?? [];
  if (areas.length === 0) return null;

  return (
    <section className="border-b border-sand-300 bg-sand-100/60">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-12 md:text-center">
          <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
            Áreas de atuação
          </div>
          <h2 className="font-display text-3xl font-semibold leading-tight text-ink-900 md:text-4xl">
            O que fazemos pela criação nacional.
          </h2>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {areas.map((a) => (
            <AreaCard key={a.title} a={a} />
          ))}
        </div>
      </div>
    </section>
  );
}
