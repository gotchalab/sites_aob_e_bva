"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type { ConvoyageActiveYearDto } from "@/lib/api-types";
import {
  SPECIES_LABELS,
  TYPE_LABELS,
  filterNomenclatura,
  getAvailableTypes,
  getCodeForMutation,
  getGroupedMutations,
  type EntryType,
  type MutationGroup,
  type Species,
} from "@/lib/nomenclatura-bva";

const TURNSTILE_SITEKEY = process.env.NEXT_PUBLIC_TURNSTILE_SITEKEY;

declare global {
  interface Window {
    turnstile?: {
      render: (
        el: string | HTMLElement,
        opts: { sitekey: string; callback: (token: string) => void; theme?: string },
      ) => string;
      reset: (id?: string) => void;
    };
  }
}

const inputCls =
  "mt-1 w-full rounded-lg border bg-white px-3.5 py-2.5 text-sm text-ink-900 placeholder-ink-500/60 shadow-sm transition focus:outline-none focus:ring-2 focus:ring-brand-500/20";
const inputBorder = (err: boolean) =>
  err ? "border-red-500 focus:border-red-500" : "border-ink-900/15 focus:border-brand-500";
const labelCls = "text-[11px] font-medium uppercase tracking-widest text-ink-500";
const sectionCls = "rounded-2xl border border-sand-300 bg-white p-5 shadow-sm md:p-7";
const sectionTitleCls = "font-display text-lg font-semibold text-ink-900 md:text-xl";
const errCls = "mt-1 text-xs text-red-600";

type BirdState = {
  id: number;
  species: Species | "";
  type: EntryType | "";
  code: string;        // guardado para o DTO/PDF
  mutation: string;    // guardado para o DTO/PDF
  selectionLabel: string; // texto visível no combobox combinado
  anilha: string;
};

let nextId = 1;
function makeBird(): BirdState {
  return {
    id: nextId++,
    species: "",
    type: "",
    code: "",
    mutation: "",
    selectionLabel: "",
    anilha: "",
  };
}

const SEP = " — ";

type ComboboxItem =
  | { kind: "group"; label: string }
  | { kind: "item"; value: string };

function buildDisplayList(
  options: string[] | undefined,
  groupedOptions: MutationGroup[] | undefined,
  query: string,
): ComboboxItem[] {
  const q = query.toLowerCase();
  if (groupedOptions) {
    const result: ComboboxItem[] = [];
    for (const { group, items } of groupedOptions) {
      const matching = q ? items.filter((i) => i.toLowerCase().includes(q)) : items;
      if (matching.length === 0) continue;
      if (group) result.push({ kind: "group", label: group });
      for (const v of matching) result.push({ kind: "item", value: v });
    }
    return result;
  }
  const flat = options ?? [];
  const matching = q ? flat.filter((o) => o.toLowerCase().includes(q)) : flat;
  return matching.map((v) => ({ kind: "item" as const, value: v }));
}

