import Link from "next/link";

export const metadata = { title: "Inscrição submetida" };

export default async function ObrigadoPage({
  searchParams,
}: {
  searchParams: Promise<{ id?: string }>;
}) {
  const sp = await searchParams;
  return (
    <div className="mx-auto max-w-2xl px-4 py-16 text-center">
      <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-600">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="h-8 w-8">
          <path d="M20 6 9 17l-5-5" />
        </svg>
      </div>
      <h1 className="font-display text-3xl font-bold text-earth-900 md:text-4xl">
        Pedido submetido com sucesso
      </h1>
      <p className="mt-4 text-base text-earth-700 md:text-lg">
        Obrigado pela sua candidatura. Enviámos-lhe uma cópia por email com a ficha em PDF
        preenchida. A Direcção irá analisar o pedido em próxima reunião e será contactado(a)
        com a decisão.
      </p>
      {sp.id && (
        <p className="mt-3 text-sm text-earth-700/70">Referência: #{sp.id}</p>
      )}
      <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
        <Link
          href="/"
          className="inline-flex items-center justify-center rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600"
        >
          Voltar ao início
        </Link>
        <Link
          href="/artigos"
          className="inline-flex items-center justify-center rounded-full border border-earth-800/15 bg-white px-6 py-3 text-sm font-medium text-earth-800 shadow-sm transition hover:bg-cream-100"
        >
          Ver artigos
        </Link>
      </div>
    </div>
  );
}
