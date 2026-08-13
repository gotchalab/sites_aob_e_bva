"use client";

import { useState, useTransition } from "react";
import { API_URL, SITE_SLUG } from "@/lib/config";
import type { ArticleSummary, PagedResult } from "@/lib/api-types";
import { ArticleCard } from "./article-card";

type Params = { category?: string; search?: string };

export function LoadMoreArticles({
  initialItems,
  total,
  pageSize,
  apiSkip = 0,
  params = {},
}: {
  initialItems: ArticleSummary[];
  /** Total absoluto de artigos disponíveis na API (não subtraia o apiSkip). */
  total: number;
  /** Items por carga (múltiplo de 3 para grid alinhado com 1/2/3 colunas). */
  pageSize: number;
  /** Items já consumidos ANTES do initialItems (ex: 1 se houver featured no hero). */
  apiSkip?: number;
  params?: Params;
}) {
  const [items, setItems] = useState(initialItems);
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const shown = apiSkip + items.length;
  const hasMore = shown < total;

  function loadMore() {
    setError(null);
    startTransition(async () => {
      const q = new URLSearchParams();
      q.set("offset", String(shown));
      q.set("limit", String(pageSize));
      if (params.category) q.set("category", params.category);
      if (params.search) q.set("search", params.search);
      try {
        const res = await fetch(
          `${API_URL}/api/sites/${SITE_SLUG}/articles?${q.toString()}`,
          { headers: { Accept: "application/json" } }
        );
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as PagedResult<ArticleSummary>;
        setItems((prev) => dedupById([...prev, ...data.items]));
      } catch (e) {
        setError(e instanceof Error ? e.message : "Erro ao carregar mais artigos");
      }
    });
  }

  return (
    <>
      <div className="grid gap-5 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4">
        {items.map((a) => (
          <ArticleCard key={a.id} article={a} />
        ))}
      </div>

      {(hasMore || error) && (
        <div className="mt-10 flex flex-col items-center gap-3">
          {error && <p className="text-sm text-brand-700">{error} — tenta novamente.</p>}
          {hasMore && (
            <button
              type="button"
              onClick={loadMore}
              disabled={isPending}
              className="inline-flex items-center gap-2 rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-wait disabled:opacity-70"
            >
              {isPending ? "A carregar…" : "Ver mais artigos"}
              {!isPending && (
                <svg viewBox="0 0 20 20" className="h-4 w-4" fill="none" aria-hidden>
                  <path d="M5 8l5 5 5-5" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              )}
            </button>
          )}
          <p className="text-xs text-earth-700/60">
            {shown} de {total} artigos
          </p>
        </div>
      )}
    </>
  );
}

function dedupById(items: ArticleSummary[]): ArticleSummary[] {
  const seen = new Set<number>();
  const out: ArticleSummary[] = [];
  for (const it of items) {
    if (seen.has(it.id)) continue;
    seen.add(it.id);
    out.push(it);
  }
  return out;
}
