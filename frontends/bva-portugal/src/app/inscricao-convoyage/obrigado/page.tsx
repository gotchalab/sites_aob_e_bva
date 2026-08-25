import Link from "next/link";
import { SITE_SLUG } from "@/lib/config";

export const metadata = { title: "Inscrição submetida" };

export default async function ObrigadoConvoyagePage({
  searchParams,
}: {
  searchParams: Promise<{ id?: string; t?: string; traces?: string }>;
}) {
  const sp = await searchParams;
  const idNum = sp.id ? Number(sp.id) : null;
  const hasTraces = sp.traces === "1";
  // Path relativo — o nginx do dominio faz proxy de /api/ para a API interna.
  // Absoluto (com API_INTERNAL_URL) tem 127.0.0.1 que so funciona no servidor.
  const pdfHref =
    idNum && sp.t
      ? `/api/sites/${SITE_SLUG}/forms/inscricao-convoyage/${idNum}/pdf?token=${encodeURIComponent(sp.t)}`
      : null;
  const tracesHref =
    idNum && sp.t && hasTraces
      ? `/api/sites/${SITE_SLUG}/forms/inscricao-convoyage/${idNum}/traces?token=${encodeURIComponent(sp.t)}`
      : null;
  return (
    <div className="mx-auto max-w-2xl px-4 py-16 text-center">
      <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-600">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-8 w-8">
          <path d="M20 6 9 17l-5-5" />
        </svg>
      </div>
      <h1 className="font-display text-3xl font-bold text-ink-900 md:text-4xl">
        Inscrição submetida com sucesso
      </h1>
      <p className="mt-4 text-base text-ink-700 md:text-lg">
        A sua inscrição na convoyage BVA Masters foi recebida. Enviámos-lhe uma cópia por email
        com a ficha de inscrição em PDF.
      </p>
      <p className="mt-3 text-sm text-ink-600">
        <b>Não recebeu o email?</b> Verifique a caixa de <b>spam</b> ou <b>promoções</b>.
        {pdfHref ? " Pode também transferir aqui a ficha em PDF." : ""}
      </p>
      {sp.id && (
        <p className="mt-3 text-sm text-ink-500">Referência: #{sp.id}</p>
      )}
      <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row sm:flex-wrap">
        {pdfHref && (
          <a
            href={pdfHref}
            target="_blank"
            rel="noopener"
            className="inline-flex items-center justify-center gap-2 rounded-full bg-emerald-600 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-emerald-700"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" x2="12" y1="15" y2="3" />
            </svg>
            Ficha de inscrição
          </a>
        )}
        {tracesHref && (
          <a
            href={tracesHref}
            target="_blank"
            rel="noopener"
            className="inline-flex items-center justify-center gap-2 rounded-full bg-indigo-600 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-indigo-700"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-4 w-4">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" x2="12" y1="15" y2="3" />
            </svg>
            Declaração TRACES
          </a>
        )}
        <Link
          href="/"
          className="inline-flex items-center justify-center rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600"
        >
          Voltar ao início
        </Link>
        <Link
          href="/artigos"
          className="inline-flex items-center justify-center rounded-full border border-ink-900/15 bg-white px-6 py-3 text-sm font-medium text-ink-800 shadow-sm transition hover:bg-sand-100"
        >
          Ver artigos
        </Link>
      </div>
    </div>
  );
}
