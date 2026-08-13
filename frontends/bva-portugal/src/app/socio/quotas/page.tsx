import { apiGet } from "@/lib/socio-auth";

type Quota = { id: number; ano: number; valor: number; dataPagamento: string | null; metodo: string | null; recibo: string | null };

export const metadata = { title: "Quotas" };

export default async function QuotasPage() {
  const quotas = (await apiGet<Quota[]>("/api/me/quotas")) ?? [];
  return (
    <div>
      <h1 className="text-3xl font-bold">Quotas</h1>
      <p className="mt-2 text-sm text-ink-500">Histórico de pagamentos de quotas anuais.</p>

      {quotas.length === 0 ? (
        <p className="mt-6 text-ink-500">Sem registos.</p>
      ) : (
        <table className="mt-6 w-full rounded border border-sand-300 bg-white text-sm">
          <thead className="bg-sand-100 text-left text-xs uppercase text-ink-500">
            <tr>
              <th className="px-4 py-2">Ano</th>
              <th className="px-4 py-2">Valor</th>
              <th className="px-4 py-2">Pago em</th>
              <th className="px-4 py-2">Método</th>
              <th className="px-4 py-2">Recibo</th>
            </tr>
          </thead>
          <tbody>
            {quotas.map((q) => (
              <tr key={q.id} className="border-t border-sand-200">
                <td className="px-4 py-2 font-medium">{q.ano}</td>
                <td className="px-4 py-2">{q.valor.toFixed(2)} €</td>
                <td className="px-4 py-2">
                  {q.dataPagamento
                    ? new Date(q.dataPagamento).toLocaleDateString("pt-PT")
                    : <span className="text-amber-700">Por pagar</span>}
                </td>
                <td className="px-4 py-2">{q.metodo ?? "-"}</td>
                <td className="px-4 py-2 font-mono text-xs">{q.recibo ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
