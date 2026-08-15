import { API_URL, SITE_SLUG } from "./config";
import type {
  EntryType,
  NomenclatureClassDto,
  NomenclatureGroupDto,
  NomenclatureVersionDto,
  Species,
} from "./api-types";

// Display labels for enum values coming from the API (which serialises them
// as strings).
export const SPECIES_LABELS: Record<Species, string> = {
  Roseicollis: "A. roseicollis",
  Personatus:  "A. personatus",
  Fischeri:    "A. fischeri",
  Nigrigenis:  "A. nigrigenis",
  Lilianae:    "A. lilianae",
  Canus:       "A. canus",
  Taranta:     "A. taranta",
  Pullarius:   "A. pullarius",
};

export const TYPE_LABELS: Record<EntryType, string> = {
  Individual: "Individual (A)",
  Team:       "Team (T)",
  Study:      "Study Group (N)",
};

// A "row" the combobox displays: `code — mutation`.
export type NomenclatureItem = {
  code: string;
  mutation: string;
  display: string;
  entryType: EntryType;
};

export type MutationGroup = { group: string; items: NomenclatureItem[] };

async function fetchVersion(path: string): Promise<NomenclatureVersionDto | null> {
  const res = await fetch(`${API_URL}/api/sites/${SITE_SLUG}${path}`, {
    // Nomenclature rarely changes mid-year but admin edits should reflect
    // within a minute — short revalidate window.
    next: { revalidate: 60 },
    headers: { Accept: "application/json" },
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error(`API ${res.status}: GET ${path}`);
  return res.json() as Promise<NomenclatureVersionDto>;
}

export const nomenclatureApi = {
  active:   () => fetchVersion(`/convoyage/nomenclature/active`),
  byYear:   (year: number) => fetchVersion(`/convoyage/nomenclature/${year}`),
};

// ── Helpers used by the form ────────────────────────────────────────────────

// Flattened list of items available for a given species + entry type.
export function itemsFor(
  version: NomenclatureVersionDto,
  species: Species,
  entryType: EntryType,
): NomenclatureItem[] {
  const out: NomenclatureItem[] = [];
  for (const g of version.groups) {
    if (g.species !== species || g.entryType !== entryType) continue;
    for (const c of g.classes) {
      out.push({ code: c.code, mutation: c.mutation, display: `${c.code} — ${c.mutation}`, entryType });
    }
  }
  return out;
}

// Grouped items suitable for a headered dropdown.
export function groupedItemsFor(
  version: NomenclatureVersionDto,
  species: Species,
  entryType: EntryType,
): MutationGroup[] {
  const buckets = new Map<string, NomenclatureItem[]>();
  const order: string[] = [];
  for (const g of version.groups) {
    if (g.species !== species || g.entryType !== entryType) continue;
    if (g.classes.length === 0) continue;
    let list = buckets.get(g.displayName);
    if (!list) {
      list = [];
      buckets.set(g.displayName, list);
      order.push(g.displayName);
    }
    for (const c of g.classes) {
      list.push({ code: c.code, mutation: c.mutation, display: `${c.code} — ${c.mutation}`, entryType });
    }
  }
  return order.map((name) => ({ group: name, items: buckets.get(name)! }));
}

// Grouped items for the "ave individual" combobox — Individual + Grupo de Estudo
// aparecem misturados; o tipo real fica no `entryType` de cada item.
export function groupedItemsForConcurso(
  version: NomenclatureVersionDto,
  species: Species,
): MutationGroup[] {
  const buckets = new Map<string, NomenclatureItem[]>();
  const order: string[] = [];
  for (const g of version.groups) {
    if (g.species !== species) continue;
    if (g.entryType !== "Individual" && g.entryType !== "Study") continue;
    if (g.classes.length === 0) continue;
    let list = buckets.get(g.displayName);
    if (!list) {
      list = [];
      buckets.set(g.displayName, list);
      order.push(g.displayName);
    }
    for (const c of g.classes) {
      list.push({
        code: c.code,
        mutation: c.mutation,
        display: `${c.code} — ${c.mutation}`,
        entryType: g.entryType,
      });
    }
  }
  return order.map((name) => ({ group: name, items: buckets.get(name)! }));
}

export function findByCode(
  version: NomenclatureVersionDto,
  code: string,
): { class: NomenclatureClassDto; group: NomenclatureGroupDto } | undefined {
  for (const g of version.groups) {
    const cls = g.classes.find((c) => c.code === code);
    if (cls) return { class: cls, group: g };
  }
  return undefined;
}

export function availableEntryTypes(
  version: NomenclatureVersionDto,
  species: Species,
): EntryType[] {
  const seen = new Set<EntryType>();
  for (const g of version.groups) {
    if (g.species === species) seen.add(g.entryType);
  }
  const order: EntryType[] = ["Individual", "Team", "Study"];
  return order.filter((t) => seen.has(t));
}
