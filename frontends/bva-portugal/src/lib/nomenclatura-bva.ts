export type Species =
  | "roseicollis"
  | "personatus"
  | "fischeri"
  | "nigrigenis"
  | "lilianae"
  | "canus"
  | "taranta"
  | "pullarius";

export type EntryType = "individual" | "team" | "study";

export type NomenclaturaEntry = {
  species: Species;
  type: EntryType;
  code: string;
  mutation: string;
};

export const SPECIES_LABELS: Record<Species, string> = {
  roseicollis: "A. roseicollis",
  personatus:  "A. personatus",
  fischeri:    "A. fischeri",
  nigrigenis:  "A. nigrigenis",
  lilianae:    "A. lilianae",
  canus:       "A. canus",
  taranta:     "A. taranta",
  pullarius:   "A. pullarius",
};

export const TYPE_LABELS: Record<EntryType, string> = {
  individual: "Individual (A)",
  team:       "Equipa (T)",
  study:      "Grupo de Estudo (N)",
};

// Helper: expand comma-separated mutations into individual entries
function exp(
  species: Species,
  type: EntryType,
  code: string,
  mutations: string,
): NomenclaturaEntry[] {
  return mutations.split(",").map((m) => ({ species, type, code, mutation: m.trim() }));
}

// Shorthands
const r  = (c: string, m: string) => exp("roseicollis", "individual", c, m);
const per = (c: string, m: string) => exp("personatus",  "individual", c, m);
const fis = (c: string, m: string) => exp("fischeri",    "individual", c, m);
const nig = (c: string, m: string) => exp("nigrigenis",  "individual", c, m);
const lil = (c: string, m: string) => exp("lilianae",    "individual", c, m);
const can = (c: string, m: string) => exp("canus",       "individual", c, m);
const tar = (c: string, m: string) => exp("taranta",     "individual", c, m);
const pul = (c: string, m: string) => exp("pullarius",   "individual", c, m);

// 4 eye-ring species share the same mutation; each gets its own code
function e4(pc: string, fc: string, nc: string, lc: string, m: string) {
  return [...per(pc, m), ...fis(fc, m), ...nig(nc, m), ...lil(lc, m)];
}
// personatus + fischeri only (NYA for nigrigenis / lilianae)
function e2pf(pc: string, fc: string, m: string) {
  return [...per(pc, m), ...fis(fc, m)];
}

// ── A. ROSEICOLLIS ─────────────────────────────────────────────────────────

// 001 GREEN
const roseicollis_001 = [...r("001/01", "green")];

// 002 GREENSERIES
const roseicollis_002 = [
  ...r("002/01", "D green, DD green"),
  ...r("002/02", "orange face green, orange face D green, orange face DD green"),
  ...r("002/03", "pale headed green, pale headed D green, pale headed DD green"),
  ...r("002/04", "opaline green, opaline D green, opaline DD green"),
  ...r("002/05", "orange face opaline green, orange face opaline D green, orange face opaline DD green"),
  ...r("002/06", "pale headed opaline green, pale headed opaline D green, pale headed opaline DD green"),
];

// 003 BLUE
const roseicollis_003 = [
  ...r("003/01", "blue, D blue, DD blue"),
  ...r("003/02", "violet"),
  ...r("003/03", "opaline blue, opaline D blue, opaline DD blue"),
  ...r("003/04", "opaline violet"),
];

// 004 AQUA
const roseicollis_004 = [
  ...r("004/01", "aqua, D aqua, DD aqua"),
  ...r("004/02", "violet factored aqua"),
  ...r("004/03", "opaline aqua, opaline D aqua, opaline DD aqua"),
  ...r("004/04", "opaline violet factored aqua"),
];

// 005 TURQUOISE
const roseicollis_005 = [
  ...r("005/01", "turquoise, D turquoise, DD turquoise"),
  ...r("005/02", "violet turquoise"),
  ...r("005/03", "opaline turquoise, opaline D turquoise, opaline DD turquoise"),
  ...r("005/04", "opaline violet turquoise"),
];

