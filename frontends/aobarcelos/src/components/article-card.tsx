import Link from "next/link";
import type { ArticleSummary } from "@/lib/api-types";
import { formatDate } from "@/lib/api";
import { CoverImage } from "./cover-image";

export function ArticleCard({ article }: { article: ArticleSummary }) {
  const hasCover = Boolean(article.coverImagePath);

  return (
    <Link
      href={`/artigos/${article.slug}`}
      className="group relative flex h-full flex-col overflow-hidden rounded-2xl bg-white shadow-sm ring-1 ring-black/5 transition hover:-translate-y-1 hover:shadow-xl hover:ring-brand-500/30"
    >
      {hasCover ? (
        <>
          <div className="relative aspect-[4/5] w-full overflow-hidden bg-white p-2">
            <CoverImage
              src={article.coverImagePath!}
              alt=""
              className="h-full w-full object-contain transition duration-500 group-hover:scale-[1.02]"
            />
          </div>
          {article.categoryName && (
            <div className="absolute left-3 top-3 rounded-full bg-white/95 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-brand-600 shadow-sm backdrop-blur-sm">
              {article.categoryName}
            </div>
          )}
          <div className="flex flex-1 flex-col gap-1.5 p-4">
            <h3 className="line-clamp-2 font-display text-base font-semibold leading-snug text-earth-900 group-hover:text-brand-700">
              {article.title}
            </h3>
            {article.publishedAt && (
              <div className="mt-auto text-xs text-earth-700/60">{formatDate(article.publishedAt)}</div>
            )}
          </div>
        </>
      ) : (
        <div className="relative flex h-full flex-col justify-between overflow-hidden bg-gradient-to-br from-cream-100 via-cream-200 to-gold-400/40 p-4">
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            aria-hidden
            src="/logo.png"
            alt=""
            className="pointer-events-none absolute -right-6 -bottom-6 h-40 w-auto select-none opacity-[0.12]"
          />
          <div className="relative">
            {article.categoryName && (
              <span className="inline-flex items-center rounded-full bg-white/95 px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest text-brand-600 shadow-sm ring-1 ring-black/5">
                {article.categoryName}
              </span>
            )}
          </div>
          <div className="relative mt-6 flex flex-1 flex-col">
            <h3 className="line-clamp-4 font-display text-xl font-bold leading-tight tracking-tight text-earth-900 group-hover:text-brand-700">
              {article.title}
            </h3>
            {article.excerpt && (
              <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-earth-800/75">
                {article.excerpt}
              </p>
            )}
            {article.publishedAt && (
              <div className="mt-auto pt-3 text-xs text-earth-700/60">{formatDate(article.publishedAt)}</div>
            )}
          </div>
        </div>
      )}
    </Link>
  );
}
