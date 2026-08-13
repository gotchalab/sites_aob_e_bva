"use client";

import { useState } from "react";

type Profile = {
  nomeCompleto: string;
  telefone: string | null;
  nif: string | null;
  dataNascimento: string | null;
  morada: string | null;
  codigoPostal: string | null;
  localidade: string | null;
  especiesInteresse: string[];
};

export function DadosForm({ initial }: { initial: Profile }) {
  const [state, setState] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setState("saving");
    setErrorMsg(undefined);
    const fd = new FormData(e.currentTarget);
    const body = {
      nomeCompleto: String(fd.get("nomeCompleto") ?? ""),
      telefone: String(fd.get("telefone") ?? "") || null,
      nif: String(fd.get("nif") ?? "") || null,
      dataNascimento: String(fd.get("dataNascimento") ?? "") || null,
      morada: String(fd.get("morada") ?? "") || null,
      codigoPostal: String(fd.get("codigoPostal") ?? "") || null,
      localidade: String(fd.get("localidade") ?? "") || null,
      especiesInteresse: String(fd.get("especiesInteresse") ?? "")
        .split(",").map((s) => s.trim()).filter(Boolean),
    };
    const res = await fetch("/socio/api/perfil", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      setState("error");
      setErrorMsg(err.error ?? `Erro ${res.status}`);
      return;
    }
    setState("saved");
  }

  const dataNasc = initial.dataNascimento ? initial.dataNascimento.slice(0, 10) : "";

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4">
      <div className="grid gap-4 md:grid-cols-2">
        <label className="block md:col-span-2">
          <span className="text-sm font-medium text-slate-700">Nome completo *</span>
          <input required name="nomeCompleto" defaultValue={initial.nomeCompleto} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-slate-700">Telefone</span>
          <input name="telefone" defaultValue={initial.telefone ?? ""} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-slate-700">NIF</span>
          <input name="nif" defaultValue={initial.nif ?? ""} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-slate-700">Data de nascimento</span>
          <input type="date" name="dataNascimento" defaultValue={dataNasc} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block md:col-span-2">
          <span className="text-sm font-medium text-slate-700">Morada</span>
          <input name="morada" defaultValue={initial.morada ?? ""} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-slate-700">Código postal</span>
          <input name="codigoPostal" defaultValue={initial.codigoPostal ?? ""} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block">
          <span className="text-sm font-medium text-slate-700">Localidade</span>
          <input name="localidade" defaultValue={initial.localidade ?? ""} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
        <label className="block md:col-span-2">
          <span className="text-sm font-medium text-slate-700">Espécies de interesse (separadas por vírgula)</span>
          <input name="especiesInteresse" defaultValue={initial.especiesInteresse.join(", ")} className="mt-1 w-full rounded border border-slate-300 px-3 py-2" />
        </label>
      </div>

      {state === "error" && errorMsg && (
        <div className="rounded bg-red-50 p-3 text-sm text-red-700">{errorMsg}</div>
      )}
      {state === "saved" && (
        <div className="rounded bg-green-50 p-3 text-sm text-green-700">Dados atualizados.</div>
      )}

      <button
        type="submit"
        disabled={state === "saving"}
        className="rounded bg-brand-500 px-4 py-2 text-white font-medium hover:bg-brand-600 disabled:opacity-50 self-start"
      >
        {state === "saving" ? "A guardar..." : "Guardar"}
      </button>
    </form>
  );
}