// Helper for the standard 18-entry roseicollis mutation pattern
function ros18(g: string, base: string) {
  return [
    ...r(`${g}/01`, `${base} green, ${base} D green, ${base} DD green`),
    ...r(`${g}/02`, `orange face ${base} green, orange face ${base} D green, orange face ${base} DD green`),
    ...r(`${g}/03`, `pale headed ${base} green, pale headed ${base} D green, pale headed ${base} DD green`),
    ...r(`${g}/04`, `${base} blue, ${base} D blue, ${base} DD blue`),
    ...r(`${g}/05`, `${base} aqua, ${base} D aqua, ${base} DD aqua`),
    ...r(`${g}/06`, `${base} turquoise, ${base} D turquoise, ${base} DD turquoise`),
    ...r(`${g}/07`, `${base} violet`),
    ...r(`${g}/08`, `${base} violet factored aqua`),
    ...r(`${g}/09`, `${base} violet turquoise`),
    ...r(`${g}/10`, `opaline ${base} green, opaline ${base} D green, opaline ${base} DD green`),
    ...r(`${g}/11`, `orange face opaline ${base} green, orange face opaline ${base} D green, orange face opaline ${base} DD green`),
    ...r(`${g}/12`, `pale headed opaline ${base} green, pale headed opaline ${base} D green, pale headed opaline ${base} DD green`),
    ...r(`${g}/13`, `opaline ${base} blue, opaline ${base} D blue, opaline ${base} DD blue`),
    ...r(`${g}/14`, `opaline ${base} aqua, opaline ${base} D aqua, opaline ${base} DD aqua`),
    ...r(`${g}/15`, `opaline ${base} turquoise, opaline ${base} D turquoise, opaline ${base} DD turquoise`),
    ...r(`${g}/16`, `opaline ${base} violet`),
    ...r(`${g}/17`, `opaline ${base} violet factored aqua`),
    ...r(`${g}/18`, `opaline ${base} violet turquoise`),
  ];
}

// 006–009, 011–016: standard 18-entry pattern
const roseicollis_006 = ros18("006", "marbled");
const roseicollis_007 = ros18("007", "dilute");
const roseicollis_008 = ros18("008", "bronze fallow");
const roseicollis_009 = ros18("009", "pale fallow");

// 010 SL INO (21 entries, different structure)
const roseicollis_010 = [
  ...r("010/01", "SL ino green (lutino)"),
  ...r("010/02", "orange face ino green (orange face lutino)"),
  ...r("010/03", "pale headed ino green (pale headed lutino)"),
  ...r("010/04", "SL ino blue"),
  ...r("010/05", "SL ino aqua"),
  ...r("010/06", "SL ino turquoise"),
  ...r("010/07", "opaline-ino green"),
  ...r("010/08", "orange face opaline-ino green"),
  ...r("010/09", "pale headed opaline-ino green"),
  ...r("010/10", "opaline-ino blue"),
  ...r("010/11", "opaline-ino aqua"),
  ...r("010/12", "opaline-ino turquoise"),
  ...r("010/13", "cinnamon-ino green, cinnamon-ino D green, cinnamon-ino DD green"),
  ...r("010/14", "cinnamon-ino blue, cinnamon-ino D blue, cinnamon-ino DD blue"),
  ...r("010/15", "cinnamon-ino aqua, cinnamon-ino D aqua, cinnamon-ino DD aqua"),
  ...r("010/16", "cinnamon-ino turquoise, cinnamon-ino D turquoise, cinnamon-ino DD turquoise"),
  ...r("010/17", "orange face cinnamon-ino green, orange face cinnamon-ino D green, orange face cinnamon-ino DD green"),
  ...r("010/18", "pale headed cinnamon-ino green, pale headed cinnamon-ino D green, pale headed cinnamon-ino DD green"),
  ...r("010/19", "cinnamon-ino violet"),
  ...r("010/20", "cinnamon-ino violet factored aqua"),
  ...r("010/21", "cinnamon-ino violet turquoise"),
];

const roseicollis_011 = ros18("011", "cinnamon");
const roseicollis_012 = ros18("012", "pallid");
const roseicollis_013 = ros18("013", "pale");
const roseicollis_014 = ros18("014", "dominant pied");
const roseicollis_015 = ros18("015", "recessive pied");
const roseicollis_016 = ros18("016", "DM jade");

// 017 MISTY
const roseicollis_017 = [
  ...r("017/01", "DF misty green"),
  ...r("017/02", "DF misty blue"),
  ...r("017/03", "DF misty aqua"),
  ...r("017/04", "DF misty turquoise"),
];

// 018 CRESTED
const roseicollis_018 = [
  ...r("018/01", "crested in combination with birds in group 1 till group 17"),
];

// ── EYE-RING SPECIES SHARED MUTATION HELPERS ──────────────────────────────

// Standard 6 entries for greenseries of eye-ring species (shared)
function eyering_greenseries(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "D green, DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "slaty green"),
    ...e2pf(`${pg}/05`, `${fg}/05`, "opaline green, opaline D green, opaline DD green"),
    ...e2pf(`${pg}/07`, `${fg}/07`, "opaline slaty green"),
    // fischeri-only (orange face variants accepted for fischeri)
    ...fis(`${fg}/02`, "orange face green, orange face D green, orange face DD green"),
    ...fis(`${fg}/06`, "orange face opaline green, orange face opaline D green, orange face opaline DD green"),
  ];
}

function eyering_blue(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "blue, D blue, DD blue"),
    ...e4(`${pg}/02`, `${fg}/02`, `${ng}/02`, `${lg}/02`, "violet"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "slaty blue"),
    ...e2pf(`${pg}/04`, `${fg}/04`, "opaline blue, opaline D blue, opaline DD blue"),
    ...e2pf(`${pg}/05`, `${fg}/05`, "opaline violet"),
    ...e2pf(`${pg}/06`, `${fg}/06`, "opaline slaty blue"),
  ];
}

