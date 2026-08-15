"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import type {
  ConvoyageActiveYearDto,
  EntryType,
  NomenclatureVersionDto,
  SocioBvaStatus,
  Species,
} from "@/lib/api-types";
import {
  SPECIES_LABELS,
  groupedItemsFor,
  groupedItemsForConcurso,
  type MutationGroup,
  type NomenclatureItem,
} from "@/lib/nomenclatura-api";

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
  code: string;
  mutation: string;
  selectionLabel: string;
  anilha: string;
};

type TeamState = {
  id: number;
  species: Species | "";
  code: string;
  mutation: string;
  selectionLabel: string;
  anilhas: [string, string, string, string];
};

const TEAM_POSICOES = ["A", "B", "C", "D"] as const;

type SexoOpt = "Macho" | "Femea" | "Indefinido" | "";

type SaleBirdState = {
  id: number;
  freeSpecies: boolean;
  species: Species | "";
  type: EntryType | "";
  code: string;
  mutation: string;
  selectionLabel: string;
  freeSpeciesText: string;
  dataNascimento: string;
  sexo: SexoOpt;
  preco: string;
  anilha: string;
};

type OrigemTransporteOpt = "Compra" | "Vende";

type TransportBirdState = {
  id: number;
  species: Species | "";
  anilha: string;
};

