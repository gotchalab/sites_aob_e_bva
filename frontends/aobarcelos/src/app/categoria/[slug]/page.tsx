import type { Metadata } from "next";
import Link from "next/link";
import { api } from "@/lib/api";
import type { CategoryTree } from "@/lib/api-types";
import { LoadMoreArticles } from "@/components/load-more-articles";

export const revalidate = 300;

const PAGE_SIZE = 12;

function findCategory(nodes: CategoryTree[], slug: string): CategoryTree | null {
  for (const n of nodes) {
    if (n.slug === slug) return n;
    const found = findCategory(n.children, slug);
    if (found) return found;
  }
  return null;
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const categories = await api.categories("articles").catch(() => []);
  const category = findCategory(categories, slug);
  return { title: category?.name ?? slug };
}

export default async function CategoryPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const [list, categories] = await Promise.all([
    api.articles({ category: slug, page: 1, pageSize: PAGE_SIZE }),
    api.categories("articles"),
  ]);
  const category = findCategory(categories, slug);
  const title = category?.name ?? slug;

  return (
    <>
      <section className="hero-pattern border-b border-gold-500/30">
        <div className="mx-auto max-w-6xl px-4 py-10 md:py-14">
          <Link href="/artigos" className="text-sm font-medium text-brand-600 hover:underline">
            ← Todos os artigos
          </Link>
          <h1 className="mt-3 font-display text-3xl font-bold text-earth-900 md:text-4xl">
            {title}
          </h1>
          {category?.description && (
            <div
              className="category-lead max-w-3xl"
              dangerouslySetInnerHTML={{ __html: category.description }}
            />
          )}
          <p className="mt-2 text-sm text-earth-700/60">
            {list.total} {list.total === 1 ? "artigo" : "artigos"}
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-6xl px-4 py-10 md:py-14">
        {list.items.length === 0 ? (
          <p className="text-earth-700/70">Sem artigos nesta categoria.</p>
        ) : (
          <LoadMoreArticles
            initialItems={list.items}
            total={list.total}
            pageSize={PAGE_SIZE}
            params={{ category: slug }}
          />
        )}
      </div>
    </>
  );
}