function eyering_blue1blue2(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "Blue1Blue2, D Blue1Blue2, DD Blue1Blue2"),
    ...e4(`${pg}/02`, `${fg}/02`, `${ng}/02`, `${lg}/02`, "violet Blue1Blue2"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "slaty Blue1Blue2"),
    ...e2pf(`${pg}/04`, `${fg}/04`, "opaline Blue1Blue2, opaline D Blue1Blue2, opaline DD Blue1Blue2"),
    ...e2pf(`${pg}/05`, `${fg}/05`, "opaline violet Blue1Blue2"),
    ...e2pf(`${pg}/06`, `${fg}/06`, "opaline slaty Blue1Blue2"),
  ];
}

function eyering_pastel(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "pastel green, pastel D green, pastel DD green"),
    ...fis(`${fg}/02`, "orange face pastel green, orange face pastel D green, orange face pastel DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "pastel blue, pastel D blue, pastel DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "pastel Blue1Blue2, pastel D Blue1Blue2, pastel DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "pastel violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "pastel violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "pastel slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "pastel slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "pastel slaty Blue1Blue2"),
  ];
}

function eyering_edged(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "SF edged green, SF edged D green, SF edged DD green"),
    ...fis(`${fg}/02`, "orange face SF edged green, orange face SF edged D green, orange face SF edged DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "SF edged blue, SF edged D blue, SF edged DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "SF edged Blue1Blue2, SF edged D Blue1Blue2, SF edged DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "SF edged violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "SF edged violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "SF edged slaty green"),
    ...fis(`${fg}/10`, "orange face SF edged slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "SF edged slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "SF edged slaty Blue1Blue2"),
    ...e4(`${pg}/14`, `${fg}/14`, `${ng}/14`, `${lg}/14`, "DF edged green, DF edged D green, DF edged DD green"),
    ...fis(`${fg}/15`, "orange face DF edged green, orange face DF edged D green, orange face DF edged DD green"),
    ...e4(`${pg}/16`, `${fg}/16`, `${ng}/16`, `${lg}/16`, "DF edged blue, DF edged D blue, DF edged DD blue"),
    ...e4(`${pg}/17`, `${fg}/17`, `${ng}/17`, `${lg}/17`, "DF edged Blue1Blue2, DF edged D Blue1Blue2, DF edged DD Blue1Blue2"),
    ...e4(`${pg}/19`, `${fg}/19`, `${ng}/19`, `${lg}/19`, "DF edged violet"),
    ...e4(`${pg}/20`, `${fg}/20`, `${ng}/20`, `${lg}/20`, "DF edged violet Blue1Blue2"),
    ...e4(`${pg}/22`, `${fg}/22`, `${ng}/22`, `${lg}/22`, "DF edged slaty green"),
    ...fis(`${fg}/23`, "orange face DF edged slaty green"),
    ...e4(`${pg}/24`, `${fg}/24`, `${ng}/24`, `${lg}/24`, "DF edged slaty blue"),
    ...e4(`${pg}/25`, `${fg}/25`, `${ng}/25`, `${lg}/25`, "DF edged slaty Blue1Blue2"),
  ];
}

function eyering_dompied(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "dominant pied green, dominant pied D green, dominant pied DD green"),
    ...fis(`${fg}/02`, "orange face dominant pied green, orange face dominant pied D green, orange face dominant pied DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "dominant pied blue, dominant pied D blue, dominant pied DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "dominant pied Blue1Blue2, dominant pied D Blue1Blue2, dominant pied DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "dominant pied violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "dominant pied violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "dominant pied slaty green"),
    ...fis(`${fg}/10`, "orange face dominant pied slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "dominant pied slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "dominant pied slaty Blue1Blue2"),
  ];
}

function eyering_recpied(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "recessive pied green, recessive pied D green, recessive pied DD green"),
    ...fis(`${fg}/02`, "orange face recessive pied green, orange face recessive pied D green, orange face recessive pied DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "recessive pied blue, recessive pied D blue, recessive pied DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "recessive pied Blue1Blue2, recessive pied D Blue1Blue2, recessive pied DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "recessive pied violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "recessive pied violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "recessive pied slaty green"),
    ...fis(`${fg}/10`, "orange face recessive pied slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "recessive pied slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "recessive pied slaty Blue1Blue2"),
  ];
}

function eyering_dilute(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "dilute green, dilute D green, dilute DD green"),
    ...fis(`${fg}/02`, "orange face dilute green, orange face dilute D green, orange face dilute DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "dilute blue, dilute D blue, dilute DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "dilute Blue1Blue2, dilute D Blue1Blue2, dilute DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "dilute violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "dilute violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "dilute slaty green"),
    ...fis(`${fg}/10`, "orange face dilute slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "dilute slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "dilute slaty Blue1Blue2"),
  ];
}

