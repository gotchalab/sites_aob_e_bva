import Link from "next/link";
import { SafeImage } from "./safe-image";
import type { ArticleSummary } from "@/lib/api-types";
import { formatDate } from "@/lib/api";

/** Considera "novo" um artigo publicado nas últimas 72h. */
function isNew(publishedAt: string | null): boolean {
  if (!publishedAt) return false;
  const d = new Date(publishedAt).getTime();
  if (Number.isNaN(d)) return false;
  return Date.now() - d < 72 * 60 * 60 * 1000;
}

function relative(publishedAt: string | null): string {
  if (!publishedAt) return "";
  const d = new Date(publishedAt).getTime();
  if (Number.isNaN(d)) return "";
  const diffMs = Date.now() - d;
  const days = Math.floor(diffMs / (24 * 60 * 60 * 1000));
  if (days < 1) return "Hoje";
  if (days === 1) return "Ontem";
  if (days < 7) return `Há ${days} dias`;
  return formatDate(publishedAt);
}

function Card({ article }: { article: ArticleSummary }) {
  const newFlag = isNew(article.publishedAt);
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
          {newFlag && (
            <div className="absolute right-3 top-3 rounded-full bg-gold-500 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-earth-900 shadow-sm">
              Novo
            </div>
          )}
          <div className="flex flex-1 flex-col gap-1.5 p-4">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-earth-900 group-hover:text-brand-700">
              {article.title}
            </h3>
            {article.publishedAt && (
              <div className="mt-auto text-xs text-earth-700/60">{relative(article.publishedAt)}</div>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="relative aspect-[4/5] w-full overflow-hidden bg-gradient-to-br from-cream-100 via-cream-200 to-gold-400/40 p-2">
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
          {newFlag && (
            <div className="absolute right-3 top-3 rounded-full bg-gold-500 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-earth-900 shadow-sm">
              Novo
            </div>
          )}
          <div className="flex flex-1 flex-col gap-1.5 p-4">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-earth-900 group-hover:text-brand-700">
              {article.title}
            </h3>
            {article.excerpt && (
              <p className="line-clamp-2 text-sm leading-relaxed text-earth-800/75">
                {article.excerpt}
              </p>
            )}
            {article.publishedAt && (
              <div className="mt-auto text-xs text-earth-700/60">{relative(article.publishedAt)}</div>
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
    <section className="border-b border-gold-500/20">
      <div className="mx-auto max-w-6xl px-4 py-20 md:py-24">
        <div className="mb-10 flex items-end justify-between border-b border-gold-500/30 pb-4">
          <div>
            <div className="mb-2 text-[11px] font-semibold uppercase tracking-widest text-brand-600">
              Últimas notícias
            </div>
            <h2 className="font-display text-3xl font-semibold leading-tight text-earth-900 md:text-4xl">
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
            className="inline-flex items-center gap-2 rounded-full border border-earth-800/20 bg-white px-7 py-3 text-sm font-semibold text-earth-800 shadow-sm transition hover:border-brand-500 hover:text-brand-700 hover:shadow-md"
          >
            Ver todos os artigos
            <span aria-hidden>→</span>
          </Link>
        </div>
      </div>
    </section>
  );
}
