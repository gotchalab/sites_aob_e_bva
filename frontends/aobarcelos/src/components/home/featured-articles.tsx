import Link from "next/link";
import { SafeImage } from "./safe-image";
import type { ArticleSummary } from "@/lib/api-types";
import { cleanExcerpt, formatDate } from "@/lib/api";

/**
 * Mostra 1 destaque grande + 2 pequenos. Se o hero já usou o featured[0],
 * passa-se skipSlug para não repetir.
 */
export function FeaturedArticles({
  items,
  skipSlug,
}: {
  items: ArticleSummary[];
  skipSlug?: string;
}) {
  const filtered = skipSlug ? items.filter((a) => a.slug !== skipSlug) : items;
  if (filtered.length === 0) return null;

  const primary = filtered[0];
  const secondary = filtered.slice(1, 3);

  return (
    <section className="border-b border-gold-500/20">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-10 flex items-end justify-between">
          <div>
            <div className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
              Em destaque
            </div>
            <h2 className="font-display text-3xl font-semibold leading-tight text-earth-900 md:text-4xl">
              Escolhidos para si.
            </h2>
          </div>
          <Link
            href="/artigos"
            className="hidden text-sm font-semibold text-brand-600 hover:text-brand-700 md:inline"
          >
            Ver todos →
          </Link>
        </div>

        <div className="grid gap-6 md:grid-cols-[1.4fr,1fr]">
          <Link
            href={`/artigos/${primary.slug}`}
            className="group relative block overflow-hidden rounded-2xl bg-earth-900 shadow-lg ring-1 ring-earth-900/10"
          >
            <div className="relative aspect-[16/10] w-full overflow-hidden md:aspect-[5/4]">
              {primary.coverImagePath ? (
                <SafeImage
                  src={primary.coverImagePath}
                  alt={primary.title}
                  fill
                  sizes="(min-width: 768px) 55vw, 100vw"
                  className="object-contain transition duration-700 group-hover:scale-105"
                />
              ) : (
                <div className="flex h-full items-center justify-center bg-gradient-to-br from-earth-700 to-earth-900 text-6xl text-gold-400/50">
                  ✻
                </div>
              )}
            </div>
            <div className="absolute inset-x-0 bottom-0 bg-gradient-to-t from-earth-900 via-earth-900/85 to-transparent p-6 pt-24">
              <div className="mb-2 inline-flex items-center gap-1.5 rounded-full bg-brand-500/90 px-2.5 py-0.5 text-[10px] font-semibold uppercase tracking-widest text-white">
                {primary.categoryName}
              </div>
              <h3 className="font-display text-2xl font-semibold leading-tight text-white md:text-3xl">
                {primary.title}
              </h3>
              {primary.excerpt && (
                <p className="mt-2 line-clamp-2 text-sm text-white/80 md:text-base">
                  {cleanExcerpt(primary.excerpt, 160)}
                </p>
              )}
            </div>
          </Link>

          <div className="grid gap-6">
            {secondary.map((a) => (
              <Link
                key={a.id}
                href={`/artigos/${a.slug}`}
                className="group flex overflow-hidden rounded-2xl border border-black/5 bg-white shadow-sm transition hover:-translate-y-0.5 hover:border-brand-500/30 hover:shadow-md"
              >
                <div className="relative aspect-square w-32 shrink-0 overflow-hidden bg-cream-200 sm:w-40">
                  {a.coverImagePath ? (
                    <SafeImage
                      src={a.coverImagePath}
                      alt={a.title}
                      fill
                      sizes="160px"
                      className="object-contain transition duration-500 group-hover:scale-105"
                    />
                  ) : (
                    <div className="h-full w-full bg-gradient-to-br from-cream-200 to-cream-300" />
                  )}
                </div>
                <div className="flex flex-1 flex-col justify-center gap-1 p-4">
                  <div className="text-[10px] font-semibold uppercase tracking-widest text-brand-600">
                    {a.categoryName}
                  </div>
                  <h3 className="font-display text-base font-semibold leading-snug text-earth-900 group-hover:text-brand-700">
                    {a.title}
                  </h3>
                  {a.publishedAt && (
                    <div className="text-xs text-earth-700/60">{formatDate(a.publishedAt)}</div>
                  )}
                </div>
              </Link>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