function eyering_misty(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "DF misty green"),
    ...fis(`${fg}/02`, "orange face DF misty green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "DF misty blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "DF misty Blue1Blue2"),
  ];
}

function eyering_euwing(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "SF euwing green, SF euwing D green, SF euwing DD green"),
    ...fis(`${fg}/02`, "orange face SF euwing green, orange face SF euwing D green, orange face SF euwing DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "SF euwing blue, SF euwing D blue, SF euwing DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "SF euwing Blue1Blue2, SF euwing D Blue1Blue2, SF euwing DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "SF euwing violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "SF euwing violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "SF euwing slaty green"),
    ...fis(`${fg}/10`, "orange face SF euwing slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "SF euwing slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "SF euwing slaty Blue1Blue2"),
    ...e4(`${pg}/14`, `${fg}/14`, `${ng}/14`, `${lg}/14`, "DF euwing green, DF euwing D green, DF euwing DD green"),
    ...fis(`${fg}/15`, "orange face DF euwing green, orange face DF euwing D green, orange face DF euwing DD green"),
    ...e4(`${pg}/16`, `${fg}/16`, `${ng}/16`, `${lg}/16`, "DF euwing blue, DF euwing D blue, DF euwing DD blue"),
    ...e4(`${pg}/17`, `${fg}/17`, `${ng}/17`, `${lg}/17`, "DF euwing Blue1Blue2, DF euwing D Blue1Blue2, DF euwing DD Blue1Blue2"),
    ...e4(`${pg}/19`, `${fg}/19`, `${ng}/19`, `${lg}/19`, "DF euwing violet"),
    ...e4(`${pg}/20`, `${fg}/20`, `${ng}/20`, `${lg}/20`, "DF euwing violet Blue1Blue2"),
    ...e4(`${pg}/22`, `${fg}/22`, `${ng}/22`, `${lg}/22`, "DF euwing slaty green"),
    ...fis(`${fg}/23`, "orange face DF euwing slaty green"),
    ...e4(`${pg}/24`, `${fg}/24`, `${ng}/24`, `${lg}/24`, "DF euwing slaty blue"),
    ...e4(`${pg}/25`, `${fg}/25`, `${ng}/25`, `${lg}/25`, "DF euwing slaty Blue1Blue2"),
  ];
}

function eyering_brfallow(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "bronze fallow green, bronze fallow D green, bronze fallow DD green"),
    ...fis(`${fg}/02`, "orange face bronze fallow green, orange face bronze fallow D green, orange face bronze fallow DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "bronze fallow blue, bronze fallow D blue, bronze fallow DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "bronze fallow Blue1Blue2, bronze fallow D Blue1Blue2, bronze fallow DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "bronze fallow violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "bronze fallow violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "bronze fallow slaty green"),
    ...fis(`${fg}/10`, "orange face bronze fallow slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "bronze fallow slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "bronze fallow slaty Blue1Blue2"),
  ];
}

function eyering_palfallow(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "pale fallow green, pale fallow D green, pale fallow DD green"),
    ...fis(`${fg}/02`, "orange face pale fallow green, orange face pale fallow D green, orange face pale fallow DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "pale fallow blue, pale fallow D blue, pale fallow DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "pale fallow Blue1Blue2, pale fallow D Blue1Blue2, pale fallow DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "pale fallow violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "pale fallow violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "pale fallow slaty green"),
    ...fis(`${fg}/10`, "orange face pale fallow slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "pale fallow slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "pale fallow slaty Blue1Blue2"),
  ];
}

function eyering_dunfallow(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "dun fallow green, dun fallow D green, dun fallow DD green"),
    ...fis(`${fg}/02`, "orange face dun fallow green, orange face dun fallow D green, orange face dun fallow DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "dun fallow blue, dun fallow D blue, dun fallow DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "dun fallow Blue1Blue2, dun fallow D Blue1Blue2, dun fallow DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "dun fallow violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "dun fallow violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "dun fallow slaty green"),
    ...fis(`${fg}/10`, "orange face dun fallow slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "dun fallow slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "dun fallow slaty Blue1Blue2"),
  ];
}

function eyering_pale(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "pale green, pale D green, pale DD green"),
    ...fis(`${fg}/02`, "orange face pale green, orange face pale D green, orange face pale DD green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "pale blue, pale D blue, pale DD blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "pale Blue1Blue2, pale D Blue1Blue2, pale DD Blue1Blue2"),
    ...e4(`${pg}/06`, `${fg}/06`, `${ng}/06`, `${lg}/06`, "pale violet"),
    ...e4(`${pg}/07`, `${fg}/07`, `${ng}/07`, `${lg}/07`, "pale violet Blue1Blue2"),
    ...e4(`${pg}/09`, `${fg}/09`, `${ng}/09`, `${lg}/09`, "pale slaty green"),
    ...fis(`${fg}/10`, "orange face pale slaty green"),
    ...e4(`${pg}/11`, `${fg}/11`, `${ng}/11`, `${lg}/11`, "pale slaty blue"),
    ...e4(`${pg}/12`, `${fg}/12`, `${ng}/12`, `${lg}/12`, "pale slaty Blue1Blue2"),
  ];
}

