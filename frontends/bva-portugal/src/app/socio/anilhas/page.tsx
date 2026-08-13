import Link from "next/link";
import { apiGet } from "@/lib/socio-auth";

type Pedido = {
  id: number;
  especieCientifica: string;
  especieNomeComum: string | null;
  ano: number;
  diametro: number;
  quantidade: number;
  estado: string;
  dataPedido: string;
  dataEntrega: string | null;
  observacoes: string | null;
};

export const metadata = { title: "Anilhas" };

const estadoBadge: Record<string, string> = {
  Pendente: "bg-amber-100 text-amber-800",
  Aprovado: "bg-blue-100 text-blue-800",
  Encomendado: "bg-indigo-100 text-indigo-800",
  Entregue: "bg-green-100 text-green-800",
  Cancelado: "bg-sand-200 text-ink-600",
};

export default async function AnilhasPage() {
  const pedidos = (await apiGet<Pedido[]>("/api/me/anilhas")) ?? [];
  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Anilhas</h1>
          <p className="mt-2 text-sm text-ink-500">Histórico de pedidos e respetivo estado.</p>
        </div>
        <Link href="/socio/pedir-anilhas" className="rounded bg-brand-500 px-4 py-2 text-sm text-white hover:bg-brand-600">
          Novo pedido
        </Link>
      </div>

      {pedidos.length === 0 ? (
        <p className="mt-6 text-ink-500">Sem pedidos.</p>
      ) : (
        <ul className="mt-6 divide-y divide-sand-200 rounded border border-sand-300 bg-white">
          {pedidos.map((p) => (
            <li key={p.id} className="flex flex-col gap-2 p-4 md:flex-row md:items-center md:justify-between">
              <div>
                <div className="text-sm font-semibold">
                  {p.especieCientifica}
                  {p.especieNomeComum && <span className="ml-2 text-ink-500">({p.especieNomeComum})</span>}
                </div>
                <div className="text-xs text-ink-500">
                  {p.ano} · ⌀ {p.diametro} mm · {p.quantidade} un · Pedido em {new Date(p.dataPedido).toLocaleDateString("pt-PT")}
                  {p.dataEntrega && ` · Entregue em ${new Date(p.dataEntrega).toLocaleDateString("pt-PT")}`}
                </div>
                {p.observacoes && <div className="mt-1 text-xs text-ink-500">Obs: {p.observacoes}</div>}
              </div>
              <span className={`self-start rounded px-2 py-1 text-xs font-medium ${estadoBadge[p.estado] ?? "bg-sand-200 text-ink-600"}`}>
                {p.estado}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
