import Link from "next/link";
import { apiGet } from "@/lib/socio-auth";

type SocioProfile = {
  id: number;
  numeroSocio: string;
  nomeCompleto: string;
  email: string;
  estado: string;
  dataInscricao: string;
};

type Quota = { id: number; ano: number; valor: number; dataPagamento: string | null };
type Pedido = { id: number; ano: number; especieCientifica: string; estado: string; dataPedido: string };

export const metadata = { title: "Área de sócio" };

export default async function SocioDashboard() {
  const [me, quotas, anilhas] = await Promise.all([
    apiGet<SocioProfile>("/api/me"),
    apiGet<Quota[]>("/api/me/quotas"),
    apiGet<Pedido[]>("/api/me/anilhas"),
  ]);

  if (!me) {
    return (
      <div className="rounded bg-amber-50 p-4 text-sm text-amber-800">
        Não encontrei o perfil de sócio associado a esta conta.
      </div>
    );
  }

  const quotasPagas = quotas?.filter((q) => q.dataPagamento).length ?? 0;
  const currentYear = new Date().getFullYear();
  const quotaEmAtraso = !quotas?.some((q) => q.ano === currentYear && q.dataPagamento);
  const pendentes = anilhas?.filter((a) => a.estado === "Pendente").length ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-3xl font-bold">Olá, {me.nomeCompleto.split(" ")[0]}</h1>
        <p className="text-sm text-ink-500">
          Nº {me.numeroSocio} · {me.estado} · Inscrito em {new Date(me.dataInscricao).toLocaleDateString("pt-PT")}
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card title="Quotas pagas" value={String(quotasPagas)} link="/socio/quotas" />
        <Card
          title={`Quota ${currentYear}`}
          value={quotaEmAtraso ? "Em atraso" : "Regularizada"}
          tone={quotaEmAtraso ? "warn" : "ok"}
          link="/socio/quotas"
        />
        <Card title="Pedidos pendentes" value={String(pendentes)} link="/socio/anilhas" />
      </div>

      <div className="rounded-lg border border-sand-300 bg-white p-6">
        <h2 className="text-xl font-semibold">Atalhos</h2>
        <div className="mt-3 flex flex-wrap gap-2">
          <Link href="/socio/dados" className="rounded border border-sand-300 px-4 py-2 text-sm hover:bg-sand-100">
            Atualizar dados
          </Link>
          <Link href="/socio/pedir-anilhas" className="rounded bg-brand-500 px-4 py-2 text-sm text-white hover:bg-brand-600">
            Pedir anilhas
          </Link>
        </div>
      </div>
    </div>
  );
}

function Card({
  title,
  value,
  tone = "neutral",
  link,
}: {
  title: string;
  value: string;
  tone?: "ok" | "warn" | "neutral";
  link?: string;
}) {
  const toneCls = tone === "ok" ? "text-green-700" : tone === "warn" ? "text-amber-700" : "text-ink-900";
  const body = (
    <>
      <div className="text-xs uppercase tracking-wide text-ink-500">{title}</div>
      <div className={`mt-1 text-2xl font-bold ${toneCls}`}>{value}</div>
    </>
  );
  const cls = "block rounded-lg border border-sand-300 bg-white p-4 shadow-sm hover:shadow";
  return link ? <Link href={link} className={cls}>{body}</Link> : <div className={cls}>{body}</div>;
}