function eyering_dec(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "dec green"),
    ...fis(`${fg}/02`, "orange face dec green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "dec blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "dec Blue1Blue2"),
  ];
}

function eyering_nslino(pg: string, fg: string, ng: string, lg: string) {
  return [
    ...e4(`${pg}/01`, `${fg}/01`, `${ng}/01`, `${lg}/01`, "NSL ino green"),
    ...fis(`${fg}/02`, "orange face NSL ino green"),
    ...e4(`${pg}/03`, `${fg}/03`, `${ng}/03`, `${lg}/03`, "NSL ino blue"),
    ...e4(`${pg}/04`, `${fg}/04`, `${ng}/04`, `${lg}/04`, "NSL ino Blue1Blue2"),
  ];
}

// ── A. PERSONATUS (050-069) ───────────────────────────────────────────────
const personatus_green  = [...e4("050/01", "100/01", "150/01", "200/01", "green")];
const personatus_grseries = [...eyering_greenseries("051", "101", "151", "201")];
const personatus_blue   = [...eyering_blue("052", "102", "152", "202")];
const personatus_b1b2   = [...eyering_blue1blue2("053", "103", "153", "203")];
const personatus_pastel = [...eyering_pastel("055", "105", "155", "205")];
const personatus_edged  = [...eyering_edged("056", "106", "156", "206")];
const personatus_dompied= [...eyering_dompied("057", "107", "157", "207")];
const personatus_recpied= [...eyering_recpied("058", "108", "158", "208")];
const personatus_dilute = [...eyering_dilute("059", "109", "159", "209")];
const personatus_misty  = [...eyering_misty("060", "110", "160", "210")];
const personatus_euwing = [...eyering_euwing("061", "111", "161", "211")];
const personatus_brfal  = [...eyering_brfallow("062", "112", "162", "212")];
const personatus_palfal = [...eyering_palfallow("063", "113", "163", "213")];
const personatus_dunfal = [...eyering_dunfallow("064", "114", "164", "214")];
const personatus_pale   = [...eyering_pale("065", "115", "165", "215")];
const personatus_dec    = [...eyering_dec("066", "116", "166", "216")];
const personatus_nslino = [...eyering_nslino("067", "117", "167", "217")];
const personatus_rare   = [
  ...e4("069/01", "119/01", "169/01", "219/01", "crested in combination with birds in groups 50-67"),
  ...fis("119/02", "opaline crested in combination with birds in groups 100-118"),
];

// ── A. CANUS ──────────────────────────────────────────────────────────────
const canus_green = [
  ...can("250/01", "male green"),
  ...can("250/02", "female green"),
];

// ── A. TARANTA ────────────────────────────────────────────────────────────
const taranta_green = [
  ...tar("300/01", "male green"),
  ...tar("300/02", "female green"),
];
const taranta_greenseries = [
  ...tar("301/01", "male D green, male DD green"),
  ...tar("301/02", "female D green, female DD green"),
  ...tar("301/03", "male DF misty green"),
  ...tar("301/04", "female DF misty green"),
  ...tar("301/05", "male bronze fallow green, bronze fallow D green, bronze fallow DD green"),
  ...tar("301/06", "female bronze fallow green, bronze fallow D green, bronze fallow DD green"),
  ...tar("301/07", "male pale fallow green, pale fallow D green, pale fallow DD green"),
  ...tar("301/08", "female pale fallow green, pale fallow D green, pale fallow DD green"),
];
const taranta_tealseries = [
  ...tar("302/01", "male teal, D teal, DD teal"),
  ...tar("302/02", "female teal, D teal, DD teal"),
  ...tar("302/03", "male DF misty teal"),
  ...tar("302/04", "female DF misty teal"),
  ...tar("302/05", "male bronze fallow teal, bronze fallow D teal, bronze fallow DD teal"),
  ...tar("302/06", "female bronze fallow teal, bronze fallow D teal, bronze fallow DD teal"),
  ...tar("302/07", "male pale fallow teal, pale fallow D teal, pale fallow DD teal"),
  ...tar("302/08", "female pale fallow teal, pale fallow D teal, pale fallow DD teal"),
];

// ── A. PULLARIUS ──────────────────────────────────────────────────────────
const pullarius_green = [
  ...pul("350/01", "male green"),
  ...pul("350/02", "female green"),
];

