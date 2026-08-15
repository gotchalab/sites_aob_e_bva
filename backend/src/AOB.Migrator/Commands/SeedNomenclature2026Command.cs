using AOB.Core.Entities;
using AOB.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AOB.Migrator.Commands;

// Seeds the BVA INT 2026 nomenclature (source: bvamasters.com/pdf/nomINT.pdf).
// Idempotent: refuses to run if the target year already has groups.
public class SeedNomenclature2026Command(AppDbContext db, ILogger<SeedNomenclature2026Command> log)
{
    private const string SiteSlug = "bva";
    private const int TargetYear = 2026;

    public async Task RunAsync()
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Slug == SiteSlug);
        if (site is null)
        {
            log.LogError("Site '{Slug}' não encontrado. Corre 'seed' primeiro.", SiteSlug);
            return;
        }

        var year = await db.ConvoyageYears
            .FirstOrDefaultAsync(y => y.SiteId == site.Id && y.Year == TargetYear);
        if (year is null)
        {
            year = new ConvoyageYear
            {
                SiteId = site.Id,
                Year = TargetYear,
                Description = "BVA Masters 2026",
                IsActive = false,
            };
            db.ConvoyageYears.Add(year);
            await db.SaveChangesAsync();
            log.LogInformation("Criado ConvoyageYear {Year} (id={Id})", TargetYear, year.Id);
        }

        var already = await db.NomenclatureGroups.AnyAsync(g => g.ConvoyageYearId == year.Id);
        if (already)
        {
            log.LogWarning(
                "Ano {Year} já tem grupos de nomenclatura. Apaga-os antes de re-seed.",
                TargetYear);
            return;
        }

        var b = new NomenclatureBuilder(year.Id);
        BuildRoseicollis(b);
        BuildEyeRing(b);
        BuildOthers(b);
        BuildStudyGroups(b);
        BuildTeams(b);

        db.NomenclatureGroups.AddRange(b.Groups);
        await db.SaveChangesAsync();

        var totalClasses = b.Groups.Sum(g => g.Classes.Count);
        log.LogInformation(
            "Seed 2026 concluído: {Groups} grupos, {Classes} classes.",
            b.Groups.Count, totalClasses);
    }

    // ── BUILDER ────────────────────────────────────────────────────────────

    private sealed class NomenclatureBuilder(int convoyageYearId)
    {
        public List<NomenclatureGroup> Groups { get; } = [];
        private int _groupSort;

        public NomenclatureGroup AddGroup(string prefix, string name, SpeciesCode species, EntryTypeCode type)
        {
            var g = new NomenclatureGroup
            {
                ConvoyageYearId = convoyageYearId,
                CodePrefix = prefix,
                DisplayName = name,
                Species = species,
                EntryType = type,
                SortOrder = ++_groupSort,
            };
            Groups.Add(g);
            return g;
        }
    }

    private static string Sub(int n) => n.ToString("D2");

    private static void Add(NomenclatureGroup g, string subCode, string mutations, string? notes = null)
    {
        // A single "line" in the PDF may pack multiple comma-separated variants
        // sharing a code — expand each into its own class row.
        var code = g.CodePrefix + "/" + subCode;
        foreach (var m in mutations.Split(','))
        {
            var mut = m.Trim();
            if (mut.Length == 0) continue;
            g.Classes.Add(new NomenclatureClass
            {
                Code = code,
                Mutation = mut,
                SortOrder = g.Classes.Count + 1,
                IsActive = true,
                Notes = notes,
            });
        }
    }

    // Repeat the standard 18-slot roseicollis mutation layout ("marbled",
    // "dilute", "bronze fallow", …). `opalineSep` = "-" for cinnamon/pallid/pale.
    private static void Ros18(NomenclatureGroup g, string b, string opalineSep = " ")
    {
        var op = "opaline" + opalineSep + b;
        Add(g, "01", $"{b} green, {b} D green, {b} DD green");
        Add(g, "02", $"orange face {b} green, orange face {b} D green, orange face {b} DD green");
        Add(g, "03", $"pale headed {b} green, pale headed {b} D green, pale headed {b} DD green");
        Add(g, "04", $"{b} blue, {b} D blue, {b} DD blue");
        Add(g, "05", $"{b} aqua, {b} D aqua, {b} DD aqua");
        Add(g, "06", $"{b} turquoise, {b} D turquoise, {b} DD turquoise");
        Add(g, "07", $"{b} violet");
        Add(g, "08", $"{b} violet factored aqua");
        Add(g, "09", $"{b} violet turquoise");
        Add(g, "10", $"{op} green, {op} D green, {op} DD green");
        Add(g, "11", $"orange face {op} green, orange face {op} D green, orange face {op} DD green");
        Add(g, "12", $"pale headed {op} green, pale headed {op} D green, pale headed {op} DD green");
        Add(g, "13", $"{op} blue, {op} D blue, {op} DD blue");
        Add(g, "14", $"{op} aqua, {op} D aqua, {op} DD aqua");
        Add(g, "15", $"{op} turquoise, {op} D turquoise, {op} DD turquoise");
        Add(g, "16", $"{op} violet");
        Add(g, "17", $"{op} violet factored aqua");
        Add(g, "18", $"{op} violet turquoise");
    }

    // ── ROSEICOLLIS ────────────────────────────────────────────────────────

    private static void BuildRoseicollis(NomenclatureBuilder b)
    {
        var g001 = b.AddGroup("001", "Green", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g001, "01", "green");

        var g002 = b.AddGroup("002", "Greenseries", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g002, "01", "D green, DD green");
        Add(g002, "02", "orange face green, orange face D green, orange face DD green");
        Add(g002, "03", "pale headed green, pale headed D green, pale headed DD green");
        Add(g002, "04", "opaline green, opaline D green, opaline DD green");
        Add(g002, "05", "orange face opaline green, orange face opaline D green, orange face opaline DD green");
        Add(g002, "06", "pale headed opaline green, pale headed opaline D green, pale headed opaline DD green");

        var g003 = b.AddGroup("003", "Blue", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g003, "01", "blue, D blue, DD blue");
        Add(g003, "02", "violet");
        Add(g003, "03", "opaline blue, opaline D blue, opaline DD blue");
        Add(g003, "04", "opaline violet");

        var g004 = b.AddGroup("004", "Aqua", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g004, "01", "aqua, D aqua, DD aqua");
        Add(g004, "02", "violet factored aqua");
        Add(g004, "03", "opaline aqua, opaline D aqua, opaline DD aqua");
        Add(g004, "04", "opaline violet factored aqua");

        var g005 = b.AddGroup("005", "Turquoise", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g005, "01", "turquoise, D turquoise, DD turquoise");
        Add(g005, "02", "violet turquoise");
        Add(g005, "03", "opaline turquoise, opaline D turquoise, opaline DD turquoise");
        Add(g005, "04", "opaline violet turquoise");

        Ros18(b.AddGroup("006", "Marbled",       SpeciesCode.Roseicollis, EntryTypeCode.Individual), "marbled");
        Ros18(b.AddGroup("007", "Dilute",        SpeciesCode.Roseicollis, EntryTypeCode.Individual), "dilute");
        Ros18(b.AddGroup("008", "Bronze Fallow", SpeciesCode.Roseicollis, EntryTypeCode.Individual), "bronze fallow");
        Ros18(b.AddGroup("009", "Pale Fallow",   SpeciesCode.Roseicollis, EntryTypeCode.Individual), "pale fallow");

        var g010 = b.AddGroup("010", "SL Ino", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g010, "01", "SL ino green (lutino)");
        Add(g010, "02", "orange face ino green (orange face lutino)");
        Add(g010, "03", "pale headed ino green (pale headed lutino)");
        Add(g010, "04", "SL ino blue");
        Add(g010, "05", "SL ino aqua");
        Add(g010, "06", "SL ino turquoise");
        Add(g010, "07", "opaline-ino green");
        Add(g010, "08", "orange face opaline-ino green");
        Add(g010, "09", "pale headed opaline-ino green");
        Add(g010, "10", "opaline-ino blue");
        Add(g010, "11", "opaline-ino aqua");
        Add(g010, "12", "opaline-ino turquoise");
        Add(g010, "13", "cinnamon-ino green, cinnamon-ino D green, cinnamon-ino DD green");
        Add(g010, "14", "cinnamon-ino blue, cinnamon-ino D blue, cinnamon-ino DD blue");
        Add(g010, "15", "cinnamon-ino aqua, cinnamon-ino D aqua, cinnamon-ino DD aqua");
        Add(g010, "16", "cinnamon-ino turquoise, cinnamon-ino D turquoise, cinnamon-ino DD turquoise");
        Add(g010, "17", "orange face cinnamon-ino green, orange face cinnamon-ino D green, orange face cinnamon-ino DD green");
        Add(g010, "18", "pale headed cinnamon-ino green, pale headed cinnamon-ino D green, pale headed cinnamon-ino DD green");
        Add(g010, "19", "cinnamon-ino violet");
        Add(g010, "20", "cinnamon-ino violet factored aqua");
        Add(g010, "21", "cinnamon-ino violet turquoise");

        Ros18(b.AddGroup("011", "Cinnamon", SpeciesCode.Roseicollis, EntryTypeCode.Individual), "cinnamon", "-");
        Ros18(b.AddGroup("012", "Pallid",   SpeciesCode.Roseicollis, EntryTypeCode.Individual), "pallid",   "-");
        Ros18(b.AddGroup("013", "Pale",     SpeciesCode.Roseicollis, EntryTypeCode.Individual), "pale",     "-");
        Ros18(b.AddGroup("014", "Dominant Pied",  SpeciesCode.Roseicollis, EntryTypeCode.Individual), "dominant pied");
        Ros18(b.AddGroup("015", "Recessive Pied", SpeciesCode.Roseicollis, EntryTypeCode.Individual), "recessive pied");
        Ros18(b.AddGroup("016", "DM jade",        SpeciesCode.Roseicollis, EntryTypeCode.Individual), "DM jade");

        var g017 = b.AddGroup("017", "Misty", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g017, "01", "DF misty green");
        Add(g017, "02", "DF misty blue");
        Add(g017, "03", "DF misty aqua");
        Add(g017, "04", "DF misty turquoise");

        var g018 = b.AddGroup("018", "Crested", SpeciesCode.Roseicollis, EntryTypeCode.Individual);
        Add(g018, "01", "crested in combination with birds in group 1 till group 17");
    }

    // ── EYE-RING (personatus/fischeri/nigrigenis/lilianae) ─────────────────
    //
    // Each shared group in the PDF spans four species with staggered sub-codes;
    // fischeri-only variants sit in the same code space. We expand each
    // species' group separately since GROUP_NAMES are per species.

    // Slots 1-13 of the "std26" layout. Fischeri-only slots (marked *) are
    // omitted when generating for non-fischeri species.
    private static readonly (int Slot, bool FischeriOnly, string Suffix)[] Std13 =
    {
        (1,  false, ""),                          // <base> green (D/DD)
        (2,  true,  "orange face "),              // orange face <base> green (D/DD)
        (3,  false, ""),                          // <base> blue (D/DD)
        (4,  false, ""),                          // <base> Blue1Blue2 (D/DD)
        (5,  true,  ""),                          // <base> aqua (D/DD)
        (6,  false, ""),                          // <base> violet
        (7,  false, ""),                          // <base> violet Blue1Blue2
        (8,  true,  ""),                          // <base> violet factored D aqua
        (9,  false, ""),                          // <base> slaty green
        (10, true,  "orange face "),              // orange face <base> slaty green
        (11, false, ""),                          // <base> slaty blue
        (12, false, ""),                          // <base> slaty Blue1Blue2
        (13, true,  ""),                          // <base> slaty aqua
    };

    // Populate a "std26" group (pastel, dominant pied, recessive pied, dilute,
    // bronze/pale/dun fallow, pale). `species` chooses which slots to emit —
    // fischeri gets all 26; the others get the ~7 non-fischeri-only slots.
    private static void EyeringStd26(NomenclatureGroup g, string b, SpeciesCode species, string opalineSep = " ")
    {
        var op = "opaline" + opalineSep + b;
        bool isFischeri = species == SpeciesCode.Fischeri;

        // Slots 01-13 (base)
        Emit(1,  $"{b} green, {b} D green, {b} DD green");
        Emit(2,  $"orange face {b} green, orange face {b} D green, orange face {b} DD green");
        Emit(3,  $"{b} blue, {b} D blue, {b} DD blue");
        Emit(4,  $"{b} Blue1Blue2, {b} D Blue1Blue2, {b} DD Blue1Blue2");
        Emit(5,  $"{b} aqua, {b} D aqua, {b} DD aqua");
        Emit(6,  $"{b} violet");
        Emit(7,  $"{b} violet Blue1Blue2");
        Emit(8,  $"{b} violet factored D aqua");
        Emit(9,  $"{b} slaty green");
        Emit(10, $"orange face {b} slaty green");
        Emit(11, $"{b} slaty blue");
        Emit(12, $"{b} slaty Blue1Blue2");
        Emit(13, $"{b} slaty aqua");
        // Slots 14-26 (opaline, fischeri-only)
        Emit(14, $"{op} green, {op} D green, {op} DD green");
        Emit(15, $"orange face {op} green, orange face {op} D green, orange face {op} DD green");
        Emit(16, $"{op} blue, {op} D blue, {op} DD blue");
        Emit(17, $"{op} Blue1Blue2, {op} D Blue1Blue2, {op} DD Blue1Blue2");
        Emit(18, $"{op} aqua, {op} D aqua, {op} DD aqua");
        Emit(19, $"{op} violet");
        Emit(20, $"{op} violet Blue1Blue2");
        Emit(21, $"{op} violet factored D aqua");
        Emit(22, $"{op} slaty green");
        Emit(23, $"orange face {op} slaty green");
        Emit(24, $"{op} slaty blue");
        Emit(25, $"{op} slaty Blue1Blue2");
        Emit(26, $"{op} slaty aqua");

        void Emit(int slot, string mutation)
        {
            // Slot classification:
            //   1-13: mixed 4-species / fischeri-only (per Std13 table)
            //   14-26: opaline block, fischeri-only
            bool fisOnly;
            if (slot <= 13)
                fisOnly = Std13[slot - 1].FischeriOnly;
            else
                fisOnly = true;
            if (fisOnly && !isFischeri) return;
            Add(g, Sub(slot), mutation);
        }
    }

    // Populate a "std52" group (edged, euwing): SF (1-13), DF (14-26),
    // opaline SF (27-39, fischeri-only), opaline DF (40-52, fischeri-only).
    private static void EyeringStd52(NomenclatureGroup g, string b, SpeciesCode species)
    {
        bool isFischeri = species == SpeciesCode.Fischeri;
        BlockStd(1,  "SF "  + b, false);
        BlockStd(14, "DF "  + b, false);
        BlockOp(27,  "opaline SF " + b);
        BlockOp(40,  "opaline DF " + b);

        void BlockStd(int start, string prefix, bool _)
        {
            EmitStd(start + 0,  $"{prefix} green, {prefix} D green, {prefix} DD green");
            EmitStd(start + 1,  $"orange face {prefix} green, orange face {prefix} D green, orange face {prefix} DD green", fisOnly: true);
            EmitStd(start + 2,  $"{prefix} blue, {prefix} D blue, {prefix} DD blue");
            EmitStd(start + 3,  $"{prefix} Blue1Blue2, {prefix} D Blue1Blue2, {prefix} DD Blue1Blue2");
            EmitStd(start + 4,  $"{prefix} aqua, {prefix} D aqua, {prefix} DD aqua", fisOnly: true);
            EmitStd(start + 5,  $"{prefix} violet");
            EmitStd(start + 6,  $"{prefix} violet Blue1Blue2");
            EmitStd(start + 7,  $"{prefix} violet factored D aqua", fisOnly: true);
            EmitStd(start + 8,  $"{prefix} slaty green");
            EmitStd(start + 9,  $"orange face {prefix} slaty green", fisOnly: true);
            EmitStd(start + 10, $"{prefix} slaty blue");
            EmitStd(start + 11, $"{prefix} slaty Blue1Blue2");
            EmitStd(start + 12, $"{prefix} slaty aqua", fisOnly: true);
        }

        void BlockOp(int start, string prefix)
        {
            if (!isFischeri) return;
            Add(g, Sub(start + 0),  $"{prefix} green, {prefix} D green, {prefix} DD green");
            Add(g, Sub(start + 1),  $"orange face {prefix} green, orange face {prefix} D green, orange face {prefix} DD green");
            Add(g, Sub(start + 2),  $"{prefix} blue, {prefix} D blue, {prefix} DD blue");
            Add(g, Sub(start + 3),  $"{prefix} Blue1Blue2, {prefix} D Blue1Blue2, {prefix} DD Blue1Blue2");
            Add(g, Sub(start + 4),  $"{prefix} aqua, {prefix} D aqua, {prefix} DD aqua");
            Add(g, Sub(start + 5),  $"{prefix} violet");
            Add(g, Sub(start + 6),  $"{prefix} violet Blue1Blue2");
            Add(g, Sub(start + 7),  $"{prefix} violet factored D aqua");
            Add(g, Sub(start + 8),  $"{prefix} slaty green");
            Add(g, Sub(start + 9),  $"orange face {prefix} slaty green");
            Add(g, Sub(start + 10), $"{prefix} slaty blue");
            Add(g, Sub(start + 11), $"{prefix} slaty Blue1Blue2");
            Add(g, Sub(start + 12), $"{prefix} slaty aqua");
        }

        void EmitStd(int slot, string mutation, bool fisOnly = false)
        {
            if (fisOnly && !isFischeri) return;
            Add(g, Sub(slot), mutation);
        }
    }

    // Populate small "std10" groups (misty, dec, NSL ino).
    private static void EyeringSmall10(NomenclatureGroup g, string b, SpeciesCode species)
    {
        bool isFischeri = species == SpeciesCode.Fischeri;
        void EmitFis(int slot, string mut) { if (isFischeri) Add(g, Sub(slot), mut); }

        Add(g, "01", $"{b} green");
        EmitFis(2, $"orange face {b} green");
        Add(g, "03", $"{b} blue");
        Add(g, "04", $"{b} Blue1Blue2");
        EmitFis(5, $"{b} aqua");
        EmitFis(6,  $"opaline {b} green");
        EmitFis(7,  $"orange face opaline {b} green");
        EmitFis(8,  $"opaline {b} blue");
        EmitFis(9,  $"opaline {b} Blue1Blue2");
        EmitFis(10, $"opaline {b} aqua");
    }

    // Fischeri-only SL Dominant Greywing (52 slots, transcribed verbatim).
    private static void EyeringSlDomGreywing(NomenclatureGroup g)
    {
        void A(int n, string m) => Add(g, Sub(n), m);
        A(1,  "SL SF greywing green, SL SF greywing D green, SL SF greywing DD green (male)");
        A(2,  "orange face SL SF greywing green, orange face SL SF greywing D green, orange face SL SF greywing DD green (male)");
        A(3,  "SL SF greywing blue, SL SF greywing D blue, SL SF greywing DD blue (male)");
        A(4,  "SL SF greywing Blue1Blue2, SL SF greywing D Blue1Blue2, SL SF greywing DD Blue1Blue2 (male)");
        A(5,  "SL SF greywing aqua, SL SF greywing D aqua, SL SF greywing DD aqua (male)");
        A(6,  "SL SF greywing violet (male)");
        A(7,  "SL SF greywing violet Blue1Blue2 (male)");
        A(8,  "SL SF greywing violet factored D aqua (male)");
        A(9,  "SL SF greywing slaty green (male)");
        A(10, "orange face SL SF greywing slaty green (male)");
        A(11, "SL SF greywing slaty blue (male)");
        A(12, "SL SF greywing slaty Blue1Blue2 (male)");
        A(13, "SL SF greywing slaty aqua (male)");
        A(14, "SL SF greywing green (female); SL DF greywing green (male)");
        A(15, "orange face SL SF greywing green (female); orange face SL DF greywing green (male)");
        A(16, "SL SF greywing blue (female); SL DF greywing blue (male)");
        A(17, "SL SF greywing Blue1Blue2 (female); SL DF greywing Blue1Blue2 (male)");
        A(18, "SL SF greywing aqua (female); SL DF greywing aqua (male)");
        A(19, "SL SF greywing violet (female); SL DF greywing violet (male)");
        A(20, "SL SF greywing violet Blue1Blue2 (female); SL DF greywing violet Blue1Blue2 (male)");
        A(21, "SL SF greywing violet factored D aqua (female); SL DF greywing violet factored D aqua (male)");
        A(22, "SL SF greywing slaty green (female); SL DF greywing slaty green (male)");
        A(23, "orange face SL SF greywing slaty green (female); orange face SL DF greywing slaty green (male)");
        A(24, "SL SF greywing slaty blue (female); SL DF greywing slaty blue (male)");
        A(25, "SL SF greywing slaty Blue1Blue2 (female); SL DF greywing slaty Blue1Blue2 (male)");
        A(26, "SL SF greywing slaty aqua (female); SL DF greywing aqua (male)");
        A(27, "opaline SL SF greywing green (male)");
        A(28, "orange face opaline SL SF greywing green (male)");
        A(29, "opaline SL SF greywing blue (male)");
        A(30, "opaline SL SF greywing Blue1Blue2 (male)");
        A(31, "opaline SL SF greywing aqua (male)");
        A(32, "opaline SL SF greywing violet (male)");
        A(33, "opaline SL SF greywing violet Blue1Blue2 (male)");
        A(34, "opaline SL SF greywing violet factored D aqua (male)");
        A(35, "opaline SL SF greywing slaty green (male)");
        A(36, "orange face opaline SL SF greywing slaty green (male)");
        A(37, "opaline SL SF greywing slaty blue (male)");
        A(38, "opaline SL SF greywing slaty Blue1Blue2 (male)");
        A(39, "opaline SL SF greywing slaty aqua (male)");
        A(40, "opaline SL SF greywing green (female); opaline SL DF greywing green (male)");
        A(41, "orange face opaline SL SF greywing green (female); orange face opaline SL DF greywing green (male)");
        A(42, "opaline SL SF greywing blue (female); opaline SL DF greywing blue (male)");
        A(43, "opaline SL SF greywing Blue1Blue2 (female); opaline SL DF greywing Blue1Blue2 (male)");
        A(44, "opaline SL SF greywing aqua (female); opaline SL DF greywing aqua (male)");
        A(45, "opaline SL SF greywing violet (female); opaline SL DF greywing violet (male)");
        A(46, "opaline SL SF greywing violet Blue1Blue2 (female); opaline SL DF greywing violet Blue1Blue2 (male)");
        A(47, "opaline SL SF greywing violet factored D aqua (female); opaline SL DF greywing violet factored D aqua (male)");
        A(48, "opaline SL SF greywing slaty green (female); opaline SL DF greywing slaty green (male)");
        A(49, "orange face opaline SL SF greywing slaty green (female); orange face opaline SL DF greywing slaty green (male)");
        A(50, "opaline SL SF greywing slaty blue (female); opaline SL DF greywing slaty blue (male)");
        A(51, "opaline SL SF greywing slaty Blue1Blue2 (female); opaline SL DF greywing slaty Blue1Blue2 (male)");
        A(52, "opaline SL SF greywing slaty aqua (female); opaline SL DF greywing slaty aqua (male)");
    }

    private static void BuildEyeRing(NomenclatureBuilder b)
    {
        (SpeciesCode Sp, int Base)[] species =
        {
            (SpeciesCode.Personatus, 50),
            (SpeciesCode.Fischeri,   100),
            (SpeciesCode.Nigrigenis, 150),
            (SpeciesCode.Lilianae,   200),
        };

        foreach (var (sp, baseCode) in species)
        {
            string P(int off) => (baseCode + off).ToString("D3");

            // Green (050/100/150/200)
            var green = b.AddGroup(P(0), "Green", sp, EntryTypeCode.Individual);
            Add(green, "01", "green");

            // Greenseries (051/101/151/201)
            var gs = b.AddGroup(P(1), "Greenseries", sp, EntryTypeCode.Individual);
            Add(gs, "01", "D green, DD green");
            if (sp == SpeciesCode.Fischeri) Add(gs, "02", "orange face green, orange face D green, orange face DD green");
            Add(gs, "03", "slaty green");
            if (sp == SpeciesCode.Fischeri) Add(gs, "04", "orange face slaty green");
            if (sp is SpeciesCode.Personatus or SpeciesCode.Fischeri)
                Add(gs, "05", "opaline green, opaline D green, opaline DD green");
            if (sp == SpeciesCode.Fischeri) Add(gs, "06", "orange face opaline green, orange face opaline D green, orange face opaline DD green");
            if (sp is SpeciesCode.Personatus or SpeciesCode.Fischeri)
                Add(gs, "07", "opaline slaty green");
            if (sp == SpeciesCode.Fischeri) Add(gs, "08", "orange face opaline slaty green");

            // Blue (052/102/152/202)
            var blue = b.AddGroup(P(2), "Blue", sp, EntryTypeCode.Individual);
            Add(blue, "01", "blue, D blue, DD blue");
            Add(blue, "02", "violet");
            Add(blue, "03", "slaty blue");
            if (sp is SpeciesCode.Personatus or SpeciesCode.Fischeri)
            {
                Add(blue, "04", "opaline blue, opaline D blue, opaline DD blue");
                Add(blue, "05", "opaline violet");
                Add(blue, "06", "opaline slaty blue");
            }

            // Blue1Blue2 (053/103/153/203)
            var b1b2 = b.AddGroup(P(3), "Blue1Blue2", sp, EntryTypeCode.Individual);
            Add(b1b2, "01", "Blue1Blue2, D Blue1Blue2, DD Blue1Blue2");
            Add(b1b2, "02", "violet Blue1Blue2");
            Add(b1b2, "03", "slaty Blue1Blue2");
            if (sp is SpeciesCode.Personatus or SpeciesCode.Fischeri)
            {
                Add(b1b2, "04", "opaline Blue1Blue2, opaline D Blue1Blue2, opaline DD Blue1Blue2");
                Add(b1b2, "05", "opaline violet Blue1Blue2");
                Add(b1b2, "06", "opaline slaty Blue1Blue2");
            }

            // Aqua (054/104/154/204) — fischeri-only individual group
            if (sp == SpeciesCode.Fischeri)
            {
                var aqua = b.AddGroup(P(4), "Aqua", sp, EntryTypeCode.Individual);
                Add(aqua, "01", "aqua, D aqua, DD aqua");
                Add(aqua, "02", "violet factored D aqua");
                Add(aqua, "03", "slaty aqua");
                Add(aqua, "04", "opaline aqua, opaline D aqua, opaline DD aqua");
                Add(aqua, "05", "opaline violet factored D aqua");
                Add(aqua, "06", "opaline slaty aqua");
            }

            EyeringStd26(b.AddGroup(P(5),  "Pastel",           sp, EntryTypeCode.Individual), "pastel", sp);
            EyeringStd52(b.AddGroup(P(6),  "Dominant Edged",   sp, EntryTypeCode.Individual), "edged", sp);
            EyeringStd26(b.AddGroup(P(7),  "Dominant Pied",    sp, EntryTypeCode.Individual), "dominant pied", sp);
            EyeringStd26(b.AddGroup(P(8),  "Recessive Pied",   sp, EntryTypeCode.Individual), "recessive pied", sp);
            EyeringStd26(b.AddGroup(P(9),  "Dilute",           sp, EntryTypeCode.Individual), "dilute", sp);
            EyeringSmall10(b.AddGroup(P(10), "Misty",          sp, EntryTypeCode.Individual), "DF misty", sp);
            EyeringStd52(b.AddGroup(P(11), "Euwing",           sp, EntryTypeCode.Individual), "euwing", sp);
            EyeringStd26(b.AddGroup(P(12), "Bronze Fallow",    sp, EntryTypeCode.Individual), "bronze fallow", sp);
            EyeringStd26(b.AddGroup(P(13), "Pale Fallow",      sp, EntryTypeCode.Individual), "pale fallow", sp);
            EyeringStd26(b.AddGroup(P(14), "Dun Fallow",       sp, EntryTypeCode.Individual), "dun fallow", sp);
            EyeringStd26(b.AddGroup(P(15), "Pale",             sp, EntryTypeCode.Individual), "pale", sp, "-");
            EyeringSmall10(b.AddGroup(P(16), "Dec",            sp, EntryTypeCode.Individual), "dec", sp);
            EyeringSmall10(b.AddGroup(P(17), "NSL Ino",        sp, EntryTypeCode.Individual), "NSL ino", sp);

            // SL Dominant Greywing (118) — fischeri-only individual group
            if (sp == SpeciesCode.Fischeri)
            {
                var grey = b.AddGroup(P(18), "SL Dominant Greywing", sp, EntryTypeCode.Individual);
                EyeringSlDomGreywing(grey);
            }

            // Rare Mutations (069/119/169/219)
            var rare = b.AddGroup(P(19), "Rare Mutations", sp, EntryTypeCode.Individual);
            Add(rare, "01", "crested in combination with birds in groups 50-67; 100-118; 150-167; 200-217");
            if (sp == SpeciesCode.Fischeri)
            {
                Add(rare, "02", "opaline crested in combination with birds in groups 100-118");
                Add(rare, "03", "DF dominant reduced in combination with birds in groups 100-104");
                Add(rare, "04", "opaline DF dominant reduced in combination with birds in groups 100-104");
            }
        }
    }

    // ── CANUS / TARANTA / PULLARIUS + STUDY + TEAMS ────────────────────────

    private static void BuildOthers(NomenclatureBuilder b)
    {
        var g250 = b.AddGroup("250", "Green", SpeciesCode.Canus, EntryTypeCode.Individual);
        Add(g250, "01", "male green");
        Add(g250, "02", "female green");

        var g300 = b.AddGroup("300", "Green", SpeciesCode.Taranta, EntryTypeCode.Individual);
        Add(g300, "01", "male green");
        Add(g300, "02", "female green");

        var g301 = b.AddGroup("301", "Greenseries", SpeciesCode.Taranta, EntryTypeCode.Individual);
        Add(g301, "01", "male D green, male DD green");
        Add(g301, "02", "female D green, female DD green");
        Add(g301, "03", "male DF misty green");
        Add(g301, "04", "female DF misty green");
        Add(g301, "05", "male bronze fallow green, bronze fallow D green, bronze fallow DD green");
        Add(g301, "06", "female bronze fallow green, bronze fallow D green, bronze fallow DD green");
        Add(g301, "07", "male pale fallow green, pale fallow D green, pale fallow DD green");
        Add(g301, "08", "female pale fallow green, pale fallow D green, pale fallow DD green");

        var g302 = b.AddGroup("302", "Tealseries", SpeciesCode.Taranta, EntryTypeCode.Individual);
        Add(g302, "01", "male teal, D teal, DD teal");
        Add(g302, "02", "female teal, D teal, DD teal");
        Add(g302, "03", "male DF misty teal");
        Add(g302, "04", "female DF misty teal");
        Add(g302, "05", "male bronze fallow teal, bronze fallow D teal, bronze fallow DD teal");
        Add(g302, "06", "female bronze fallow teal, bronze fallow D teal, bronze fallow DD teal");
        Add(g302, "07", "male pale fallow teal, pale fallow D teal, pale fallow DD teal");
        Add(g302, "08", "female pale fallow teal, pale fallow D teal, pale fallow DD teal");

        var g350 = b.AddGroup("350", "Green", SpeciesCode.Pullarius, EntryTypeCode.Individual);
        Add(g350, "01", "male green");
        Add(g350, "02", "female green");
    }

    private static void BuildStudyGroups(NomenclatureBuilder b)
    {
        // The N-class study group is a single 400 group whose rows are
        // per-species. Species is stored on the group level, so we associate
        // it with Roseicollis by convention (the group is species-agnostic
        // in practice; the rows discriminate).
        var study = b.AddGroup("400", "Study Group", SpeciesCode.Roseicollis, EntryTypeCode.Study);
        Add(study, "01", "A. roseicollis");
        Add(study, "02", "A. personatus");
        Add(study, "03", "A. fischeri");
        Add(study, "04", "A. nigrigenis");
        Add(study, "05", "A. lilianae");
        Add(study, "06", "A. canus");
        Add(study, "07", "A. taranta");
        Add(study, "08", "A. pullarius");
    }

    private static void BuildTeams(NomenclatureBuilder b)
    {
        // Roseicollis: 450 (green) + 451 (mutations)
        var g450 = b.AddGroup("450", "Green", SpeciesCode.Roseicollis, EntryTypeCode.Team);
        Add(g450, "01", "green");

        var g451 = b.AddGroup("451", "Mutations", SpeciesCode.Roseicollis, EntryTypeCode.Team);
        (string Sub, string Label)[] r451 =
        {
            ("01", "A. roseicollis greenseries (group 2)"),
            ("02", "A. roseicollis blue (group 3)"),
            ("03", "A. roseicollis aqua (group 4)"),
            ("04", "A. roseicollis turquoise (group 5)"),
            ("05", "A. roseicollis marbled (group 6)"),
            ("06", "A. roseicollis dilute (group 7)"),
            ("07", "A. roseicollis bronze fallow (group 8)"),
            ("08", "A. roseicollis pale fallow (group 9)"),
            ("09", "A. roseicollis SL ino (group 10)"),
            ("10", "A. roseicollis cinnamon (group 11)"),
            ("11", "A. roseicollis pallid (group 12)"),
            ("12", "A. roseicollis pale (group 13)"),
            ("13", "A. roseicollis dominant pied (group 14)"),
            ("14", "A. roseicollis recessive pied (group 15)"),
            ("15", "A. roseicollis DM jade (group 16)"),
            ("16", "A. roseicollis misty (group 17)"),
            ("17", "A. roseicollis crested (group 18)"),
        };
        foreach (var (sub, label) in r451) Add(g451, sub, label);

        // Eye-ring teams — codes staggered because personatus/nigrigenis/
        // lilianae skip fischeri-only slots (aqua at /04, SL greywing at /17 for fischeri).
        (SpeciesCode Sp, string GreenPrefix, string MutPrefix, bool IsFischeri)[] eyeRing =
        {
            (SpeciesCode.Personatus, "452", "453", false),
            (SpeciesCode.Fischeri,   "454", "455", true),
            (SpeciesCode.Nigrigenis, "456", "457", false),
            (SpeciesCode.Lilianae,   "458", "459", false),
        };

        foreach (var (sp, greenPref, mutPref, isFis) in eyeRing)
        {
            var gGreen = b.AddGroup(greenPref, "Green", sp, EntryTypeCode.Team);
            Add(gGreen, "01", "green");

            var gMut = b.AddGroup(mutPref, "Mutations", sp, EntryTypeCode.Team);
            if (isFis)
            {
                (string Sub, string Label)[] rows =
                {
                    ("01", "birds in greenseries"),
                    ("02", "blue"),
                    ("03", "Blue1Blue2"),
                    ("04", "aqua"),
                    ("05", "birds in pastel"),
                    ("06", "birds in dominant edged"),
                    ("07", "birds in dominant pied"),
                    ("08", "birds in recessive pied"),
                    ("09", "birds in misty"),
                    ("10", "birds in dilute"),
                    ("11", "birds in euwing"),
                    ("12", "birds in bronze fallow"),
                    ("13", "birds in pale fallow"),
                    ("14", "birds in dun fallow"),
                    ("15", "birds in pale"),
                    ("16", "birds in dec"),
                    ("17", "birds in NSL ino"),
                    ("18", "birds in SL dominant greywing"),
                    ("19", "birds in rare mutations"),
                };
                foreach (var (sub, label) in rows) Add(gMut, sub, label);
            }
            else
            {
                (string Sub, string Label)[] rows =
                {
                    ("01", "birds in greenseries"),
                    ("02", "blue"),
                    ("03", "Blue1Blue2"),
                    ("04", "birds in pastel"),
                    ("05", "birds in dominant edged"),
                    ("06", "birds in dominant pied"),
                    ("07", "birds in recessive pied"),
                    ("08", "birds in misty"),
                    ("09", "birds in dilute"),
                    ("10", "birds in euwing"),
                    ("11", "birds in bronze fallow"),
                    ("12", "birds in pale fallow"),
                    ("13", "birds in dun fallow"),
                    ("14", "birds in pale"),
                    ("15", "birds in dec"),
                    ("16", "birds in NSL ino"),
                    ("18", "birds in rare mutations"),
                };
                foreach (var (sub, label) in rows) Add(gMut, sub, label);
            }
        }

        // Canus / Taranta / Pullarius teams
        var g460 = b.AddGroup("460", "Green", SpeciesCode.Canus, EntryTypeCode.Team);
        Add(g460, "01", "green");
        var g461 = b.AddGroup("461", "Green", SpeciesCode.Taranta, EntryTypeCode.Team);
        Add(g461, "01", "green");
        var g462 = b.AddGroup("462", "Mutations", SpeciesCode.Taranta, EntryTypeCode.Team);
        Add(g462, "01", "birds in greenseries");
        Add(g462, "02", "birds in tealseries");
        var g463 = b.AddGroup("463", "Green", SpeciesCode.Pullarius, EntryTypeCode.Team);
        Add(g463, "01", "green");
    }
}