function Combobox({
  options,
  groupedOptions,
  inputValue,
  onInputChange,
  onSelect,
  placeholder,
  disabled,
  hasError,
}: {
  options?: string[];
  groupedOptions?: MutationGroup[];
  inputValue: string;
  onInputChange: (v: string) => void;
  onSelect: (v: string) => void;
  placeholder?: string;
  disabled?: boolean;
  hasError?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const displayList = buildDisplayList(options, groupedOptions, inputValue);

  return (
    <div className="relative">
      <input
        type="text"
        value={inputValue}
        placeholder={placeholder ?? "Pesquisar..."}
        disabled={disabled}
        onChange={(e) => onInputChange(e.target.value)}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        className={`${inputCls} ${inputBorder(!!hasError)} ${disabled ? "cursor-not-allowed bg-sand-50 opacity-60" : ""}`}
      />
      {open && displayList.length > 0 && (
        <ul className="absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded-lg border border-sand-300 bg-white py-1 shadow-lg">
          {displayList.slice(0, 300).map((item, i) =>
            item.kind === "group" ? (
              <li
                key={`grp-${i}`}
                className="select-none px-3.5 pb-0.5 pt-2 text-[10px] font-semibold uppercase tracking-widest text-ink-400"
              >
                {item.label}
              </li>
            ) : (
              <li
                key={item.value}
                onMouseDown={() => {
                  onSelect(item.value);
                  setOpen(false);
                }}
                className="cursor-pointer px-3.5 py-1.5 text-sm text-ink-900 hover:bg-brand-500/10"
              >
                {item.value}
              </li>
            ),
          )}
        </ul>
      )}
    </div>
  );
}

function BirdCard({
  bird,
  idx,
  onUpdate,
  onRemove,
  birdErrors,
  canRemove,
}: {
  bird: BirdState;
  idx: number;
  onUpdate: (update: Partial<BirdState>) => void;
  onRemove: () => void;
  birdErrors: Record<string, string>;
  canRemove: boolean;
}) {
  const entries =
    bird.species && bird.type
      ? filterNomenclatura(bird.species as Species, bird.type as EntryType)
      : [];

  const availableTypes = bird.species
    ? getAvailableTypes(bird.species as Species)
    : ([] as EntryType[]);

  // Dropdown combinada: "002/02 — orange face green"
  const combinedGrouped: MutationGroup[] = getGroupedMutations(entries).map(({ group, items }) => ({
    group,
    items: items.map((mutation) => {
      const code = getCodeForMutation(entries, mutation) ?? "";
      return code ? `${code}${SEP}${mutation}` : mutation;
    }),
  }));

  function handleCombinedInputChange(v: string) {
    onUpdate({ selectionLabel: v, code: "", mutation: "" });
  }

  function handleCombinedSelect(v: string) {
    const idx = v.indexOf(SEP);
    const code = idx >= 0 ? v.slice(0, idx) : "";
    const mutation = idx >= 0 ? v.slice(idx + SEP.length) : v;
    onUpdate({ code, mutation, selectionLabel: v });
  }

  const typeDisabled = !bird.species;
  const comboDisabled = !bird.species || !bird.type;

  return (
    <div className="rounded-xl border border-sand-200 bg-sand-50/60 p-4">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-sm font-semibold text-ink-700">Ave {idx + 1}</span>
        {canRemove && (
          <button
            type="button"
            onClick={onRemove}
            className="text-xs text-red-600 hover:underline"
          >
            Remover
          </button>
        )}
      </div>

      {/* Espécie + Tipo */}
      <div className="mb-3 grid gap-3 sm:grid-cols-2">
        <div>
          <span className={labelCls}>Espécie *</span>
          <select
            value={bird.species}
            onChange={(e) =>
              onUpdate({
                species: e.target.value as Species | "",
                type: "",
                code: "",
                mutation: "",
                selectionLabel: "",
              })
            }
            className={`${inputCls} ${inputBorder(!!birdErrors.species)}`}
          >
            <option value="">— selecionar —</option>
            {(Object.entries(SPECIES_LABELS) as [Species, string][]).map(([k, v]) => (
              <option key={k} value={k}>
                {v}
              </option>
            ))}
          </select>
          {birdErrors.species && <p className={errCls}>{birdErrors.species}</p>}
        </div>

        <div>
          <span className={labelCls}>Tipo *</span>
          <select
            value={bird.type}
            disabled={typeDisabled}
            onChange={(e) =>
              onUpdate({
                type: e.target.value as EntryType | "",
                code: "",
                mutation: "",
                selectionLabel: "",
              })
            }
            className={`${inputCls} ${inputBorder(!!birdErrors.type)} ${typeDisabled ? "cursor-not-allowed opacity-60" : ""}`}
          >
            <option value="">— selecionar —</option>
            {availableTypes.map((t) => (
              <option key={t} value={t}>
                {TYPE_LABELS[t]}
              </option>
            ))}
          </select>
          {birdErrors.type && <p className={errCls}>{birdErrors.type}</p>}
        </div>
      </div>

      {/* Dropdown combinada: Nº Série/Classe — Mutação */}
      <div className="mb-3">
        <span className={labelCls}>Nº Série/Classe — Espécies e Mutação *</span>
        <Combobox
          groupedOptions={combinedGrouped}
          inputValue={bird.selectionLabel}
          onInputChange={handleCombinedInputChange}
          onSelect={handleCombinedSelect}
          placeholder="ex: 002/02 — orange face green"
          disabled={comboDisabled}
          hasError={!!birdErrors.selection}
        />
        {birdErrors.selection && <p className={errCls}>{birdErrors.selection}</p>}
      </div>

      {/* Anilha */}
      <div>
        <span className={labelCls}>Anilha *</span>
        <input
          type="text"
          value={bird.anilha}
          onChange={(e) => onUpdate({ anilha: e.target.value })}
          placeholder="AOB PT STAM 001 FNP26 5.0"
          minLength={25}
          className={`${inputCls} ${inputBorder(!!birdErrors.anilha)}`}
        />
        {birdErrors.anilha && <p className={errCls}>{birdErrors.anilha}</p>}
      </div>
    </div>
  );
}

export function InscricaoConvoyageForm({
  siteName: _siteName,
  contactEmail,
  activeYear,
}: {
  siteName: string;
  contactEmail: string | null;
  activeYear: ConvoyageActiveYearDto;
}) {
  const router = useRouter();
  const [birds, setBirds] = useState<BirdState[]>([makeBird()]);
  const [birdErrors, setBirdErrors] = useState<Record<number, Record<string, string>>>({});
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [state, setState] = useState<"idle" | "sending" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();
  const [token, setToken] = useState<string>();
  const widgetContainer = useRef<HTMLDivElement>(null);
  const widgetId = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (!TURNSTILE_SITEKEY || !widgetContainer.current) return;
    let cancelled = false;
    const mount = () => {
      if (cancelled || !widgetContainer.current || !window.turnstile) return;
      if (widgetId.current) return;
      widgetId.current = window.turnstile.render(widgetContainer.current, {
        sitekey: TURNSTILE_SITEKEY,
        theme: "light",
        callback: (t) => setToken(t),
      });
    };
    if (window.turnstile) mount();
    else {
      const s = document.createElement("script");
      s.src = "https://challenges.cloudflare.com/turnstile/v0/api.js";
      s.async = true;
      s.defer = true;
      s.onload = mount;
      document.head.appendChild(s);
    }
    return () => {
      cancelled = true;
    };
  }, []);

  function updateBird(idx: number, update: Partial<BirdState>) {
    setBirds((prev) => prev.map((b, i) => (i === idx ? { ...b, ...update } : b)));
  }

  function addBird() {
    setBirds((prev) => [...prev, makeBird()]);
  }

  function removeBird(idx: number) {
    setBirds((prev) => prev.filter((_, i) => i !== idx));
    setBirdErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        const ki = parseInt(k, 10);
        if (ki < idx) next[ki] = v;
        else if (ki > idx) next[ki - 1] = v;
      });
      return next;
    });
  }

  function onFieldInvalid(e: React.FormEvent<HTMLInputElement>) {
    e.preventDefault();
    const name = e.currentTarget.name;
    const msg = e.currentTarget.validity.valueMissing ? "Campo obrigatório" : "Formato inválido";
    setFieldErrors((prev) => ({ ...prev, [name]: msg }));
  }

  function clearField(name: string) {
    setFieldErrors((prev) => {
      const { [name]: _, ...rest } = prev;
      return rest;
    });
  }

  const err = (name: string) =>
    fieldErrors[name] ? <p className={errCls}>{fieldErrors[name]}</p> : null;

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const raw = new FormData(e.currentTarget);

    // Validate main fields
    const newFieldErrors: Record<string, string> = {};
    const nomeCompleto = String(raw.get("nomeCompleto") ?? "").trim();
    const email = String(raw.get("email") ?? "").trim();
    const pais = String(raw.get("pais") ?? "").trim();
    const localRecolhaId = parseInt(String(raw.get("localRecolhaId") ?? ""), 10);
    if (!nomeCompleto) newFieldErrors["nomeCompleto"] = "Campo obrigatório";
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) newFieldErrors["email"] = "Email inválido";
    if (!pais) newFieldErrors["pais"] = "Campo obrigatório";
    if (isNaN(localRecolhaId)) newFieldErrors["localRecolhaId"] = "Selecione um local de recolha";
    if (!raw.get("aceitouRegulamento")) newFieldErrors["aceitouRegulamento"] = "Campo obrigatório";
    if (Object.keys(newFieldErrors).length > 0) {
      setFieldErrors(newFieldErrors);
      return;
    }

    // Validate each bird
    const newBirdErrors: Record<number, Record<string, string>> = {};
    let birdValid = true;

    for (let i = 0; i < birds.length; i++) {
      const b = birds[i];
      const be: Record<string, string> = {};
      if (!b.species) be.species = "Campo obrigatório";
      if (!b.type) be.type = "Campo obrigatório";
      if (!b.code || !b.mutation) be.selection = "Selecione uma opção da lista";
      if (!b.anilha.trim()) be.anilha = "Campo obrigatório";
      else if (b.anilha.trim().length < 25) be.anilha = "Mínimo de 25 caracteres — ex: AOB PT STAM 001 FNP26 5.0";
      if (Object.keys(be).length > 0) {
        newBirdErrors[i] = be;
        birdValid = false;
      }
    }

    setBirdErrors(newBirdErrors);

    if (!birdValid) {
      const firstIdx = parseInt(Object.keys(newBirdErrors)[0], 10);
      document.getElementById(`bird-${firstIdx}`)?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }

    setState("sending");
    setErrorMsg(undefined);

    const body = {
      nomeCompleto: String(raw.get("nomeCompleto") ?? "").trim(),
      email: String(raw.get("email") ?? "").trim(),
      telefone: String(raw.get("telefone") ?? "").trim() || undefined,
      pais: String(raw.get("pais") ?? "").trim(),
      numeroSocioBva: String(raw.get("numeroSocioBva") ?? "").trim() || undefined,
      numeroStam: String(raw.get("numeroStam") ?? "").trim() || undefined,
      localRecolhaId,
      aceitouRegulamento: true,
      aves: birds.map((b) => ({
        serie: b.code,
        especieMutacao: b.mutation,
        especie: b.species,
        tipoClasse: b.type,
        anilha: b.anilha.trim(),
      })),
      turnstileToken: token,
    };

    const res = await api.submitInscricaoConvoyage(body);
    if (res.ok) {
      router.push(`/inscricao-convoyage/obrigado?id=${res.submissionId ?? ""}`);
      return;
    }
    setState("error");
    setErrorMsg(res.error ?? "Erro desconhecido");
    if (window.turnstile && widgetId.current) window.turnstile.reset(widgetId.current);
    setToken(undefined);
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-6">
      {/* 1. Dados do criador */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>1. Dados do criador</h2>
        <p className="mt-1 text-sm text-ink-500">Campos com * são obrigatórios.</p>

        <div className="mt-5 grid gap-4 md:grid-cols-2">
          <label className="block md:col-span-2">
            <span className={labelCls}>Nome completo *</span>
            <input
              required
              name="nomeCompleto"
              autoComplete="name"
              onInvalid={onFieldInvalid}
              onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["nomeCompleto"])}`}
            />
            {err("nomeCompleto")}
          </label>

          <label className="block">
            <span className={labelCls}>País *</span>
            <input
              required
              name="pais"
              autoComplete="country-name"
              onInvalid={onFieldInvalid}
              onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["pais"])}`}
            />
            {err("pais")}
          </label>

          <label className="block">
            <span className={labelCls}>Email *</span>
            <input
              required
              type="email"
              name="email"
              autoComplete="email"
              onInvalid={onFieldInvalid}
              onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["email"])}`}
            />
            {err("email")}
          </label>

          <label className="block">
            <span className={labelCls}>Telefone</span>
            <input
              name="telefone"
              type="tel"
              autoComplete="tel"
              className={`${inputCls} border-ink-900/15 focus:border-brand-500`}
            />
          </label>

          <label className="block">
            <span className={labelCls}>Nº sócio BVA</span>
            <input
              name="numeroSocioBva"
              className={`${inputCls} border-ink-900/15 focus:border-brand-500`}
            />
          </label>

          <label className="block">
            <span className={labelCls}>Nº STAM</span>
            <input
              name="numeroStam"
              className={`${inputCls} border-ink-900/15 focus:border-brand-500`}
            />
          </label>

          <label className="block md:col-span-2">
            <span className={labelCls}>Local de recolha *</span>
            <select
              name="localRecolhaId"
              onChange={(e) => clearField("localRecolhaId")}
              className={`${inputCls} ${inputBorder(!!fieldErrors["localRecolhaId"])}`}
              defaultValue=""
            >
              <option value="" disabled>— selecionar —</option>
              {activeYear.collectionPoints.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}{p.location ? ` (${p.location})` : ""}
                </option>
              ))}
            </select>
            {err("localRecolhaId")}
          </label>
        </div>
      </section>

      {/* 2. Aves inscritas */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>2. Aves inscritas</h2>
        <p className="mt-1 text-sm text-ink-500">
          Adicione todas as aves que pretende inscrever. Escolha a espécie e o tipo primeiro; depois
          pode pesquisar pelo código ou pela mutação — cada um preenche o outro automaticamente.
        </p>

        <div className="mt-5 flex flex-col gap-4">
          {birds.map((b, i) => (
            <div id={`bird-${i}`} key={b.id}>
              <BirdCard
                bird={b}
                idx={i}
                onUpdate={(u) => updateBird(i, u)}
                onRemove={() => removeBird(i)}
                birdErrors={birdErrors[i] ?? {}}
                canRemove={birds.length > 1}
              />
            </div>
          ))}
        </div>

        <button
          type="button"
          onClick={addBird}
          className="mt-4 inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white px-4 py-2 text-sm font-medium text-brand-700 shadow-sm transition hover:bg-brand-500/10"
        >
          <span className="text-base leading-none">+</span>
          Adicionar ave
        </button>
      </section>

      {/* 3. Declaração */}
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>3. Declaração</h2>
        <p className="mt-2 text-sm leading-relaxed text-ink-700">
          Declaro que os dados acima são correctos e que aceito o regulamento da convoyage BVA Masters.
        </p>
        <div className="mt-4">
          <label className="inline-flex cursor-pointer items-start gap-3">
            <input
              required
              type="checkbox"
              name="aceitouRegulamento"
              onInvalid={onFieldInvalid}
              onChange={(e) => {
                if (e.currentTarget.checked) clearField("aceitouRegulamento");
              }}
              className="mt-1 h-4 w-4 accent-brand-500"
            />
            <span className="font-medium text-ink-900">
              Li e aceito o regulamento da convoyage *
            </span>
          </label>
          {err("aceitouRegulamento")}
        </div>
      </section>

      {TURNSTILE_SITEKEY ? (
        <div ref={widgetContainer} />
      ) : (
        <p className="rounded-lg border border-amber-300/60 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          NEXT_PUBLIC_TURNSTILE_SITEKEY não configurado — modo dev sem verificação anti-bot.
        </p>
      )}

      {state === "error" && errorMsg && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          {errorMsg}
        </div>
      )}

      <button
        type="submit"
        disabled={state === "sending"}
        className="rounded-full bg-brand-500 px-6 py-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-600 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {state === "sending" ? "A enviar..." : "Submeter inscrição"}
      </button>

      {contactEmail && (
        <p className="text-center text-xs text-ink-500">
          Em caso de dúvida contacte{" "}
          <a className="underline hover:text-brand-700" href={`mailto:${contactEmail}`}>
            {contactEmail}
          </a>
        </p>
      )}
    </form>
  );
}