// ── STUDY GROUPS (N class) ────────────────────────────────────────────────
const study_groups: NomenclaturaEntry[] = [
  { species: "roseicollis", type: "study", code: "400/01", mutation: "nova mutação (N class)" },
  { species: "personatus",  type: "study", code: "400/02", mutation: "nova mutação (N class)" },
  { species: "fischeri",    type: "study", code: "400/03", mutation: "nova mutação (N class)" },
  { species: "nigrigenis",  type: "study", code: "400/04", mutation: "nova mutação (N class)" },
  { species: "lilianae",    type: "study", code: "400/05", mutation: "nova mutação (N class)" },
  { species: "canus",       type: "study", code: "400/06", mutation: "nova mutação (N class)" },
  { species: "taranta",     type: "study", code: "400/07", mutation: "nova mutação (N class)" },
  { species: "pullarius",   type: "study", code: "400/08", mutation: "nova mutação (N class)" },
];

// ── TEAMS (T class) ───────────────────────────────────────────────────────

// Helper for team entries
const t = (species: Species, c: string, m: string) => exp(species, "team", c, m);

const teams_roseicollis: NomenclaturaEntry[] = [
  ...t("roseicollis", "450/01", "green"),
  ...t("roseicollis", "451/01", "A. roseicollis greenseries (group 2)"),
  ...t("roseicollis", "451/02", "A. roseicollis blue (group 3)"),
  ...t("roseicollis", "451/03", "A. roseicollis aqua (group 4)"),
  ...t("roseicollis", "451/04", "A. roseicollis turquoise (group 5)"),
  ...t("roseicollis", "451/05", "A. roseicollis marbled (group 6)"),
  ...t("roseicollis", "451/06", "A. roseicollis dilute (group 7)"),
  ...t("roseicollis", "451/07", "A. roseicollis bronze fallow (group 8)"),
  ...t("roseicollis", "451/08", "A. roseicollis pale fallow (group 9)"),
  ...t("roseicollis", "451/09", "A. roseicollis SL ino (group 10)"),
  ...t("roseicollis", "451/10", "A. roseicollis cinnamon (group 11)"),
  ...t("roseicollis", "451/11", "A. roseicollis pallid (group 12)"),
  ...t("roseicollis", "451/12", "A. roseicollis pale (group 13)"),
  ...t("roseicollis", "451/13", "A. roseicollis dominant pied (group 14)"),
  ...t("roseicollis", "451/14", "A. roseicollis recessive pied (group 15)"),
  ...t("roseicollis", "451/15", "A. roseicollis DM jade (group 16)"),
  ...t("roseicollis", "451/16", "A. roseicollis misty (group 17)"),
  ...t("roseicollis", "451/17", "A. roseicollis crested (group 18)"),
];

const teams_personatus: NomenclaturaEntry[] = [
  ...t("personatus", "452/01", "green"),
  ...t("personatus", "453/01", "birds in greenseries"),
  ...t("personatus", "453/02", "blue"),
  ...t("personatus", "453/03", "Blue1Blue2"),
  ...t("personatus", "453/04", "birds in pastel"),
  ...t("personatus", "453/05", "birds in dominant edged"),
  ...t("personatus", "453/06", "birds in dominant pied"),
  ...t("personatus", "453/07", "birds in recessive pied"),
  ...t("personatus", "453/08", "birds in misty"),
  ...t("personatus", "453/09", "birds in dilute"),
  ...t("personatus", "453/10", "birds in euwing"),
  ...t("personatus", "453/11", "birds in bronze fallow"),
  ...t("personatus", "453/12", "birds in pale fallow"),
  ...t("personatus", "453/13", "birds in dun fallow"),
  ...t("personatus", "453/14", "birds in pale"),
  ...t("personatus", "453/15", "birds in dec"),
  ...t("personatus", "453/16", "birds in NSL ino"),
  ...t("personatus", "453/18", "birds in rare mutations"),
];

const teams_fischeri: NomenclaturaEntry[] = [
  ...t("fischeri", "454/01", "green"),
  ...t("fischeri", "455/01", "birds in greenseries"),
  ...t("fischeri", "455/02", "blue"),
  ...t("fischeri", "455/03", "Blue1Blue2"),
  ...t("fischeri", "455/04", "aqua"),
  ...t("fischeri", "455/05", "birds in pastel"),
  ...t("fischeri", "455/06", "birds in dominant edged"),
  ...t("fischeri", "455/07", "birds in dominant pied"),
  ...t("fischeri", "455/08", "birds in recessive pied"),
  ...t("fischeri", "455/09", "birds in misty"),
  ...t("fischeri", "455/10", "birds in dilute"),
  ...t("fischeri", "455/11", "birds in euwing"),
  ...t("fischeri", "455/12", "birds in bronze fallow"),
  ...t("fischeri", "455/13", "birds in pale fallow"),
  ...t("fischeri", "455/14", "birds in dun fallow"),
  ...t("fischeri", "455/15", "birds in pale"),
  ...t("fischeri", "455/16", "birds in dec"),
  ...t("fischeri", "455/17", "birds in NSL ino"),
  ...t("fischeri", "455/19", "birds in rare mutations"),
];

