import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { api, formatDate, stripDuplicateCover } from "@/lib/api";
import { articleJsonLd, breadcrumbJsonLd } from "@/lib/jsonld";
import { ViewTracker } from "@/components/view-tracker";
import { CoverImage } from "@/components/cover-image";

export const revalidate = 300;

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const a = await api.article(slug);
  if (!a) return { title: "Artigo não encontrado" };
  return {
    title: a.metaTitle ?? a.title,
    description: a.metaDescription ?? a.excerpt ?? undefined,
    alternates: { canonical: `/artigos/${a.slug}` },
    openGraph: {
      title: a.title,
      description: a.excerpt ?? undefined,
      images: a.coverImagePath ? [a.coverImagePath] : undefined,
      type: "article",
      publishedTime: a.publishedAt ?? undefined,
      url: `/artigos/${a.slug}`,
    },
    twitter: {
      card: "summary_large_image",
      title: a.title,
      description: a.excerpt ?? undefined,
      images: a.coverImagePath ? [a.coverImagePath] : undefined,
    },
  };
}

export default async function ArticlePage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const [article, site] = await Promise.all([api.article(slug), api.site().catch(() => null)]);
  if (!article) notFound();

  const content = stripDuplicateCover(article.content, article.coverImagePath);
  const jsonLd = articleJsonLd(article, site);
  const breadcrumb = breadcrumbJsonLd(
    [
      { name: "Início", url: "/" },
      { name: "Artigos", url: "/artigos" },
      { name: article.categoryName, url: `/artigos?category=${article.categorySlug}` },
      { name: article.title, url: `/artigos/${article.slug}` },
    ],
    site,
  );

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumb) }}
      />
      <section className="hero-pattern border-b border-gold-500/30">
        <div className="mx-auto max-w-3xl px-4 pt-10 pb-8 md:pt-14">
          <Link
            href={`/artigos?category=${article.categorySlug}`}
            className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-widest text-brand-600 hover:text-brand-700"
          >
            ← {article.categoryName}
          </Link>
          <h1 className="mt-4 font-display text-3xl font-bold leading-tight text-earth-900 md:text-5xl">
            {article.title}
          </h1>
          <div className="mt-5 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-earth-700/70">
            {article.publishedAt && (
              <span className="flex items-center gap-1.5">
                <span aria-hidden>📅</span> {formatDate(article.publishedAt)}
              </span>
            )}
            <span className="flex items-center gap-1.5">
              <span aria-hidden>👁</span> {article.viewCount} visualizações
            </span>
          </div>
        </div>
      </section>

      <ViewTracker slug={article.slug} />

      {article.coverImagePath && (
        <div className="mx-auto mt-8 flex max-w-4xl justify-center px-4">
          <CoverImage
            src={article.coverImagePath}
            alt={article.title}
            className="max-h-[70vh] w-auto max-w-full rounded-2xl object-contain shadow-xl ring-1 ring-earth-900/5"
            hideOnError
          />
        </div>
      )}

      <article className="mx-auto max-w-3xl px-4 py-10">
        <div
          className="prose-article text-earth-900"
          dangerouslySetInnerHTML={{ __html: content }}
        />

        {article.tags && article.tags.length > 0 && (
          <div className="mt-10 flex flex-wrap gap-2 border-t border-gold-500/30 pt-6">
            {article.tags.map((t) => (
              <span
                key={t}
                className="rounded-full border border-gold-500/40 bg-cream-100 px-3 py-1 text-xs font-medium text-earth-700"
              >
                #{t}
              </span>
            ))}
          </div>
        )}

        <div className="mt-10 flex items-center justify-between border-t border-gold-500/30 pt-6">
          <Link
            href="/artigos"
            className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700"
          >
            ← Todos os artigos
          </Link>
          <Link
            href={`/artigos?category=${article.categorySlug}`}
            className="text-sm font-medium text-earth-700 hover:text-brand-700"
          >
            Mais em {article.categoryName} →
          </Link>
        </div>
      </article>
    </>
  );
}