type TransportGroupState = {
  id: number;
  origem: OrigemTransporteOpt | "";
  destinatarioNome: string;
  destinatarioWhatsapp: string;
  destinatarioNotas: string;
  birds: TransportBirdState[];
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

function makeTeam(): TeamState {
  return {
    id: nextId++,
    species: "",
    code: "",
    mutation: "",
    selectionLabel: "",
    anilhas: ["", "", "", ""],
  };
}

function validateTeam(t: TeamState): Record<string, string> {
  const be: Record<string, string> = {};
  if (!t.species) be.species = "Campo obrigatório";
  if (!t.code || !t.mutation) be.selection = "Selecione uma opção da lista";
  for (let i = 0; i < 4; i++) {
    const anilha = t.anilhas[i].trim();
    if (anilha.length < 10)
      be[`anilha${i}`] = "Mínimo de 10 caracteres — ex: AOB PT STAM 001 FNP26 5.0";
  }
  return be;
}

function makeSaleBird(): SaleBirdState {
  return {
    id: nextId++,
    freeSpecies: false,
    species: "",
    type: "Individual",
    code: "",
    mutation: "",
    selectionLabel: "",
    freeSpeciesText: "",
    dataNascimento: "",
    sexo: "",
    preco: "",
    anilha: "",
  };
}

function validateBird(b: BirdState): Record<string, string> {
  const be: Record<string, string> = {};
  if (!b.species) be.species = "Campo obrigatório";
  if (!b.code || !b.mutation || !b.type) be.selection = "Selecione uma opção da lista";
  if (b.anilha.trim().length < 10)
    be.anilha = "Mínimo de 10 caracteres — ex: AOB PT STAM 001 FNP26 5.0";
  return be;
}

function validateSaleBird(b: SaleBirdState): Record<string, string> {
  const be: Record<string, string> = {};
  if (!b.species) be.species = "Campo obrigatório";
  if (b.freeSpecies) {
    if (!b.freeSpeciesText.trim()) be.selection = "Campo obrigatório";
  } else if (!b.mutation) be.selection = "Selecione uma opção da lista";
  if (!b.dataNascimento) be.dataNascimento = "Campo obrigatório";
  if (!b.sexo) be.sexo = "Campo obrigatório";
  const preco = parseFloat(b.preco);
  if (!b.preco || isNaN(preco) || preco < 0) be.preco = "Preço inválido";
  if (b.anilha.trim().length < 10)
    be.anilha = "Mínimo de 10 caracteres — ex: AOB PT STAM 001 FNP26 5.0";
  return be;
}

function makeTransportBird(): TransportBirdState {
  return {
    id: nextId++,
    species: "",
    anilha: "",
  };
}

function makeTransportGroup(): TransportGroupState {
  return {
    id: nextId++,
    origem: "",
    destinatarioNome: "",
    destinatarioWhatsapp: "",
    destinatarioNotas: "",
    birds: [makeTransportBird()],
  };
}

function validateTransportBird(b: TransportBirdState): Record<string, string> {
  const be: Record<string, string> = {};
  if (!b.species) be.species = "Campo obrigatório";
  if (b.anilha.trim().length < 5)
    be.anilha = "Mínimo de 5 caracteres — ex: AOB PT STAM 001 FNP26 5.0";
  return be;
}

function validateTransportGroup(g: TransportGroupState): {
  group: Record<string, string>;
  birds: Record<number, Record<string, string>>;
} {
  const groupErrors: Record<string, string> = {};
  if (!g.origem) groupErrors.origem = "Selecione se é uma compra ou uma venda";
  // Para Compra, o destinatário é o próprio criador (dados auto-preenchidos no envio);
  // só se pedem os campos de destinatário quando é uma Venda.
  if (g.origem === "Vende") {
    if (!g.destinatarioNome.trim()) groupErrors.destinatarioNome = "Campo obrigatório";
    if (!g.destinatarioWhatsapp.trim()) groupErrors.destinatarioWhatsapp = "Campo obrigatório";
  }
  const birds: Record<number, Record<string, string>> = {};
  for (let i = 0; i < g.birds.length; i++) {
    const be = validateTransportBird(g.birds[i]);
    if (Object.keys(be).length > 0) birds[i] = be;
  }
  return { group: groupErrors, birds };
}

type ComboboxItem =
  | { kind: "group"; label: string }
  | { kind: "item"; item: NomenclatureItem };

function buildDisplayList(
  groupedOptions: MutationGroup[] | undefined,
  query: string,
): ComboboxItem[] {
  if (!groupedOptions) return [];
  const q = query.toLowerCase().trim();
  const result: ComboboxItem[] = [];
  for (const { group, items } of groupedOptions) {
    // Match por nome do grupo (ex: "grupo de estudo") mostra todos os items
    // do grupo; caso contrário filtra por display (código — mutação).
    const groupMatches = !!q && group.toLowerCase().includes(q);
    const matching = q
      ? groupMatches
        ? items
        : items.filter((i) => i.display.toLowerCase().includes(q))
      : items;
    if (matching.length === 0) continue;
    if (group) result.push({ kind: "group", label: group });
    for (const it of matching) result.push({ kind: "item", item: it });
  }
  return result;
}

function Combobox({
  groupedOptions,
  inputValue,
  onInputChange,
  onSelect,
  placeholder,
  disabled,
  hasError,
}: {
  groupedOptions?: MutationGroup[];
  inputValue: string;
  onInputChange: (v: string) => void;
  onSelect: (item: NomenclatureItem) => void;
  placeholder?: string;
  disabled?: boolean;
  hasError?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const matchesSelection = useMemo(() => {
    if (!groupedOptions) return false;
    const v = inputValue.trim();
    if (!v) return false;
    return groupedOptions.some((g) => g.items.some((it) => it.display === v));
  }, [groupedOptions, inputValue]);
  const query = matchesSelection ? "" : inputValue;
  const displayList = buildDisplayList(groupedOptions, query);

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
          {displayList.map((entry, i) =>
            entry.kind === "group" ? (
              <li
                key={`grp-${i}`}
                className="select-none px-3.5 pb-0.5 pt-2 text-[10px] font-semibold uppercase tracking-widest text-ink-400"
              >
                {entry.label}
              </li>
            ) : (
              <li
                key={`${entry.item.code}-${entry.item.mutation}`}
                onMouseDown={() => {
                  onSelect(entry.item);
                  setOpen(false);
                }}
                className="cursor-pointer px-3.5 py-1.5 text-sm text-ink-900 hover:bg-brand-500/10"
              >
                {entry.item.display}
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
  version,
  onUpdate,
  onRemove,
  birdErrors,
  canRemove,
}: {
  bird: BirdState;
  idx: number;
  version: NomenclatureVersionDto;
  onUpdate: (update: Partial<BirdState>) => void;
  onRemove: () => void;
  birdErrors: Record<string, string>;
  canRemove: boolean;
}) {
  const combinedGrouped = useMemo(
    () => (bird.species ? groupedItemsForConcurso(version, bird.species as Species) : []),
    [version, bird.species],
  );

  function handleCombinedInputChange(v: string) {
    onUpdate({ selectionLabel: v, code: "", mutation: "", type: "" });
  }

  function handleCombinedSelect(item: NomenclatureItem) {
    onUpdate({
      code: item.code,
      mutation: item.mutation,
      selectionLabel: item.display,
      type: item.entryType,
    });
  }

  const comboDisabled = !bird.species;

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

      <div className="mb-3">
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

function TeamCard({
  team,
  idx,
  version,
  onUpdate,
  onReorder,
  onRemove,
  teamErrors,
  canRemove,
}: {
  team: TeamState;
  idx: number;
  version: NomenclatureVersionDto;
  onUpdate: (update: Partial<TeamState>) => void;
  onReorder: (from: number, to: number) => void;
  onRemove: () => void;
  teamErrors: Record<string, string>;
  canRemove: boolean;
}) {
  const combinedGrouped = useMemo(
    () =>
      team.species
        ? groupedItemsFor(version, team.species as Species, "Team" as EntryType)
        : [],
    [version, team.species],
  );

  function handleCombinedInputChange(v: string) {
    onUpdate({ selectionLabel: v, code: "", mutation: "" });
  }

  function handleCombinedSelect(item: NomenclatureItem) {
    onUpdate({ code: item.code, mutation: item.mutation, selectionLabel: item.display });
  }

  function updateAnilha(i: number, value: string) {
    const next: [string, string, string, string] = [...team.anilhas] as [string, string, string, string];
    next[i] = value;
    onUpdate({ anilhas: next });
  }

  const comboDisabled = !team.species;

  return (
    <div className="rounded-xl border border-indigo-200 bg-indigo-50/40 p-4">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-sm font-semibold text-ink-700">Equipa {idx + 1} · 4 aves</span>
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

      <div className="mb-3">
        <span className={labelCls}>Espécie *</span>
        <select
          value={team.species}
          onChange={(e) =>
            onUpdate({
              species: e.target.value as Species | "",
              code: "",
              mutation: "",
              selectionLabel: "",
            })
          }
          className={`${inputCls} ${inputBorder(!!teamErrors.species)}`}
        >
          <option value="">— selecionar —</option>
          {(Object.entries(SPECIES_LABELS) as [Species, string][]).map(([k, v]) => (
            <option key={k} value={k}>
              {v}
            </option>
          ))}
        </select>
        {teamErrors.species && <p className={errCls}>{teamErrors.species}</p>}
      </div>

      <div className="mb-3">
        <span className={labelCls}>Nº Série/Classe — Espécies e Mutação *</span>
        <Combobox
          groupedOptions={combinedGrouped}
          inputValue={team.selectionLabel}
          onInputChange={handleCombinedInputChange}
          onSelect={handleCombinedSelect}
          placeholder="ex: 451/01 — A. roseicollis greenseries (group 2)"
          disabled={comboDisabled}
          hasError={!!teamErrors.selection}
        />
        {teamErrors.selection && <p className={errCls}>{teamErrors.selection}</p>}
      </div>

      <div>
        <span className={labelCls}>Ordem das gaiolas · A (topo) → D (fundo) na exposição</span>
        <div className="mt-2 flex flex-col gap-2">
          {team.anilhas.map((anilha, i) => {
            const posicao = TEAM_POSICOES[i];
            const isTopo = i === 0;
            const isFundo = i === 3;
            const canUp = i > 0;
            const canDown = i < 3;
            const errKey = `anilha${i}`;
            const hasErr = !!teamErrors[errKey];
            return (
              <div key={i} className="flex items-start gap-2">
                <div className="flex min-w-[64px] flex-col items-center pt-2 text-[10px] font-semibold uppercase tracking-widest text-indigo-700">
                  <span className="text-sm">{posicao}</span>
                  <span className="text-[9px] text-ink-500">
                    {isTopo ? "topo" : isFundo ? "fundo" : " "}
                  </span>
                </div>
                <div className="flex-1">
                  <input
                    type="text"
                    value={anilha}
                    onChange={(e) => updateAnilha(i, e.target.value)}
                    placeholder="AOB PT STAM 001 FNP26 5.0"
                    minLength={25}
                    className={`${inputCls} ${inputBorder(hasErr)}`}
                  />
                  {hasErr && <p className={errCls}>{teamErrors[errKey]}</p>}
                </div>
                <div className="flex flex-col gap-1 pt-1">
                  <button
                    type="button"
                    onClick={() => canUp && onReorder(i, i - 1)}
                    disabled={!canUp}
                    aria-label="Subir"
                    className="rounded border border-ink-900/15 bg-white px-1.5 py-0.5 text-xs text-ink-700 shadow-sm transition hover:bg-indigo-100 disabled:cursor-not-allowed disabled:opacity-30"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    onClick={() => canDown && onReorder(i, i + 1)}
                    disabled={!canDown}
                    aria-label="Descer"
                    className="rounded border border-ink-900/15 bg-white px-1.5 py-0.5 text-xs text-ink-700 shadow-sm transition hover:bg-indigo-100 disabled:cursor-not-allowed disabled:opacity-30"
                  >
                    ↓
                  </button>
                </div>
              </div>
            );
          })}
        </div>
        <p className="mt-2 text-[11px] text-ink-500">
          As anilhas ficam ordenadas na exposição de cima (A) para baixo (D). Use as setas para reordenar.
        </p>
      </div>
    </div>
  );
}

function SaleBirdCard({
  bird,
  idx,
  version,
  onUpdate,
  onRemove,
  birdErrors,
}: {
  bird: SaleBirdState;
  idx: number;
  version: NomenclatureVersionDto;
  onUpdate: (update: Partial<SaleBirdState>) => void;
  onRemove: () => void;
  birdErrors: Record<string, string>;
}) {
  const combinedGrouped = useMemo(
    () =>
      !bird.freeSpecies && bird.species && bird.type
        ? groupedItemsFor(version, bird.species as Species, bird.type as EntryType)
        : [],
    [version, bird.freeSpecies, bird.species, bird.type],
  );

  function handleCombinedInputChange(v: string) {
    onUpdate({ selectionLabel: v, code: "", mutation: "" });
  }

  function handleCombinedSelect(item: NomenclatureItem) {
    onUpdate({ code: item.code, mutation: item.mutation, selectionLabel: item.display });
  }

  const comboDisabled = !bird.species;

  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50/40 p-4">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-sm font-semibold text-ink-700">Ave de venda {idx + 1}</span>
        <button
          type="button"
          onClick={onRemove}
          className="text-xs text-red-600 hover:underline"
        >
          Remover
        </button>
      </div>

      <div className="mb-3">
        <span className={labelCls}>Espécie *</span>
        <select
          value={bird.species}
          onChange={(e) =>
            onUpdate({
              species: e.target.value as Species | "",
              type: "Individual",
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

      <div className="mb-3">
        <span className={labelCls}>Espécie e Mutação *</span>
        <label className="mb-1.5 mt-1 flex cursor-pointer items-center gap-2 text-xs text-ink-700">
          <input
            type="checkbox"
            checked={bird.freeSpecies}
            onChange={(e) =>
              onUpdate({
                freeSpecies: e.target.checked,
                code: "",
                mutation: "",
                selectionLabel: "",
                freeSpeciesText: "",
              })
            }
            className="h-3.5 w-3.5 accent-brand-500"
          />
          Mutação fora do catálogo BVA (texto livre)
        </label>
        {bird.freeSpecies ? (
          <input
            type="text"
            value={bird.freeSpeciesText}
            onChange={(e) => onUpdate({ freeSpeciesText: e.target.value })}
            placeholder="ex: orange face green"
            className={`${inputCls} ${inputBorder(!!birdErrors.selection)}`}
          />
        ) : (
          <Combobox
            groupedOptions={combinedGrouped}
            inputValue={bird.selectionLabel}
            onInputChange={handleCombinedInputChange}
            onSelect={handleCombinedSelect}
            placeholder="ex: 002/02 — orange face green"
            disabled={comboDisabled}
            hasError={!!birdErrors.selection}
          />
        )}
        {birdErrors.selection && <p className={errCls}>{birdErrors.selection}</p>}
      </div>

      <div className="mb-3 grid gap-3 sm:grid-cols-2">
        <div>
          <span className={labelCls}>Data de nascimento *</span>
          <input
            type="text"
            value={bird.dataNascimento}
            onChange={(e) => onUpdate({ dataNascimento: e.target.value })}
            placeholder="ex: 12/03/2024, 2023, primavera 2024"
            className={`${inputCls} ${inputBorder(!!birdErrors.dataNascimento)}`}
          />
          {birdErrors.dataNascimento && <p className={errCls}>{birdErrors.dataNascimento}</p>}
        </div>
        <div>
          <span className={labelCls}>Sexo *</span>
          <select
            value={bird.sexo}
            onChange={(e) => onUpdate({ sexo: e.target.value as SexoOpt })}
            className={`${inputCls} ${inputBorder(!!birdErrors.sexo)}`}
          >
            <option value="">— selecionar —</option>
            <option value="Macho">Macho</option>
            <option value="Femea">Fêmea</option>
            <option value="Indefinido">Indefinido</option>
          </select>
          {birdErrors.sexo && <p className={errCls}>{birdErrors.sexo}</p>}
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <span className={labelCls}>Preço (€) *</span>
          <input
            type="number"
            min={0}
            step="0.01"
            value={bird.preco}
            onChange={(e) => onUpdate({ preco: e.target.value })}
            placeholder="0.00"
            className={`${inputCls} ${inputBorder(!!birdErrors.preco)}`}
          />
          {birdErrors.preco && <p className={errCls}>{birdErrors.preco}</p>}
        </div>
        <div>
          <span className={labelCls}>Anilha *</span>
          <input
            type="text"
            value={bird.anilha}
            onChange={(e) => onUpdate({ anilha: e.target.value })}
            placeholder="ex: AOB PT STAM 001 FNP26 5.0"
            className={`${inputCls} ${inputBorder(!!birdErrors.anilha)}`}
          />
          {birdErrors.anilha && <p className={errCls}>{birdErrors.anilha}</p>}
        </div>
      </div>
    </div>
  );
}

function TransportBirdRow({
  bird,
  idx,
  onUpdate,
  onRemove,
  canRemove,
  birdErrors,
}: {
  bird: TransportBirdState;
  idx: number;
  onUpdate: (update: Partial<TransportBirdState>) => void;
  onRemove: () => void;
  canRemove: boolean;
  birdErrors: Record<string, string>;
}) {
  return (
    <div className="rounded-lg border border-teal-200/70 bg-white p-3">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-semibold uppercase tracking-wide text-teal-700">Ave {idx + 1}</span>
        {canRemove && (
          <button
            type="button"
            onClick={onRemove}
            className="text-xs text-red-600 hover:underline"
          >
            Remover ave
          </button>
        )}
      </div>

      <div className="grid gap-2 sm:grid-cols-2">
        <div>
          <span className={labelCls}>Espécie *</span>
          <select
            value={bird.species}
            onChange={(e) => onUpdate({ species: e.target.value as Species | "" })}
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
          <span className={labelCls}>Anilha *</span>
          <input
            type="text"
            value={bird.anilha}
            onChange={(e) => onUpdate({ anilha: e.target.value })}
            placeholder="ex: AOB PT STAM 001 FNP26 5.0"
            className={`${inputCls} ${inputBorder(!!birdErrors.anilha)}`}
          />
          {birdErrors.anilha && <p className={errCls}>{birdErrors.anilha}</p>}
        </div>
      </div>
    </div>
  );
}

function TransportGroupCard({
  group,
  idx,
  onUpdateGroup,
  onUpdateBird,
  onAddBird,
  onRemoveBird,
  onRemoveGroup,
  groupErrors,
  birdErrors,
}: {
  group: TransportGroupState;
  idx: number;
  onUpdateGroup: (update: Partial<TransportGroupState>) => void;
  onUpdateBird: (birdIdx: number, update: Partial<TransportBirdState>) => void;
  onAddBird: () => void;
  onRemoveBird: (birdIdx: number) => void;
  onRemoveGroup: () => void;
  groupErrors: Record<string, string>;
  birdErrors: Record<number, Record<string, string>>;
}) {
  const trip =
    group.origem === "Compra"
      ? { label: "Bélgica → Portugal", color: "bg-sky-100 text-sky-800 border-sky-300" }
      : group.origem === "Vende"
        ? { label: "Portugal → Bélgica", color: "bg-orange-100 text-orange-800 border-orange-300" }
        : null;

  return (
    <div className="rounded-xl border border-teal-300 bg-teal-50/40 p-4">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="rounded-full bg-teal-600 px-2.5 py-0.5 text-xs font-semibold text-white">
            Destinatário {idx + 1}
          </span>
          <span className="text-xs text-ink-500">
            {group.birds.length} {group.birds.length === 1 ? "ave" : "aves"}
          </span>
          {trip && (
            <span className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium ${trip.color}`}>
              Viagem: {trip.label}
            </span>
          )}
        </div>
        <button
          type="button"
          onClick={onRemoveGroup}
          className="text-xs text-red-600 hover:underline"
        >
          Remover destinatário e todas as aves
        </button>
      </div>

      <div className="mb-3">
        <span className={labelCls}>Tipo de operação *</span>
        <select
          value={group.origem}
          onChange={(e) => onUpdateGroup({ origem: e.target.value as OrigemTransporteOpt | "" })}
          className={`${inputCls} ${inputBorder(!!groupErrors.origem)} md:max-w-md`}
        >
          <option value="">— selecionar —</option>
          <option value="Compra">Compra — aves adquiridas por si (Bélgica → Portugal)</option>
          <option value="Vende">Venda — aves vendidas a terceiros (Portugal → Bélgica)</option>
        </select>
        {groupErrors.origem && <p className={errCls}>{groupErrors.origem}</p>}
      </div>

      {group.origem === "Compra" && (
        <div className="mb-4 rounded-lg border-l-4 border-sky-400 bg-sky-50/60 px-3 py-2 text-xs text-ink-700">
          <b>Compra:</b> as aves vão viajar da Bélgica para Portugal e serão entregues a si
          <b> juntamente com as suas aves de concurso</b>, no regresso da convoyage.
        </div>
      )}

      {group.origem === "Vende" && (
        <div className="mb-4">
          <div className="mb-3 rounded-lg border-l-4 border-orange-400 bg-orange-50/60 px-3 py-2 text-xs text-ink-700">
            <b>Venda:</b> as aves vão viajar de Portugal para a Bélgica e serão entregues à pessoa
            indicada abaixo, que <b>tem de estar presente na chegada (12h hora belga)</b>.
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <span className={labelCls}>Nome do destinatário na Bélgica *</span>
              <input
                type="text"
                value={group.destinatarioNome}
                onChange={(e) => onUpdateGroup({ destinatarioNome: e.target.value })}
                placeholder="Ex: Jan Peeters"
                className={`${inputCls} ${inputBorder(!!groupErrors.destinatarioNome)}`}
              />
              {groupErrors.destinatarioNome && <p className={errCls}>{groupErrors.destinatarioNome}</p>}
            </div>
            <div>
              <span className={labelCls}>WhatsApp do destinatário *</span>
              <input
                type="tel"
                inputMode="tel"
                value={group.destinatarioWhatsapp}
                onChange={(e) => onUpdateGroup({ destinatarioWhatsapp: e.target.value })}
                placeholder="+32 470 123 456"
                className={`${inputCls} font-mono ${inputBorder(!!groupErrors.destinatarioWhatsapp)}`}
              />
              {groupErrors.destinatarioWhatsapp && (
                <p className={errCls}>{groupErrors.destinatarioWhatsapp}</p>
              )}
            </div>
            <div className="sm:col-span-2">
              <span className={labelCls}>Notas do destinatário (opcional)</span>
              <textarea
                value={group.destinatarioNotas}
                onChange={(e) => onUpdateGroup({ destinatarioNotas: e.target.value })}
                placeholder="Notas adicionais sobre o destinatário (ex.: instruções de entrega)"
                rows={2}
                className={`${inputCls} ${inputBorder(false)}`}
              />
            </div>
          </div>
        </div>
      )}

      <div className="flex flex-col gap-3">
        {group.birds.map((b, i) => (
          <TransportBirdRow
            key={b.id}
            bird={b}
            idx={i}
            onUpdate={(u) => onUpdateBird(i, u)}
            onRemove={() => onRemoveBird(i)}
            canRemove={group.birds.length > 1}
            birdErrors={birdErrors[i] ?? {}}
          />
        ))}
      </div>

      <button
        type="button"
        onClick={onAddBird}
        className="mt-3 inline-flex items-center gap-2 rounded-full border border-teal-500/40 bg-white px-3.5 py-1.5 text-xs font-medium text-teal-700 shadow-sm transition hover:bg-teal-500/10"
      >
        <span className="text-base leading-none">+</span>
        Adicionar outra ave para este destinatário
      </button>
    </div>
  );
}

export function InscricaoConvoyageForm({
  siteName: _siteName,
  contactEmail,
  activeYear,
  nomenclature,
}: {
  siteName: string;
  contactEmail: string | null;
  activeYear: ConvoyageActiveYearDto;
  nomenclature: NomenclatureVersionDto;
}) {
  const router = useRouter();
  const [numIndividuais, setNumIndividuais] = useState<number | "">("");
  const [numEquipas, setNumEquipas] = useState<number | "">("");
  const [birds, setBirds] = useState<BirdState[]>([]);
  const [birdErrors, setBirdErrors] = useState<Record<number, Record<string, string>>>({});
  const [teams, setTeams] = useState<TeamState[]>([]);
  const [teamErrors, setTeamErrors] = useState<Record<number, Record<string, string>>>({});
  const [numAvesVenda, setNumAvesVenda] = useState<number | "">("");
  const [saleBirds, setSaleBirds] = useState<SaleBirdState[]>([]);
  const [saleBirdErrors, setSaleBirdErrors] = useState<Record<number, Record<string, string>>>({});
  const [transportGroups, setTransportGroups] = useState<TransportGroupState[]>([]);
  const [transportGroupErrors, setTransportGroupErrors] = useState<
    Record<number, { group: Record<string, string>; birds: Record<number, Record<string, string>> }>
  >({});
  const transportBirdsCount = transportGroups.reduce((n, g) => n + g.birds.length, 0);
  const [socioBvaStatus, setSocioBvaStatus] = useState<SocioBvaStatus | "">("");
  const socioBva = socioBvaStatus !== "NaoSocio";
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [state, setState] = useState<"idle" | "sending" | "error">("idle");
  const [errorMsg, setErrorMsg] = useState<string>();
  const [token, setToken] = useState<string>();
  const widgetContainer = useRef<HTMLDivElement>(null);
  const widgetId = useRef<string | undefined>(undefined);
  const [pendingScrollTarget, setPendingScrollTarget] = useState<string | null>(null);

  useEffect(() => {
    if (!pendingScrollTarget) return;
    const el = document.getElementById(pendingScrollTarget);
    if (el) {
      el.scrollIntoView({ behavior: "smooth", block: "center" });
    }
    setPendingScrollTarget(null);
  }, [pendingScrollTarget, birds, teams, saleBirds, transportGroups]);

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

  function handleNumIndividuaisChange(raw: string) {
    clearField("numIndividuais");
    clearField("numAves");
    if (raw === "") {
      setNumIndividuais("");
      setBirds([]);
      setBirdErrors({});
      return;
    }
    const n = parseInt(raw, 10);
    const clamped = Math.max(0, Math.min(50, isNaN(n) ? 0 : n));
    setNumIndividuais(clamped);
    setBirds((prev) => {
      if (clamped > prev.length)
        return [...prev, ...Array.from({ length: clamped - prev.length }, makeBird)];
      return prev.slice(0, clamped);
    });
    setBirdErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        if (parseInt(k, 10) < clamped) next[parseInt(k, 10)] = v;
      });
      return next;
    });
  }

  function handleNumEquipasChange(raw: string) {
    clearField("numEquipas");
    clearField("numAves");
    if (raw === "") {
      setNumEquipas("");
      setTeams([]);
      setTeamErrors({});
      return;
    }
    const n = parseInt(raw, 10);
    const clamped = Math.max(0, Math.min(20, isNaN(n) ? 0 : n));
    setNumEquipas(clamped);
    setTeams((prev) => {
      if (clamped > prev.length)
        return [...prev, ...Array.from({ length: clamped - prev.length }, makeTeam)];
      return prev.slice(0, clamped);
    });
    setTeamErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        if (parseInt(k, 10) < clamped) next[parseInt(k, 10)] = v;
      });
      return next;
    });
  }

  function updateBird(idx: number, update: Partial<BirdState>) {
    setBirds((prev) => prev.map((b, i) => (i === idx ? { ...b, ...update } : b)));
    setBirdErrors((prevErr) => {
      const current = prevErr[idx];
      if (!current) return prevErr;
      const currentBird = birds[idx];
      if (!currentBird) return prevErr;
      const fresh = validateBird({ ...currentBird, ...update });
      const cleaned: Record<string, string> = {};
      for (const k of Object.keys(current)) if (fresh[k]) cleaned[k] = fresh[k];
      const next = { ...prevErr };
      if (Object.keys(cleaned).length === 0) delete next[idx];
      else next[idx] = cleaned;
      return next;
    });
  }

  function addBird() {
    const newIndex = birds.length;
    setBirds((prev) => [...prev, makeBird()]);
    setNumIndividuais((prev) => (typeof prev === "number" ? prev + 1 : 1));
    setPendingScrollTarget(`bird-${newIndex}`);
  }

  function removeBird(idx: number) {
    setBirds((prev) => prev.filter((_, i) => i !== idx));
    setNumIndividuais((prev) => (typeof prev === "number" ? Math.max(0, prev - 1) : 0));
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

  function updateTeam(idx: number, update: Partial<TeamState>) {
    setTeams((prev) => prev.map((t, i) => (i === idx ? { ...t, ...update } : t)));
    setTeamErrors((prevErr) => {
      const current = prevErr[idx];
      if (!current) return prevErr;
      const currentTeam = teams[idx];
      if (!currentTeam) return prevErr;
      const fresh = validateTeam({ ...currentTeam, ...update });
      const cleaned: Record<string, string> = {};
      for (const k of Object.keys(current)) if (fresh[k]) cleaned[k] = fresh[k];
      const next = { ...prevErr };
      if (Object.keys(cleaned).length === 0) delete next[idx];
      else next[idx] = cleaned;
      return next;
    });
  }

  function reorderTeamAnilha(teamIdx: number, from: number, to: number) {
    setTeams((prev) =>
      prev.map((t, i) => {
        if (i !== teamIdx) return t;
        const next: [string, string, string, string] = [...t.anilhas] as [string, string, string, string];
        const tmp = next[from];
        next[from] = next[to];
        next[to] = tmp;
        return { ...t, anilhas: next };
      }),
    );
    setTeamErrors((prev) => {
      const current = prev[teamIdx];
      if (!current) return prev;
      const fromKey = `anilha${from}`;
      const toKey = `anilha${to}`;
      const swapped: Record<string, string> = { ...current };
      const a = swapped[fromKey];
      const b = swapped[toKey];
      if (a !== undefined) swapped[toKey] = a;
      else delete swapped[toKey];
      if (b !== undefined) swapped[fromKey] = b;
      else delete swapped[fromKey];
      const next = { ...prev };
      next[teamIdx] = swapped;
      return next;
    });
  }

  function addTeam() {
    const newIndex = teams.length;
    setTeams((prev) => [...prev, makeTeam()]);
    setNumEquipas((prev) => (typeof prev === "number" ? prev + 1 : 1));
    setPendingScrollTarget(`team-${newIndex}`);
  }

  function removeTeam(idx: number) {
    setTeams((prev) => prev.filter((_, i) => i !== idx));
    setNumEquipas((prev) => (typeof prev === "number" ? Math.max(0, prev - 1) : 0));
    setTeamErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        const ki = parseInt(k, 10);
        if (ki < idx) next[ki] = v;
        else if (ki > idx) next[ki - 1] = v;
      });
      return next;
    });
  }

  function handleNumAvesVendaChange(raw: string) {
    if (raw === "") {
      setNumAvesVenda("");
      setSaleBirds([]);
      setSaleBirdErrors({});
      return;
    }
    const n = parseInt(raw, 10);
    const clamped = Math.max(0, Math.min(50, isNaN(n) ? 0 : n));
    setNumAvesVenda(clamped);
    setSaleBirds((prev) => {
      if (clamped > prev.length)
        return [...prev, ...Array.from({ length: clamped - prev.length }, makeSaleBird)];
      return prev.slice(0, clamped);
    });
    setSaleBirdErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        if (parseInt(k, 10) < clamped) next[parseInt(k, 10)] = v;
      });
      return next;
    });
  }

  function updateSaleBird(idx: number, update: Partial<SaleBirdState>) {
    setSaleBirds((prev) => prev.map((b, i) => (i === idx ? { ...b, ...update } : b)));
    setSaleBirdErrors((prevErr) => {
      const current = prevErr[idx];
      if (!current) return prevErr;
      const currentBird = saleBirds[idx];
      if (!currentBird) return prevErr;
      const fresh = validateSaleBird({ ...currentBird, ...update });
      const cleaned: Record<string, string> = {};
      for (const k of Object.keys(current)) if (fresh[k]) cleaned[k] = fresh[k];
      const next = { ...prevErr };
      if (Object.keys(cleaned).length === 0) delete next[idx];
      else next[idx] = cleaned;
      return next;
    });
  }

  function addSaleBird() {
    const newIndex = saleBirds.length;
    setSaleBirds((prev) => [...prev, makeSaleBird()]);
    setNumAvesVenda((prev) => (typeof prev === "number" ? prev + 1 : 1));
    setPendingScrollTarget(`sale-bird-${newIndex}`);
  }

  function removeSaleBird(idx: number) {
    setSaleBirds((prev) => prev.filter((_, i) => i !== idx));
    setNumAvesVenda((prev) => (typeof prev === "number" ? Math.max(0, prev - 1) : 0));
    setSaleBirdErrors((prev) => {
      const next: Record<number, Record<string, string>> = {};
      Object.entries(prev).forEach(([k, v]) => {
        const ki = parseInt(k, 10);
        if (ki < idx) next[ki] = v;
        else if (ki > idx) next[ki - 1] = v;
      });
      return next;
    });
  }

  function addTransportGroup() {
    const newIndex = transportGroups.length;
    setTransportGroups((prev) => [...prev, makeTransportGroup()]);
    setPendingScrollTarget(`transport-group-${newIndex}`);
  }

  function removeTransportGroup(idx: number) {
    setTransportGroups((prev) => prev.filter((_, i) => i !== idx));
    setTransportGroupErrors((prev) => {
      const next: typeof prev = {};
      Object.entries(prev).forEach(([k, v]) => {
        const ki = parseInt(k, 10);
        if (ki < idx) next[ki] = v;
        else if (ki > idx) next[ki - 1] = v;
      });
      return next;
    });
  }

  function updateTransportGroup(idx: number, update: Partial<TransportGroupState>) {
    setTransportGroups((prev) => prev.map((g, i) => (i === idx ? { ...g, ...update } : g)));
    setTransportGroupErrors((prevErr) => {
      const current = prevErr[idx];
      if (!current) return prevErr;
      const source = transportGroups[idx];
      if (!source) return prevErr;
      const merged = { ...source, ...update };
      const fresh = validateTransportGroup(merged);
      const cleanedGroup: Record<string, string> = {};
      for (const k of Object.keys(current.group)) if (fresh.group[k]) cleanedGroup[k] = fresh.group[k];
      const next = { ...prevErr };
      if (Object.keys(cleanedGroup).length === 0 && Object.keys(current.birds).length === 0) {
        delete next[idx];
      } else {
        next[idx] = { group: cleanedGroup, birds: current.birds };
      }
      return next;
    });
  }

  function addBirdToGroup(groupIdx: number) {
    setTransportGroups((prev) =>
      prev.map((g, i) => (i === groupIdx ? { ...g, birds: [...g.birds, makeTransportBird()] } : g)),
    );
  }

  function removeBirdFromGroup(groupIdx: number, birdIdx: number) {
    setTransportGroups((prev) =>
      prev.map((g, i) =>
        i === groupIdx ? { ...g, birds: g.birds.filter((_, j) => j !== birdIdx) } : g,
      ),
    );
    setTransportGroupErrors((prev) => {
      const current = prev[groupIdx];
      if (!current) return prev;
      const newBirds: Record<number, Record<string, string>> = {};
      Object.entries(current.birds).forEach(([k, v]) => {
        const ki = parseInt(k, 10);
        if (ki < birdIdx) newBirds[ki] = v;
        else if (ki > birdIdx) newBirds[ki - 1] = v;
      });
      const next = { ...prev };
      if (Object.keys(current.group).length === 0 && Object.keys(newBirds).length === 0) {
        delete next[groupIdx];
      } else {
        next[groupIdx] = { group: current.group, birds: newBirds };
      }
      return next;
    });
  }

  function updateBirdInGroup(groupIdx: number, birdIdx: number, update: Partial<TransportBirdState>) {
    setTransportGroups((prev) =>
      prev.map((g, i) =>
        i === groupIdx
          ? { ...g, birds: g.birds.map((b, j) => (j === birdIdx ? { ...b, ...update } : b)) }
          : g,
      ),
    );
    setTransportGroupErrors((prevErr) => {
      const current = prevErr[groupIdx];
      if (!current) return prevErr;
      const sourceBird = transportGroups[groupIdx]?.birds[birdIdx];
      if (!sourceBird) return prevErr;
      const merged = { ...sourceBird, ...update };
      const fresh = validateTransportBird(merged);
      const currentBirdErr = current.birds[birdIdx];
      if (!currentBirdErr) return prevErr;
      const cleaned: Record<string, string> = {};
      for (const k of Object.keys(currentBirdErr)) if (fresh[k]) cleaned[k] = fresh[k];
      const newBirds = { ...current.birds };
      if (Object.keys(cleaned).length === 0) delete newBirds[birdIdx];
      else newBirds[birdIdx] = cleaned;
      const next = { ...prevErr };
      if (Object.keys(current.group).length === 0 && Object.keys(newBirds).length === 0) {
        delete next[groupIdx];
      } else {
        next[groupIdx] = { group: current.group, birds: newBirds };
      }
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

    const newFieldErrors: Record<string, string> = {};
    const nomeCompleto = String(raw.get("nomeCompleto") ?? "").trim();
    const email = String(raw.get("email") ?? "").trim();
    const pais = String(raw.get("pais") ?? "").trim();
    const telefone = String(raw.get("telefone") ?? "").trim();
    const localRecolhaId = parseInt(String(raw.get("localRecolhaId") ?? ""), 10);
    if (!nomeCompleto) newFieldErrors["nomeCompleto"] = "Campo obrigatório";
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) newFieldErrors["email"] = "Email inválido";
    if (!pais) newFieldErrors["pais"] = "Campo obrigatório";
    if (!telefone) newFieldErrors["telefone"] = "Campo obrigatório";
    const numeroStam = String(raw.get("numeroStam") ?? "").trim();
    if (!numeroStam) newFieldErrors["numeroStam"] = "Campo obrigatório";
    if (isNaN(localRecolhaId)) newFieldErrors["localRecolhaId"] = "Selecione um local de recolha";
    if (!socioBvaStatus) newFieldErrors["socioBvaStatus"] = "Escolha uma das opções";
    if (!raw.get("aceitouRegulamento")) newFieldErrors["aceitouRegulamento"] = "Campo obrigatório";
    if (birds.length === 0 && teams.length === 0 && saleBirds.length === 0 && transportBirdsCount === 0)
      newFieldErrors["numAves"] = "Indique pelo menos 1 ave (concurso, venda ou transporte)";

    const newBirdErrors: Record<number, Record<string, string>> = {};
    for (let i = 0; i < birds.length; i++) {
      const be = validateBird(birds[i]);
      if (Object.keys(be).length > 0) newBirdErrors[i] = be;
    }

    const newTeamErrors: Record<number, Record<string, string>> = {};
    for (let i = 0; i < teams.length; i++) {
      const te = validateTeam(teams[i]);
      if (Object.keys(te).length > 0) newTeamErrors[i] = te;
    }

    const newSaleBirdErrors: Record<number, Record<string, string>> = {};
    for (let i = 0; i < saleBirds.length; i++) {
      const be = validateSaleBird(saleBirds[i]);
      if (Object.keys(be).length > 0) newSaleBirdErrors[i] = be;
    }

    const newTransportGroupErrors: Record<
      number,
      { group: Record<string, string>; birds: Record<number, Record<string, string>> }
    > = {};
    for (let i = 0; i < transportGroups.length; i++) {
      const gErr = validateTransportGroup(transportGroups[i]);
      if (Object.keys(gErr.group).length > 0 || Object.keys(gErr.birds).length > 0) {
        newTransportGroupErrors[i] = gErr;
      }
    }

    const dupMsg = "Anilha duplicada nesta inscrição";
    type AnilhaRef =
      | { kind: "bird"; idx: number }
      | { kind: "team"; idx: number; anilhaIdx: number }
      | { kind: "sale"; idx: number }
      | { kind: "transport"; groupIdx: number; birdIdx: number };
    const anilhaRefs = new Map<string, AnilhaRef[]>();
    const pushAnilha = (value: string, ref: AnilhaRef) => {
      const key = value.trim().toLowerCase();
      if (!key) return;
      const list = anilhaRefs.get(key) ?? [];
      list.push(ref);
      anilhaRefs.set(key, list);
    };
    birds.forEach((b, i) => pushAnilha(b.anilha, { kind: "bird", idx: i }));
    teams.forEach((t, i) =>
      t.anilhas.forEach((a, ai) => pushAnilha(a, { kind: "team", idx: i, anilhaIdx: ai })),
    );
    saleBirds.forEach((b, i) => pushAnilha(b.anilha, { kind: "sale", idx: i }));
    transportGroups.forEach((g, gi) =>
      g.birds.forEach((b, bi) => pushAnilha(b.anilha, { kind: "transport", groupIdx: gi, birdIdx: bi })),
    );

    for (const refs of anilhaRefs.values()) {
      if (refs.length < 2) continue;
      for (const ref of refs) {
        if (ref.kind === "bird") {
          newBirdErrors[ref.idx] = { ...(newBirdErrors[ref.idx] ?? {}), anilha: dupMsg };
        } else if (ref.kind === "team") {
          newTeamErrors[ref.idx] = {
            ...(newTeamErrors[ref.idx] ?? {}),
            [`anilha${ref.anilhaIdx}`]: dupMsg,
          };
        } else if (ref.kind === "sale") {
          newSaleBirdErrors[ref.idx] = { ...(newSaleBirdErrors[ref.idx] ?? {}), anilha: dupMsg };
        } else {
          const existing = newTransportGroupErrors[ref.groupIdx] ?? { group: {}, birds: {} };
          existing.birds = {
            ...existing.birds,
            [ref.birdIdx]: { ...(existing.birds[ref.birdIdx] ?? {}), anilha: dupMsg },
          };
          newTransportGroupErrors[ref.groupIdx] = existing;
        }
      }
    }

    const hasFieldErr = Object.keys(newFieldErrors).length > 0;
    const hasBirdErr = Object.keys(newBirdErrors).length > 0;
    const hasTeamErr = Object.keys(newTeamErrors).length > 0;
    const hasSaleBirdErr = Object.keys(newSaleBirdErrors).length > 0;
    const hasTransportGroupErr = Object.keys(newTransportGroupErrors).length > 0;

    if (hasFieldErr || hasBirdErr || hasTeamErr || hasSaleBirdErr || hasTransportGroupErr) {
      setFieldErrors(newFieldErrors);
      setBirdErrors(newBirdErrors);
      setTeamErrors(newTeamErrors);
      setSaleBirdErrors(newSaleBirdErrors);
      setTransportGroupErrors(newTransportGroupErrors);

      const section1Order = ["nomeCompleto", "pais", "email", "telefone", "numeroStam", "numIndividuais", "numEquipas", "numAves", "localRecolhaId", "socioBvaStatus"];
      const firstSection1 = section1Order.find((f) => f in newFieldErrors);

      let targetEl: HTMLElement | null = null;
      if (firstSection1) {
        targetEl =
          (document.querySelector(`[name="${firstSection1}"]`) as HTMLElement | null) ??
          document.getElementById(`field-${firstSection1}`);
      } else if (hasBirdErr) {
        const firstIdx = parseInt(Object.keys(newBirdErrors)[0], 10);
        targetEl = document.getElementById(`bird-${firstIdx}`);
      } else if (hasTeamErr) {
        const firstIdx = parseInt(Object.keys(newTeamErrors)[0], 10);
        targetEl = document.getElementById(`team-${firstIdx}`);
      } else if (hasSaleBirdErr) {
        const firstIdx = parseInt(Object.keys(newSaleBirdErrors)[0], 10);
        targetEl = document.getElementById(`sale-bird-${firstIdx}`);
      } else if (hasTransportGroupErr) {
        const firstIdx = parseInt(Object.keys(newTransportGroupErrors)[0], 10);
        targetEl = document.getElementById(`transport-group-${firstIdx}`);
      } else if ("aceitouRegulamento" in newFieldErrors) {
        targetEl = document.querySelector(`[name="aceitouRegulamento"]`) as HTMLElement | null;
      }

      targetEl?.scrollIntoView({ behavior: "smooth", block: "center" });
      (targetEl as HTMLInputElement | HTMLSelectElement | null)?.focus?.({ preventScroll: true });
      return;
    }

    setState("sending");
    setErrorMsg(undefined);

    const body = {
      nomeCompleto: String(raw.get("nomeCompleto") ?? "").trim(),
      email: String(raw.get("email") ?? "").trim(),
      telefone,
      pais: String(raw.get("pais") ?? "").trim(),
      numeroStam,
      localRecolhaId,
      aceitouRegulamento: true,
      socioBvaStatus: socioBvaStatus as SocioBvaStatus,
      aves: [
        ...birds.map((b) => ({
          serie: b.code,
          especieMutacao: b.mutation,
          especie: b.species,
          tipoClasse: b.type,
          anilha: b.anilha.trim(),
        })),
        ...teams.flatMap((t) => {
          const equipaId =
            typeof crypto !== "undefined" && crypto.randomUUID
              ? crypto.randomUUID()
              : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
          return t.anilhas.map((anilha, i) => ({
            serie: t.code,
            especieMutacao: t.mutation,
            especie: t.species,
            tipoClasse: "Team" as EntryType,
            anilha: anilha.trim(),
            equipaId,
            posicaoEquipa: TEAM_POSICOES[i],
          }));
        }),
      ],
      avesVenda: saleBirds.map((b) => ({
        especie: b.species,
        tipoClasse: b.type || undefined,
        especieMutacao: b.freeSpecies ? b.freeSpeciesText.trim() : b.mutation,
        especieLivre: b.freeSpecies,
        dataNascimento: b.dataNascimento,
        sexo: b.sexo as "Macho" | "Femea" | "Indefinido",
        preco: parseFloat(b.preco),
        anilha: b.anilha.trim(),
      })),
      avesTransporte: transportGroups.flatMap((g) => {
        // Para Compra, o destinatário é o próprio criador (auto-preenchido).
        const destNome = g.origem === "Compra" ? nomeCompleto : g.destinatarioNome.trim();
        const destWa = g.origem === "Compra" ? telefone : g.destinatarioWhatsapp.trim();
        const destNotas = g.origem === "Compra" ? undefined : g.destinatarioNotas.trim() || undefined;
        return g.birds.map((b) => ({
          especie: b.species,
          origem: g.origem as "Compra" | "Vende",
          anilha: b.anilha.trim(),
          destinatarioNome: destNome,
          destinatarioWhatsapp: destWa,
          destinatarioNotas: destNotas,
        }));
      }),
      turnstileToken: token,
    };

    try {
      const res = await api.submitInscricaoConvoyage(body);
      if (res.ok) {
        const q = new URLSearchParams();
        if (res.submissionId != null) q.set("id", String(res.submissionId));
        if (res.downloadToken) q.set("t", res.downloadToken);
        router.push(`/inscricao-convoyage/obrigado?${q.toString()}`);
        return;
      }
      setState("error");
      setErrorMsg(res.error ?? "Erro desconhecido");
    } catch (err) {
      setState("error");
      setErrorMsg(
        err instanceof Error && err.name === "AbortError"
          ? "O servidor demorou demasiado tempo a responder. Verifica se a inscrição foi registada antes de submeter de novo."
          : "Falha de ligação ao servidor. Verifica a tua Internet e tenta novamente."
      );
    } finally {
      if (window.turnstile && widgetId.current) window.turnstile.reset(widgetId.current);
      setToken(undefined);
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-6">
      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>1. Dados do criador</h2>
        <p className="mt-1 text-sm text-ink-500">Campos com * são obrigatórios.</p>

        <div className="mt-5 grid gap-4 md:grid-cols-2">
          <label className="block md:col-span-2">
            <span className={labelCls}>Nome *</span>
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
              defaultValue="Portugal"
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
            <span className={labelCls}>Telefone *</span>
            <input
              required
              name="telefone"
              type="tel"
              autoComplete="tel"
              onInvalid={onFieldInvalid}
              onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["telefone"])}`}
            />
            {err("telefone")}
          </label>

          <label className="block">
            <span className={labelCls}>Nº STAM *</span>
            <input
              name="numeroStam"
              required
              onInvalid={onFieldInvalid}
              onChange={(e) => clearField(e.currentTarget.name)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["numeroStam"])}`}
            />
            {err("numeroStam")}
          </label>

          <label className="block">
            <span className={labelCls}>Nº aves individuais a concurso *</span>
            <input
              id="field-numIndividuais"
              type="number"
              min={0}
              max={50}
              value={numIndividuais === "" ? "" : numIndividuais}
              onChange={(e) => handleNumIndividuaisChange(e.target.value)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["numAves"])}`}
            />
          </label>

          <label className="block">
            <span className={labelCls}>Nº equipas a concurso *</span>
            <input
              id="field-numEquipas"
              type="number"
              min={0}
              max={20}
              value={numEquipas === "" ? "" : numEquipas}
              onChange={(e) => handleNumEquipasChange(e.target.value)}
              className={`${inputCls} ${inputBorder(!!fieldErrors["numAves"])}`}
            />
            <span className="mt-1 block text-[11px] text-ink-500">Cada equipa = 4 aves.</span>
          </label>

          <label className="block">
            <span className={labelCls}>Nº aves para venda</span>
            <input
              type="number"
              min={0}
              max={50}
              value={numAvesVenda === "" ? "" : numAvesVenda}
              onChange={(e) => handleNumAvesVendaChange(e.target.value)}
              className={`${inputCls} border-ink-900/15 focus:border-brand-500`}
            />
          </label>

          <label className="block">
            <span className={labelCls}>Local de recolha *</span>
            <select
              name="localRecolhaId"
              onChange={() => clearField("localRecolhaId")}
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

          {fieldErrors["numAves"] && (
            <div className="md:col-span-2">
              <p className={errCls}>{fieldErrors["numAves"]}</p>
            </div>
          )}

          <fieldset
            id="field-socioBvaStatus"
            className={`md:col-span-2 rounded-lg border p-4 ${
              fieldErrors["socioBvaStatus"] ? "border-red-500" : "border-ink-900/15"
            }`}
          >
            <legend className={`${labelCls} px-1`}>Situação BVA Portugal *</legend>
            <div className="mt-2 flex flex-col gap-2 text-sm text-ink-800">
              <label className="flex cursor-pointer items-start gap-2">
                <input
                  type="radio"
                  name="socioBvaStatus"
                  value="JaSocio"
                  checked={socioBvaStatus === "JaSocio"}
                  onChange={() => {
                    setSocioBvaStatus("JaSocio");
                    clearField("socioBvaStatus");
                  }}
                  className="mt-1 h-4 w-4 accent-brand-500"
                />
                <span>Sou sócio BVA Portugal com as quotas pagas em dia <span className="text-ink-500">(tarifa de transporte reduzida)</span></span>
              </label>
              <label className="flex cursor-pointer items-start gap-2">
                <input
                  type="radio"
                  name="socioBvaStatus"
                  value="PagaComInscricao"
                  checked={socioBvaStatus === "PagaComInscricao"}
                  onChange={() => {
                    setSocioBvaStatus("PagaComInscricao");
                    clearField("socioBvaStatus");
                  }}
                  className="mt-1 h-4 w-4 accent-brand-500"
                />
                <span>Vou pagar a quota BVA juntamente com esta inscrição de convoyage <span className="text-ink-500">(tarifa de transporte reduzida)</span></span>
              </label>
              <label className="flex cursor-pointer items-start gap-2">
                <input
                  type="radio"
                  name="socioBvaStatus"
                  value="NaoSocio"
                  checked={socioBvaStatus === "NaoSocio"}
                  onChange={() => {
                    setSocioBvaStatus("NaoSocio");
                    clearField("socioBvaStatus");
                  }}
                  className="mt-1 h-4 w-4 accent-brand-500"
                />
                <span>Não sou nem pretendo ser sócio BVA Portugal</span>
              </label>
            </div>
            {err("socioBvaStatus")}
          </fieldset>
        </div>
      </section>

      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>2. Aves inscritas a concurso</h2>
        <p className="mt-1 text-sm text-ink-500">
          Adicione as aves individuais e as equipas. Para cada uma, escolha a espécie primeiro e depois
          pesquise pelo código ou pela mutação. As equipas têm 4 aves ordenadas A→D de cima para baixo
          na exposição.
        </p>
        <p className="mt-2 text-xs font-medium text-ink-700">
          {birds.length} {birds.length === 1 ? "ave individual" : "aves individuais"}
          {" + "}
          {teams.length} {teams.length === 1 ? "equipa" : "equipas"}
          {" · "}
          <span className="font-semibold text-brand-700">
            {birds.length + teams.length * 4} {birds.length + teams.length * 4 === 1 ? "ave" : "aves"} no total
          </span>
        </p>

        <div className="mt-5 flex flex-col gap-4">
          {birds.map((b, i) => (
            <div id={`bird-${i}`} key={`bird-${b.id}`}>
              <BirdCard
                bird={b}
                idx={i}
                version={nomenclature}
                onUpdate={(u) => updateBird(i, u)}
                onRemove={() => removeBird(i)}
                birdErrors={birdErrors[i] ?? {}}
                canRemove={birds.length + teams.length > 1}
              />
            </div>
          ))}
          {teams.map((t, i) => (
            <div id={`team-${i}`} key={`team-${t.id}`}>
              <TeamCard
                team={t}
                idx={i}
                version={nomenclature}
                onUpdate={(u) => updateTeam(i, u)}
                onReorder={(from, to) => reorderTeamAnilha(i, from, to)}
                onRemove={() => removeTeam(i)}
                teamErrors={teamErrors[i] ?? {}}
                canRemove={birds.length + teams.length > 1}
              />
            </div>
          ))}
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={addBird}
            className="inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white px-4 py-2 text-sm font-medium text-brand-700 shadow-sm transition hover:bg-brand-500/10"
          >
            <span className="text-base leading-none">+</span>
            Adicionar ave individual
          </button>
          <button
            type="button"
            onClick={addTeam}
            className="inline-flex items-center gap-2 rounded-full border border-indigo-500/40 bg-white px-4 py-2 text-sm font-medium text-indigo-700 shadow-sm transition hover:bg-indigo-500/10"
          >
            <span className="text-base leading-none">+</span>
            Adicionar equipa
          </button>
        </div>
      </section>

      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>3. Aves para a sala de vendas</h2>
        <p className="mt-1 text-sm text-ink-500">
          Aves que pretende vender na <b>sala de vendas BVA</b> (não entram no concurso). Ocupam
          gaiola no transporte e têm de ter anilha fechada do proprietário.
        </p>
        <p className="mt-2 text-xs font-medium text-ink-700">
          {saleBirds.length} {saleBirds.length === 1 ? "ave para venda" : "aves para venda"}
        </p>

        <div className="mt-5 flex flex-col gap-4">
          {saleBirds.map((b, i) => (
            <div id={`sale-bird-${i}`} key={b.id}>
              <SaleBirdCard
                bird={b}
                idx={i}
                version={nomenclature}
                onUpdate={(u) => updateSaleBird(i, u)}
                onRemove={() => removeSaleBird(i)}
                birdErrors={saleBirdErrors[i] ?? {}}
              />
            </div>
          ))}
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button
            type="button"
            onClick={addSaleBird}
            className="inline-flex items-center gap-2 rounded-full border border-brand-500/40 bg-white px-4 py-2 text-sm font-medium text-brand-700 shadow-sm transition hover:bg-brand-500/10"
          >
            <span className="text-base leading-none">+</span>
            Adicionar ave para venda
          </button>
        </div>
      </section>

      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>4. Aves para transporte (compra/venda)</h2>
        <p className="mt-1 text-sm text-ink-500">
          Aves que <b>compra ou vende</b> e que <b>não entram no concurso BVA Masters nem vão para a sala
          de vendas da BVA</b>. Cada ave ocupa um espaço na transportadora (reservado para ida e volta).
        </p>
        <div className="mt-3 rounded-lg border-l-4 border-amber-500 bg-amber-50 px-4 py-3 text-sm text-ink-800">
          <p className="mb-2"><b>Importante — leia com atenção:</b></p>
          <ul className="mb-0 list-disc space-y-1 pl-5">
            <li>
              <b>Chegada prevista à Bélgica: 12h (hora local belga).</b> O
              destinatário indicado <b>tem obrigatoriamente de estar presente no
              local a essa hora</b> para receber as aves — <b>não temos condições
              para as guardar por mais tempo</b>.
            </li>
            <li>
              <b>Sujeito a validação de espaço:</b> capacidade máxima total é de
              <b> 400 aves para transporte</b>. Prioridade para as <b>aves de exposição</b>
              (concurso e sala de vendas BVA); as aves de compra/venda só são aceites
              se restar espaço. Enviaremos email a confirmar <b>após o fecho das inscrições</b>.
            </li>
          </ul>
        </div>
        <p className="mt-3 text-xs text-ink-500">
          Agrupe as aves pelo <b>destinatário</b> que as vai receber — preencha o nome e o WhatsApp uma vez,
          e depois acrescente rapidamente todas as aves para esse destinatário.
        </p>

        {transportGroups.length === 0 ? (
          <div className="mt-5 rounded-xl border border-dashed border-teal-300 bg-teal-50/30 p-6 text-center">
            <p className="text-sm text-ink-600">
              Ainda não adicionou nenhum destinatário. Se não vai transportar aves de compra/venda, pode ignorar esta secção.
            </p>
          </div>
        ) : (
          <div className="mt-5 flex flex-col gap-4">
            {transportGroups.map((g, i) => (
              <div id={`transport-group-${i}`} key={g.id}>
                <TransportGroupCard
                  group={g}
                  idx={i}
                  onUpdateGroup={(u) => updateTransportGroup(i, u)}
                  onUpdateBird={(bi, u) => updateBirdInGroup(i, bi, u)}
                  onAddBird={() => addBirdToGroup(i)}
                  onRemoveBird={(bi) => removeBirdFromGroup(i, bi)}
                  onRemoveGroup={() => removeTransportGroup(i)}
                  groupErrors={transportGroupErrors[i]?.group ?? {}}
                  birdErrors={transportGroupErrors[i]?.birds ?? {}}
                />
              </div>
            ))}
          </div>
        )}

        <button
          type="button"
          onClick={addTransportGroup}
          className="mt-4 inline-flex items-center gap-2 rounded-full border border-teal-500/40 bg-white px-4 py-2 text-sm font-medium text-teal-700 shadow-sm transition hover:bg-teal-500/10"
        >
          <span className="text-base leading-none">+</span>
          Adicionar destinatário
        </button>
      </section>

      {(() => {
        const nConcurso = birds.length + teams.length * 4;
        const nVenda = saleBirds.length;
        const nTransporte = transportBirdsCount;
        const totalAvesC = nConcurso + nVenda;
        const tarifa = socioBva ? 5.5 : 15.5;
        const tarifaAdq = socioBva ? 15.5 : 20.5;
        const cInscricao = totalAvesC > 0 ? 8.0 : 0;
        const cAves = 3.0 * nConcurso;
        const cGaiolas = 3.0 * totalAvesC;
        const cTransporte = tarifa * totalAvesC;
        const cTransporteAdq = tarifaAdq * nTransporte;
        const cQuota = socioBvaStatus === "PagaComInscricao" ? 40.0 : 0.0;
        const cTotal = cInscricao + cAves + cGaiolas + cTransporte + cTransporteAdq + cQuota;
        return (
          <section className={sectionCls}>
            <h2 className={sectionTitleCls}>5. Resumo de custos</h2>
            <p className="mt-1 text-sm text-ink-500">
              Valores calculados automaticamente com base nas aves inscritas, aves para venda e aves para transporte.
            </p>
            <div className="mt-5 overflow-hidden rounded-lg border border-sand-300">
              <table className="w-full text-sm">
                <tbody className="divide-y divide-sand-200">
                  {cInscricao > 0 && (
                    <tr>
                      <td className="px-4 py-2 text-ink-700">Inscrição na exposição</td>
                      <td className="px-4 py-2 text-right font-mono text-ink-900">{cInscricao.toFixed(2)} €</td>
                    </tr>
                  )}
                  {nConcurso > 0 && (
                    <tr>
                      <td className="px-4 py-2 text-ink-700">
                        Inscrição por ave · {nConcurso} × 3,00 €
                      </td>
                      <td className="px-4 py-2 text-right font-mono text-ink-900">{cAves.toFixed(2)} €</td>
                    </tr>
                  )}
                  {totalAvesC > 0 && (
                    <>
                      <tr>
                        <td className="px-4 py-2 text-ink-700">
                          Aluguer de gaiola · {totalAvesC} × 3,00 €
                        </td>
                        <td className="px-4 py-2 text-right font-mono text-ink-900">{cGaiolas.toFixed(2)} €</td>
                      </tr>
                      <tr>
                        <td className="px-4 py-2 text-ink-700">
                          Transporte {socioBva ? "(sócio BVA)" : "(não-sócio)"} · {totalAvesC} × {tarifa.toFixed(2)} €
                        </td>
                        <td className="px-4 py-2 text-right font-mono text-ink-900">{cTransporte.toFixed(2)} €</td>
                      </tr>
                    </>
                  )}
                  {nTransporte > 0 && (
                    <tr>
                      <td className="px-4 py-2 text-ink-700">
                        Transporte de aves adquiridas/cedidas {socioBva ? "(sócio BVA)" : "(não-sócio)"} · {nTransporte} × {tarifaAdq.toFixed(2)} €
                      </td>
                      <td className="px-4 py-2 text-right font-mono text-ink-900">{cTransporteAdq.toFixed(2)} €</td>
                    </tr>
                  )}
                  {cQuota > 0 && (
                    <tr>
                      <td className="px-4 py-2 text-ink-700">Quota BVA Portugal</td>
                      <td className="px-4 py-2 text-right font-mono text-ink-900">{cQuota.toFixed(2)} €</td>
                    </tr>
                  )}
                  <tr className="bg-brand-500/10">
                    <td className="px-4 py-3 font-semibold text-ink-900">TOTAL a pagar</td>
                    <td className="px-4 py-3 text-right font-mono text-lg font-semibold text-ink-900">
                      {cTotal.toFixed(2)} €
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div className="mt-4 rounded-lg border-l-4 border-amber-500 bg-amber-50 px-4 py-3 text-sm text-ink-800">
              <b>Pagamento:</b> deve ser feito no valor certo, em dinheiro, num envelope fechado, e entregue juntamente com as aves.
            </div>
            {!socioBva && (
              <p className="mt-2 text-xs text-ink-500">
                Se é (ou pretende ser) sócio BVA Portugal, escolha a opção correspondente em <b>Situação BVA Portugal</b> para aplicar a tarifa reduzida de transporte.
              </p>
            )}
          </section>
        );
      })()}

      <section className={sectionCls}>
        <h2 className={sectionTitleCls}>6. Declaração</h2>
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