const teams_nigrigenis: NomenclaturaEntry[] = [
  ...t("nigrigenis", "456/01", "green"),
  ...t("nigrigenis", "457/01", "birds in greenseries"),
  ...t("nigrigenis", "457/02", "blue"),
  ...t("nigrigenis", "457/03", "Blue1Blue2"),
  ...t("nigrigenis", "457/04", "birds in pastel"),
  ...t("nigrigenis", "457/05", "birds in dominant edged"),
  ...t("nigrigenis", "457/06", "birds in dominant pied"),
  ...t("nigrigenis", "457/07", "birds in recessive pied"),
  ...t("nigrigenis", "457/08", "birds in misty"),
  ...t("nigrigenis", "457/09", "birds in dilute"),
  ...t("nigrigenis", "457/10", "birds in euwing"),
  ...t("nigrigenis", "457/11", "birds in bronze fallow"),
  ...t("nigrigenis", "457/12", "birds in pale fallow"),
  ...t("nigrigenis", "457/13", "birds in dun fallow"),
  ...t("nigrigenis", "457/14", "birds in pale"),
  ...t("nigrigenis", "457/15", "birds in dec"),
  ...t("nigrigenis", "457/16", "birds in NSL ino"),
  ...t("nigrigenis", "457/18", "birds in rare mutations"),
];

const teams_lilianae: NomenclaturaEntry[] = [
  ...t("lilianae", "458/01", "green"),
  ...t("lilianae", "459/01", "birds in greenseries"),
  ...t("lilianae", "459/02", "blue"),
  ...t("lilianae", "459/03", "Blue1Blue2"),
  ...t("lilianae", "459/04", "birds in pastel"),
  ...t("lilianae", "459/05", "birds in dominant edged"),
  ...t("lilianae", "459/06", "birds in dominant pied"),
  ...t("lilianae", "459/07", "birds in recessive pied"),
  ...t("lilianae", "459/08", "birds in misty"),
  ...t("lilianae", "459/09", "birds in dilute"),
  ...t("lilianae", "459/10", "birds in euwing"),
  ...t("lilianae", "459/11", "birds in bronze fallow"),
  ...t("lilianae", "459/12", "birds in pale fallow"),
  ...t("lilianae", "459/13", "birds in dun fallow"),
  ...t("lilianae", "459/14", "birds in pale"),
  ...t("lilianae", "459/15", "birds in dec"),
  ...t("lilianae", "459/16", "birds in NSL ino"),
  ...t("lilianae", "459/18", "birds in rare mutations"),
];

const teams_canus_taranta_pullarius: NomenclaturaEntry[] = [
  ...t("canus",     "460/01", "green"),
  ...t("taranta",   "461/01", "green"),
  ...t("taranta",   "462/01", "birds in greenseries"),
  ...t("taranta",   "462/02", "birds in tealseries"),
  ...t("pullarius", "463/01", "green"),
];

// ── FULL ARRAY ────────────────────────────────────────────────────────────

export const NOMENCLATURA: NomenclaturaEntry[] = [
  // A. roseicollis individual
  ...roseicollis_001,
  ...roseicollis_002,
  ...roseicollis_003,
  ...roseicollis_004,
  ...roseicollis_005,
  ...roseicollis_006,
  ...roseicollis_007,
  ...roseicollis_008,
  ...roseicollis_009,
  ...roseicollis_010,
  ...roseicollis_011,
  ...roseicollis_012,
  ...roseicollis_013,
  ...roseicollis_014,
  ...roseicollis_015,
  ...roseicollis_016,
  ...roseicollis_017,
  ...roseicollis_018,

  // A. personatus individual
  ...personatus_green,
  ...personatus_grseries,
  ...personatus_blue,
  ...personatus_b1b2,
  ...personatus_pastel,
  ...personatus_edged,
  ...personatus_dompied,
  ...personatus_recpied,
  ...personatus_dilute,
  ...personatus_misty,
  ...personatus_euwing,
  ...personatus_brfal,
  ...personatus_palfal,
  ...personatus_dunfal,
  ...personatus_pale,
  ...personatus_dec,
  ...personatus_nslino,
  ...personatus_rare,

  // A. canus individual
  ...canus_green,

  // A. taranta individual
  ...taranta_green,
  ...taranta_greenseries,
  ...taranta_tealseries,

  // A. pullarius individual
  ...pullarius_green,

  // Study groups
  ...study_groups,

  // Teams
  ...teams_roseicollis,
  ...teams_personatus,
  ...teams_fischeri,
  ...teams_nigrigenis,
  ...teams_lilianae,
  ...teams_canus_taranta_pullarius,
];

// ── GROUP NAMES (por prefixo de 3 dígitos do código) ─────────────────────

