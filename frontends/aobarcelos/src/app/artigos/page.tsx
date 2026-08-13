import Link from "next/link";
import { api } from "@/lib/api";
import { LoadMoreArticles } from "@/components/load-more-articles";

export const revalidate = 300;
export const metadata = { title: "Artigos" };

const PAGE_SIZE = 12;

export default async function ArticlesPage({
  searchParams,
}: {
  searchParams: Promise<{ category?: string; q?: string }>;
}) {
  const sp = await searchParams;
  const [list, categories] = await Promise.all([
    api.articles({ page: 1, pageSize: PAGE_SIZE, category: sp.category, search: sp.q }),
    api.categories("articles"),
  ]);

  return (
    <>
      <section className="hero-pattern border-b border-gold-500/30">
        <div className="mx-auto max-w-6xl px-4 py-10 md:py-14">
          <h1 className="font-display text-3xl font-bold text-earth-900 md:text-4xl">Artigos</h1>
          <p className="mt-2 text-sm text-earth-700/70">{list.total} resultados</p>
        </div>
      </section>

      <div className="mx-auto max-w-6xl px-4 py-10">
        <div className="flex flex-col gap-8 lg:flex-row">
          <aside className="w-full flex-shrink-0 lg:w-64">
            <h2 className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-earth-700/70">
              Categorias
            </h2>
            <ul className="space-y-1 rounded-lg border border-gold-500/30 bg-white p-2">
              <li>
                <Link
                  href="/artigos"
                  className={`block rounded-md px-3 py-2 text-sm transition ${
                    !sp.category
                      ? "bg-brand-500 text-white"
                      : "text-earth-800 hover:bg-cream-100 hover:text-brand-700"
                  }`}
                >
                  Todas
                </Link>
              </li>
              {categories.map((c) => (
                <li key={c.id}>
                  <Link
                    href={`/artigos?category=${c.slug}`}
                    className={`block rounded-md px-3 py-2 text-sm transition ${
                      sp.category === c.slug
                        ? "bg-brand-500 text-white"
                        : "text-earth-800 hover:bg-cream-100 hover:text-brand-700"
                    }`}
                  >
                    {c.name}
                  </Link>
                  {c.children.length > 0 && (
                    <ul className="ml-3 mt-1 space-y-1 border-l border-gold-500/30 pl-2">
                      {c.children.map((cc) => (
                        <li key={cc.id}>
                          <Link
                            href={`/artigos?category=${cc.slug}`}
                            className={`block rounded-md px-2 py-1 text-xs transition ${
                              sp.category === cc.slug
                                ? "bg-brand-500 text-white"
                                : "text-earth-700/80 hover:bg-cream-100 hover:text-brand-700"
                            }`}
                          >
                            {cc.name}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </li>
              ))}
            </ul>
          </aside>

          <section className="flex-1 min-w-0">
            {list.items.length === 0 ? (
              <p className="text-earth-700/70">Nenhum artigo encontrado.</p>
            ) : (
              <LoadMoreArticles
                initialItems={list.items}
                total={list.total}
                pageSize={PAGE_SIZE}
                params={{ category: sp.category, search: sp.q }}
              />
            )}
          </section>
        </div>
      </div>
    </>
  );
}
