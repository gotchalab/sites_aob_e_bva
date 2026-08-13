import { PedidoForm } from "./pedido-form";

export const metadata = { title: "Pedir anilhas" };

export default async function PedirAnilhasPage() {
  return (
    <div className="max-w-2xl">
      <h1 className="text-3xl font-bold">Pedir anilhas</h1>
      <p className="mt-2 text-sm text-ink-500">
        Depois de submetido, o pedido fica <b>Pendente</b> até validação pela direção.
      </p>
      <div className="mt-6 rounded-lg border border-sand-300 bg-white p-6 shadow-sm">
        <PedidoForm />
      </div>
    </div>
  );
}