const GROUP_NAMES: Record<string, string> = {
  // A. roseicollis individual
  "001": "Green", "002": "Greenseries", "003": "Blue", "004": "Aqua",
  "005": "Turquoise", "006": "Marbled", "007": "Dilute",
  "008": "Bronze Fallow", "009": "Pale Fallow", "010": "SL Ino",
  "011": "Cinnamon", "012": "Opaline Pallid", "013": "Pale",
  "014": "Dominant Pied", "015": "Recessive Pied", "016": "DM Jade",
  "017": "Misty", "018": "Crested",
  // A. personatus individual
  "050": "Green", "051": "Greenseries", "052": "Blue", "053": "Blue1Blue2",
  "055": "Pastel", "056": "Edged", "057": "Dominant Pied", "058": "Recessive Pied",
  "059": "Dilute", "060": "Misty", "061": "Euwing", "062": "Bronze Fallow",
  "063": "Pale Fallow", "064": "Dun Fallow", "065": "Pale",
  "066": "Dec", "067": "NSL Ino", "069": "Rare",
  // A. fischeri individual
  "100": "Green", "101": "Greenseries", "102": "Blue", "103": "Blue1Blue2",
  "105": "Pastel", "106": "Edged", "107": "Dominant Pied", "108": "Recessive Pied",
  "109": "Dilute", "110": "Misty", "111": "Euwing", "112": "Bronze Fallow",
  "113": "Pale Fallow", "114": "Dun Fallow", "115": "Pale",
  "116": "Dec", "117": "NSL Ino", "119": "Rare",
  // A. nigrigenis individual
  "150": "Green", "151": "Greenseries", "152": "Blue", "153": "Blue1Blue2",
  "155": "Pastel", "156": "Edged", "157": "Dominant Pied", "158": "Recessive Pied",
  "159": "Dilute", "160": "Misty", "161": "Euwing", "162": "Bronze Fallow",
  "163": "Pale Fallow", "164": "Dun Fallow", "165": "Pale",
  "166": "Dec", "167": "NSL Ino", "169": "Rare",
  // A. lilianae individual
  "200": "Green", "201": "Greenseries", "202": "Blue", "203": "Blue1Blue2",
  "205": "Pastel", "206": "Edged", "207": "Dominant Pied", "208": "Recessive Pied",
  "209": "Dilute", "210": "Misty", "211": "Euwing", "212": "Bronze Fallow",
  "213": "Pale Fallow", "214": "Dun Fallow", "215": "Pale",
  "216": "Dec", "217": "NSL Ino", "219": "Rare",
  // A. canus / taranta / pullarius individual
  "250": "Green",
  "300": "Green", "301": "Greenseries", "302": "Tealseries",
  "350": "Green",
  // Study groups
  "400": "Grupo de Estudo",
  // Teams roseicollis
  "450": "Green", "451": "Mutations",
  // Teams personatus
  "452": "Green", "453": "Mutations",
  // Teams fischeri
  "454": "Green", "455": "Mutations",
  // Teams nigrigenis
  "456": "Green", "457": "Mutations",
  // Teams lilianae
  "458": "Green", "459": "Mutations",
  // Teams canus/taranta/pullarius
  "460": "Green", "461": "Green", "462": "Mutations", "463": "Green",
};

export type MutationGroup = { group: string; items: string[] };

export function getGroupedMutations(entries: NomenclaturaEntry[]): MutationGroup[] {
  const groupMap = new Map<string, string[]>();
  const groupOrder: string[] = [];
  for (const entry of entries) {
    const name = GROUP_NAMES[entry.code.slice(0, 3)] ?? entry.code.slice(0, 3);
    if (!groupMap.has(name)) {
      groupMap.set(name, []);
      groupOrder.push(name);
    }
    const list = groupMap.get(name)!;
    if (!list.includes(entry.mutation)) list.push(entry.mutation);
  }
  return groupOrder.map((g) => ({ group: g, items: groupMap.get(g)! }));
}

// ── LOOKUP HELPERS ────────────────────────────────────────────────────────

export function filterNomenclatura(species: Species, type: EntryType): NomenclaturaEntry[] {
  return NOMENCLATURA.filter((e) => e.species === species && e.type === type);
}

export function getUniqueCodes(entries: NomenclaturaEntry[]): string[] {
  return [...new Set(entries.map((e) => e.code))].sort();
}

export function getMutationsForCode(entries: NomenclaturaEntry[], code: string): string[] {
  return entries.filter((e) => e.code === code).map((e) => e.mutation);
}

export function getCodeForMutation(entries: NomenclaturaEntry[], mutation: string): string | undefined {
  return entries.find((e) => e.mutation === mutation)?.code;
}

export function getAvailableTypes(species: Species): EntryType[] {
  const types = new Set(NOMENCLATURA.filter((e) => e.species === species).map((e) => e.type));
  const order: EntryType[] = ["individual", "team", "study"];
  return order.filter((t) => types.has(t));
}
