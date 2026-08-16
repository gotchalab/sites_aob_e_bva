import Link from "next/link";
import { SafeImage } from "./safe-image";
import { ArrowRight } from "lucide-react";
import { formatDate } from "@/lib/api";
import type { ArticleSummary, HomeConfig, Site } from "@/lib/api-types";

export function HeroSection({
  site,
  home,
  featured,
}: {
  site: Site;
  home: HomeConfig;
  featured?: ArticleSummary | null;
}) {
  const foundedLine = home.foundedYear ? `Fundada em ${home.foundedYear}` : null;
  const membersLine = home.memberCount ? `${home.memberCount.toLocaleString("pt-PT")} sócios ativos` : null;
  const meta = [foundedLine, membersLine].filter(Boolean).join(" · ");

  return (
    <section className="hero-pattern border-b border-sand-300">
      <div className="mx-auto max-w-6xl px-4 py-16 md:py-24">
        <div className="grid gap-12 md:grid-cols-[1.05fr,1fr] md:items-center">
          <div>
            <div className="mb-4 inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white/70 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700 backdrop-blur-sm">
              <span className="h-1.5 w-1.5 rounded-full bg-brand-500" />
              BVA · Portugal
            </div>
            <h1 className="font-display text-4xl font-bold leading-[1.05] tracking-tight text-ink-900 md:text-6xl lg:text-7xl">
              {site.name}
            </h1>
            {site.tagline && (
              <p className="mt-6 max-w-xl text-lg leading-relaxed text-ink-800/85 md:text-xl">
                {site.tagline}
              </p>
            )}
            {meta && (
              <p className="mt-4 text-xs uppercase tracking-widest text-ink-500">{meta}</p>
            )}
            <div className="mt-9 flex flex-wrap gap-3">
              <Link
                href="/inscricao-socio"
                className="inline-flex items-center gap-2 rounded-full bg-brand-500 px-6 py-3 text-sm font-semibold text-white shadow-sm ring-1 ring-brand-600/20 transition hover:bg-brand-600 hover:shadow-md"
              >
                Quero ser sócio
                <ArrowRight className="h-4 w-4" strokeWidth={2.5} aria-hidden />
              </Link>
              <Link
                href="/artigos"
                className="inline-flex items-center rounded-full border border-ink-800/15 bg-white/80 px-6 py-3 text-sm font-medium text-ink-800 backdrop-blur-sm transition hover:border-brand-500 hover:text-brand-600"
              >
                Explorar artigos
              </Link>
            </div>
          </div>

          {featured && (
            <Link
              href={`/artigos/${featured.slug}`}
              className="group relative hidden overflow-hidden rounded-2xl bg-white shadow-lg ring-1 ring-ink-900/10 transition hover:-translate-y-1 hover:shadow-2xl hover:ring-brand-500/30 md:block"
            >
              {featured.coverImagePath ? (
                <>
                  <div className="relative h-[320px] w-full overflow-hidden bg-white p-3 sm:h-[400px] sm:p-4 md:h-[440px]">
                    <SafeImage
                      src={featured.coverImagePath}
                      alt={featured.title}
                      fill
                      sizes="(min-width: 768px) 50vw, 100vw"
                      priority
                      className="object-contain transition duration-700 group-hover:scale-[1.02]"
                    />
                  </div>
                  <div className="absolute left-4 top-4 inline-flex items-center gap-1.5 rounded-full bg-white/95 px-3 py-1 text-[10px] font-bold uppercase tracking-widest text-brand-600 shadow-sm backdrop-blur-sm ring-1 ring-black/5">
                    {featured.isFeatured ? "Em destaque" : featured.categoryName}
                  </div>
                  <div className="border-t border-black/5 bg-white px-5 py-4">
                    <h2 className="line-clamp-2 font-display text-lg font-semibold leading-snug text-ink-900 group-hover:text-brand-600 md:text-xl">
                      {featured.title}
                    </h2>
                  </div>
                </>
              ) : (
                <div className="relative flex h-[320px] w-full flex-col justify-between overflow-hidden bg-gradient-to-br from-sand-100 via-sand-200 to-brand-100 p-6 sm:h-[400px] sm:p-8 md:h-[440px]">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    aria-hidden
                    src="/logo.png"
                    alt=""
                    className="pointer-events-none absolute -right-10 -bottom-10 h-[360px] w-auto select-none opacity-[0.12] sm:h-[440px] md:h-[500px]"
                  />
                  <div className="relative flex items-center gap-2 text-[10px] font-bold uppercase tracking-widest text-brand-700">
                    <span className="inline-flex items-center rounded-full bg-white/95 px-3 py-1 shadow-sm ring-1 ring-black/5">
                      {featured.isFeatured ? "Em destaque" : featured.categoryName}
                    </span>
                    {featured.publishedAt && (
                      <span className="text-ink-500">{formatDate(featured.publishedAt)}</span>
                    )}
                  </div>
                  <div className="relative">
                    <h2 className="line-clamp-3 font-display text-2xl font-bold leading-[1.15] tracking-tight text-ink-900 group-hover:text-brand-600 sm:text-3xl md:text-4xl">
                      {featured.title}
                    </h2>
                    {featured.excerpt && (
                      <p className="mt-3 line-clamp-3 text-sm leading-relaxed text-ink-800/80 sm:text-base">
                        {featured.excerpt}
                      </p>
                    )}
                    <div className="mt-5 inline-flex items-center gap-1.5 text-sm font-semibold text-brand-600 group-hover:text-brand-700">
                      Ler artigo
                      <ArrowRight className="h-4 w-4 transition group-hover:translate-x-0.5" strokeWidth={2.5} aria-hidden />
                    </div>
                  </div>
                </div>
              )}
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}
