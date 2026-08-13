import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { api, formatBytes, formatDate } from "@/lib/api";

export const revalidate = 300;

export async function generateMetadata({
  params,
}: {
  params: Promise<{ slug: string }>;
}): Promise<Metadata> {
  const { slug } = await params;
  const d = await api.download(slug).catch(() => null);
  return { title: d?.title ?? "Download" };
}

export default async function DownloadPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  const d = await api.download(slug);
  if (!d) notFound();

  return (
    <>
      <section className="hero-pattern border-b border-sand-300">
        <div className="mx-auto max-w-3xl px-4 pt-10 pb-8 md:pt-14">
          <Link href="/downloads" className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-widest text-brand-600 hover:text-brand-700">
            ← Todos os downloads
          </Link>
          <h1 className="mt-4 font-display text-3xl font-bold leading-tight text-ink-900 md:text-4xl">{d.title}</h1>
          {d.categoryName && (
            <div className="mt-3 text-sm text-ink-500">{d.categoryName}</div>
          )}
        </div>
      </section>

      <div className="mx-auto max-w-3xl px-4 py-10">
        {d.description && (
          <div
            className="prose-article text-ink-800"
            dangerouslySetInnerHTML={{ __html: d.description }}
          />
        )}
        <dl className="mt-8 grid grid-cols-2 gap-3 rounded-2xl border border-sand-300 bg-white p-5 text-sm shadow-sm">
          <dt className="text-ink-500">Ficheiro</dt>
          <dd className="font-medium text-ink-900">{d.fileName}</dd>
          <dt className="text-ink-500">Tipo</dt>
          <dd className="font-medium text-ink-900">{d.mimeType}</dd>
          {d.fileSize > 0 && (<>
            <dt className="text-ink-500">Tamanho</dt>
            <dd className="font-medium text-ink-900">{formatBytes(d.fileSize)}</dd>
          </>)}
          {d.publishedAt && (<>
            <dt className="text-ink-500">Publicado em</dt>
            <dd className="font-medium text-ink-900">{formatDate(d.publishedAt)}</dd>
          </>)}
          <dt className="text-ink-500">Downloads</dt>
          <dd className="font-medium text-ink-900">{d.downloadCount}</dd>
        </dl>
        <a
          href={d.storagePath}
          target="_blank"
          rel="noopener noreferrer"
          className="mt-6 inline-flex items-center gap-2 rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600"
        >
          Descarregar
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="7 10 12 15 17 10" />
            <line x1="12" x2="12" y1="15" y2="3" />
          </svg>
        </a>
      </div>
    </>
  );
}
