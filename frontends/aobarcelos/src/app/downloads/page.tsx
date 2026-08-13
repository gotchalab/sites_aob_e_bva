import Link from "next/link";
import { api } from "@/lib/api";
import { DownloadItem } from "@/components/download-item";

export const revalidate = 300;

export const metadata = { title: "Downloads" };

export default async function DownloadsPage({
  searchParams,
}: {
  searchParams: Promise<{ page?: string; category?: string; q?: string }>;
}) {
  const sp = await searchParams;
  const page = Math.max(1, Number(sp.page ?? 1));
  const pageSize = 25;
  const [list, categories] = await Promise.all([
    api.downloads({ page, pageSize, category: sp.category, search: sp.q }),
    api.categories("downloads"),
  ]);
  const totalPages = Math.max(1, Math.ceil(list.total / pageSize));

  return (
    <>
      <section className="hero-pattern border-b border-gold-500/30">
        <div className="mx-auto max-w-6xl px-4 py-10 md:py-14">
          <div className="mb-3 inline-block rounded-full border border-brand-500/40 bg-white/60 px-3 py-1 text-[11px] font-medium uppercase tracking-widest text-brand-700">
            Recursos técnicos
          </div>
          <h1 className="font-display text-3xl font-bold leading-tight text-earth-900 md:text-5xl">
            Downloads
          </h1>
          <p className="mt-3 text-sm text-earth-700/70">
            {list.total} {list.total === 1 ? "ficheiro" : "ficheiros"} disponíveis — standards, regulamentos e fichas de inscrição.
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-6xl px-4 py-10 md:py-14">
        <div className="flex flex-col gap-8 lg:flex-row">
          <aside className="w-full flex-shrink-0 lg:w-64">
            <h2 className="mb-3 text-[11px] font-semibold uppercase tracking-widest text-earth-700/70">
              Categorias
            </h2>
            <ul className="space-y-1 rounded-lg border border-gold-500/30 bg-white p-2">
              <li>
                <Link
                  href="/downloads"
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
                    href={`/downloads?category=${c.slug}`}
                    className={`block rounded-md px-3 py-2 text-sm transition ${
                      sp.category === c.slug
                        ? "bg-brand-500 text-white"
                        : "text-earth-800 hover:bg-cream-100 hover:text-brand-700"
                    }`}
                  >
                    {c.name}
                  </Link>
                </li>
              ))}
            </ul>
          </aside>

          <section className="min-w-0 flex-1">
            {list.items.length === 0 ? (
              <p className="text-earth-700/70">Sem downloads.</p>
            ) : (
              <ul className="rounded-2xl border border-gold-500/30 bg-white px-4 shadow-sm">
                {list.items.map((d) => (
                  <DownloadItem key={d.id} download={d} />
                ))}
              </ul>
            )}
            {totalPages > 1 && (
              <nav className="mt-8 flex items-center justify-center gap-2">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => {
                  const q = new URLSearchParams();
                  q.set("page", String(p));
                  if (sp.category) q.set("category", sp.category);
                  return (
                    <Link
                      key={p}
                      href={`/downloads?${q.toString()}`}
                      className={`rounded-md px-3 py-1.5 text-sm transition ${
                        p === page
                          ? "bg-brand-500 text-white"
                          : "border border-gold-500/30 text-earth-700 hover:bg-cream-100 hover:text-brand-700"
                      }`}
                    >
                      {p}
                    </Link>
                  );
                })}
              </nav>
            )}
          </section>
        </div>
      </div>
    </>
  );
}
