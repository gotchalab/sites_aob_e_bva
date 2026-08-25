import Link from "next/link";
import { SafeImage } from "./safe-image";
import { NewBadge, RelativeDate } from "./relative-date";
import type { ArticleSummary } from "@/lib/api-types";

function Card({ article }: { article: ArticleSummary }) {
  return (
    <Link
      href={`/artigos/${article.slug}`}
      className="group relative flex h-full flex-col overflow-hidden rounded-2xl bg-white shadow-sm ring-1 ring-black/5 transition hover:-translate-y-1 hover:shadow-xl hover:ring-brand-500/30"
    >
      {article.coverImagePath ? (
        <>
          <div className="relative aspect-[4/5] w-full overflow-hidden bg-white p-2">
            <SafeImage
              src={article.coverImagePath}
              alt={article.title}
              fill
              sizes="(min-width: 1280px) 25vw, (min-width: 768px) 33vw, 50vw"
              className="object-contain transition duration-500 group-hover:scale-[1.02]"
            />
          </div>
          {article.categoryName && (
            <div className="absolute left-3 top-3 rounded-full bg-white/95 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-brand-600 shadow-sm backdrop-blur-sm">
              {article.categoryName}
            </div>
          )}
          {article.publishedAt && <NewBadge publishedAt={article.publishedAt} />}
          <div className="flex flex-1 flex-col gap-1.5 p-4">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-ink-900 group-hover:text-brand-600">
              {article.title}
            </h3>
            {article.publishedAt && (
              <div className="mt-auto text-xs text-ink-500">
                <RelativeDate publishedAt={article.publishedAt} />
              </div>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="relative aspect-[4/5] w-full overflow-hidden bg-gradient-to-br from-sand-100 via-sand-200 to-brand-100 p-2">
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img
              aria-hidden
              src="/logo.png"
              alt=""
              className="pointer-events-none absolute -right-6 -bottom-6 h-40 w-auto select-none opacity-[0.12]"
            />
          </div>
          {article.categoryName && (
            <div className="absolute left-3 top-3 rounded-full bg-white/95 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-brand-600 shadow-sm backdrop-blur-sm">
              {article.categoryName}
            </div>
          )}
          {article.publishedAt && <NewBadge publishedAt={article.publishedAt} />}
          <div className="flex flex-1 flex-col gap-1.5 p-4">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-ink-900 group-hover:text-brand-600">
              {article.title}
            </h3>
            {article.excerpt && (
              <p className="line-clamp-2 text-sm leading-relaxed text-ink-800/75">
                {article.excerpt}
              </p>
            )}
            {article.publishedAt && (
              <div className="mt-auto text-xs text-ink-500">
                <RelativeDate publishedAt={article.publishedAt} />
              </div>
            )}
          </div>
        </>
      )}
    </Link>
  );
}

export function NewsGrid({
  items,
  total,
}: {
  items: ArticleSummary[];
  total: number;
}) {
  if (items.length === 0) return null;
  return (
    <section className="border-b border-sand-300">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-10 flex items-end justify-between border-b border-sand-300 pb-4">
          <div>
            <div className="mb-2 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
              Últimas notícias
            </div>
            <h2 className="font-display text-3xl font-semibold leading-tight text-ink-900 md:text-4xl">
              O que anda a acontecer.
            </h2>
          </div>
          <Link
            href="/artigos"
            className="hidden text-sm font-semibold text-brand-600 hover:text-brand-700 md:inline"
          >
            Ver todos ({total}) →
          </Link>
        </div>

        <div className="grid gap-5 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4">
          {items.map((a) => (
            <Card key={a.id} article={a} />
          ))}
        </div>

        <div className="mt-10 flex justify-center">
          <Link
            href="/artigos"
            className="inline-flex items-center gap-2 rounded-full border border-ink-800/15 bg-white px-7 py-3 text-sm font-semibold text-ink-800 shadow-sm transition hover:border-brand-500 hover:text-brand-600 hover:shadow-md"
          >
            Ver todos os artigos
            <span aria-hidden>→</span>
          </Link>
        </div>
      </div>
    </section>
  );
}
