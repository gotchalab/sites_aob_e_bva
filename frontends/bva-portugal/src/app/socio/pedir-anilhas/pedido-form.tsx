"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function PedidoForm() {
  const [state, setState] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();
  const router = useRouter();
  const currentYear = new Date().getFullYear();

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setState("sending");
    setErrorMsg(undefined);
    const fd = new FormData(e.currentTarget);
    const body = {
      especieCientifica: String(fd.get("especieCientifica") ?? ""),
      especieNomeComum: String(fd.get("especieNomeComum") ?? "") || null,
      ano: Number(fd.get("ano") ?? currentYear),
      diametro: Number(fd.get("diametro") ?? 0),
      quantidade: Number(fd.get("quantidade") ?? 1),
      observacoes: String(fd.get("observacoes") ?? "") || null,
    };
    const res = await fetch("/socio/api/pedidos-anilhas", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      setState("error");
      setErrorMsg(err.error ?? `Erro ${res.status}`);
      return;
    }
    setState("sent");
    (e.target as HTMLFormElement).reset();
    router.push("/socio/anilhas");
    router.refresh();
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="block">
          <span className="text-sm font-medium text-ink-700">Espécie científica *</span>
          <input required name="especieCientifica" className="mt-1 w-full rounded border border-sand-300 px-3 py-2" placeholder="Ex: Agapornis roseicollis" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-ink-700">Nome comum</span>
          <input name="especieNomeComum" className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-ink-700">Ano *</span>
          <input required type="number" name="ano" min={2024} max={currentYear + 2} defaultValue={currentYear} className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-ink-700">Diâmetro (mm) *</span>
          <input required type="number" name="diametro" min={2} max={30} step={0.5} className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-ink-700">Quantidade *</span>
          <input required type="number" name="quantidade" min={1} className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
        </label>
      </div>
      <label className="block">
        <span className="text-sm font-medium text-ink-700">Observações</span>
        <textarea name="observacoes" rows={3} className="mt-1 w-full rounded border border-sand-300 px-3 py-2" />
      </label>

      {state === "error" && errorMsg && (
        <div className="rounded bg-red-50 p-3 text-sm text-red-700">{errorMsg}</div>
      )}

      <button
        type="submit"
        disabled={state === "sending"}
        className="rounded bg-brand-500 px-4 py-2 text-white font-medium hover:bg-brand-600 disabled:opacity-50"
      >
        {state === "sending" ? "A submeter..." : "Submeter pedido"}
      </button>
    </form>
  );
}
