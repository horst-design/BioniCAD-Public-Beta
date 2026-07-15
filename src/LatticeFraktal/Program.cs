using System.Diagnostics;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LatticeFraktal.Engines;
using LatticeFraktal.Model;
using LatticeFraktal.Optimierung;
using LatticeFraktal.Pipeline;

namespace LatticeFraktal;

/// <summary>
/// Einstiegspunkt und CLI-Gerüst für das Modul "lattice-fraktal".
/// Stand: Datenmodell + Parameter-Validierung vorhanden; die eigentliche
/// Pipeline (Engine -> Durchmesser -> Rendering -> STL) folgt schrittweise.
/// </summary>
internal static class Program
{
    private const string Version = "0.1.0-durchstich";

    private static int Main(string[] args)
    {
        Console.WriteLine($"BionicAD {Version}  ·  Kern-Server (PicoGK)");
        Console.WriteLine("Bionische Struktur-Suite — Baum · Tessellation · Mattheck (FEM/SKO/CAO · CAIO-Feldgitter)");
        Console.WriteLine();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        return command switch
        {
            "run"      => CmdRun(args),
            "bake"     => CmdBake(args),
            "tessbake" => CmdTessBake(args),
            "skobake"  => CmdSkoBake(args),
            "fieldlatticebake" => CmdFieldLatticeBake(args),
            "booleanbake" => CmdBooleanBake(args),
            "serve"    => CmdServe(args),
            "optimize" => CmdOptimize(args),
            "validate" => CmdValidate(args),
            "help" or "--help" or "-h" => Usage(0),
            _ => UnknownCommand(args[0]),
        };
    }

    private static int CmdRun(string[] args)
    {
        string? pfad = Option(args, "--params");
        if (pfad is null)
        {
            Console.Error.WriteLine("Fehlt: --params <eingabe.json>");
            return 1;
        }

        LatticeParameters? p = LadeParameter(pfad, out string? ladeFehler);
        if (p is null)
        {
            Console.Error.WriteLine($"Konnte Parameterdatei nicht lesen: {ladeFehler}");
            return 1;
        }

        ValidationResult v = ParameterValidator.Pruefe(p);
        foreach (string w in v.Warnungen) Console.WriteLine($"  [Warnung] {w}");
        foreach (string f in v.Fehler)   Console.WriteLine($"  [Fehler]  {f}");
        if (!v.IstGueltig)
        {
            Console.WriteLine("run: Abbruch wegen ungültiger Parameter.");
            return 2;
        }

        EngineParameterDto ep = p.EngineParameter;
        List<Hindernis> hindernisse = ParameterMapper.Hindernisse(p);
        var engine = new SpaceColonization();
        var baeume = new List<Tree>();

        EngineOptionen Opt() => new EngineOptionen
        {
            Schrittweite = ep.Schrittweite,
            Einflussradius = ep.Einflussradius,
            Killabstand = ep.Killabstand,
            Jitter = ep.Jitter,
            Seed = ep.Seed,
            MaxIterationen = ep.MaxIterationen,
            Hindernisse = hindernisse,
            Clearance = p.Mindestabstand,
        };

        float wurzelDurchmesser;

        if (p.Doppelbaum && p.ZweiterBaum is not null)
        {
            // Verschränkter Doppelbaum: beide wachsen gleichzeitig und weichen einander aus.
            Vector3 startA = ParameterMapper.Start(p);
            List<Target> zieleA = ParameterMapper.Ziele(p);
            Vector3 startB = ParameterMapper.Start(p.ZweiterBaum);
            List<Target> zieleB = ParameterMapper.Ziele(p.ZweiterBaum);

            DoppelErgebnis d = engine.ErzeugeDoppel(startA, zieleA, startB, zieleB, Opt(), p.Mindestabstand);
            SkelettGlaettung.Glaette(d.BaumA, ep.GlaettungPaesse, ep.GlaettungStaerke);
            SkelettGlaettung.Glaette(d.BaumB, ep.GlaettungPaesse, ep.GlaettungStaerke);
            new MurrayDurchmesser().Zuweisen(d.BaumA, p.Start.Durchmesser, zieleA, ep.MurrayExponent, ep.MinDurchmesser);
            new MurrayDurchmesser().Zuweisen(d.BaumB, p.ZweiterBaum.Start.Durchmesser, zieleB, ep.MurrayExponent, ep.MinDurchmesser);
            DurchmesserNachbearbeitung.Glaette(d.BaumA, ep.DurchmesserGlaettung, 0.5f);
            DurchmesserNachbearbeitung.Glaette(d.BaumB, ep.DurchmesserGlaettung, 0.5f);
            DurchmesserNachbearbeitung.BegrenzeVolumenverhaeltnis(d.BaumA, ep.MaxVolumenverhaeltnis);
            DurchmesserNachbearbeitung.BegrenzeVolumenverhaeltnis(d.BaumB, ep.MaxVolumenverhaeltnis);
            Hindernis.ProjiziereBaum(d.BaumA, hindernisse, p.Mindestabstand);
            Hindernis.ProjiziereBaum(d.BaumB, hindernisse, p.Mindestabstand);

            int verb = 0, sack = 0;
            if (p.VerbindeEnden)
                (verb, sack) = KapillarVerbindung.VerbindeAlleEnden(d.BaumA, d.BaumB, p.Verbindungsreichweite);

            baeume.Add(d.BaumA);
            baeume.Add(d.BaumB);
            wurzelDurchmesser = d.BaumA.Wurzel.Durchmesser;

            Console.WriteLine("Engine: space_colonization (Doppelbaum, verschränkt)");
            Console.WriteLine($"Baum A: {d.BaumA.Knoten.Count} Knoten, {d.ZieleErreichtA}/{d.ZieleGesamtA} Ziele.");
            Console.WriteLine($"Baum B: {d.BaumB.Knoten.Count} Knoten, {d.ZieleErreichtB}/{d.ZieleGesamtB} Ziele.");
            if (p.VerbindeEnden)
                Console.WriteLine($"Kapillar-Verbindung: {verb} verbunden, {sack} Sackgassen.");
        }
        else
        {
            Vector3 startA = ParameterMapper.Start(p);
            List<Target> zieleA = ParameterMapper.Ziele(p);
            SkelettErgebnis erg = engine.Erzeuge(startA, zieleA, Opt());
            SkelettGlaettung.Glaette(erg.Baum, ep.GlaettungPaesse, ep.GlaettungStaerke);
            new MurrayDurchmesser().Zuweisen(erg.Baum, p.Start.Durchmesser, zieleA, ep.MurrayExponent, ep.MinDurchmesser);
            DurchmesserNachbearbeitung.Glaette(erg.Baum, ep.DurchmesserGlaettung, 0.5f);
            DurchmesserNachbearbeitung.BegrenzeVolumenverhaeltnis(erg.Baum, ep.MaxVolumenverhaeltnis);
            Hindernis.ProjiziereBaum(erg.Baum, hindernisse, p.Mindestabstand);
            baeume.Add(erg.Baum);
            wurzelDurchmesser = erg.Baum.Wurzel.Durchmesser;

            Console.WriteLine("Engine: space_colonization");
            Console.WriteLine($"Skelett: {erg.Baum.Knoten.Count} Knoten, {erg.Baum.Segmente.Count} Segmente, {erg.Iterationen} Iterationen.");
            Console.WriteLine($"Ziele erreicht: {erg.ZieleErreicht} / {erg.ZieleGesamt}.");
        }

        float dBlattMin = float.MaxValue;
        foreach (Tree b in baeume)
            foreach (Node n in b.Knoten)
                if (n.IstBlatt)
                    dBlattMin = MathF.Min(dBlattMin, n.Durchmesser);
        Console.WriteLine($"Durchmesser (Murray): Wurzel {wurzelDurchmesser:F2} mm, kleinstes Blatt {dBlattMin:F2} mm.");

        if (p.Output.Stl)
        {
            string outStl = Option(args, "--out-stl") ?? "ausgabe.stl";
            float voxel = ParseFloat(Option(args, "--voxel"), ep.VoxelGroesse);

            // --fillet überschreibt die JSON-Einstellung (schnelles Ausprobieren ohne Datei-Edit).
            // --fillet 0 = Übergänge aus (roher Baum).
            bool uebergaenge;
            float fillet;
            string? filletArg = Option(args, "--fillet");
            if (filletArg is not null)
            {
                fillet = ParseFloat(filletArg, 0f);
                uebergaenge = fillet > 0f;
            }
            else
            {
                uebergaenge = p.PipelineAktiv.TryGetValue("uebergaenge", out bool ua) && ua;
                fillet = uebergaenge ? p.UebergangFilletMm : 0f;
            }

            Console.WriteLine(uebergaenge
                ? $"Rendering (Voxelgröße {voxel:F2} mm; Übergänge: Fillet {fillet:F2} mm + Knotenkugeln) ..."
                : $"Rendering (Voxelgröße {voxel:F2} mm; ohne Übergänge) ...");

            new LatticeRenderer
            {
                VoxelGroesseMm = voxel,
                KnotenKugeln = uebergaenge,
                FilletMm = fillet,
            }.RendereNachStl(baeume, outStl);

            var fi = new FileInfo(outStl);
            Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        }
        else
        {
            Console.WriteLine("output.stl ist false – kein STL-Export.");
        }

        return 0;
    }

    /// <summary>
    /// Voxelisiert ein *fertiges* Skelett (Skelett-Vertrag) direkt — ohne
    /// Neuberechnung, ohne RNG. Damit entspricht die STL exakt dem in der
    /// Vorschau entworfenen Skelett ("was du siehst, bekommst du").
    /// </summary>
    private static int CmdBake(string[] args)
    {
        string? pfad = Option(args, "--skelett") ?? Option(args, "--params");
        if (pfad is null)
        {
            Console.Error.WriteLine("Fehlt: --skelett <skelett.json>");
            return 1;
        }

        SkelettDto? s = LadeSkelett(pfad, out string? ladeFehler);
        if (s is null)
        {
            Console.Error.WriteLine($"Konnte Skelett-Datei nicht lesen: {ladeFehler}");
            return 1;
        }

        List<Tree>? baeume = SkelettBauer.Baue(s, out string? bauFehler);
        if (baeume is null)
        {
            Console.Error.WriteLine($"Ungültiges Skelett: {bauFehler}");
            return 2;
        }

        int knotenGesamt = 0, segmenteGesamt = 0;
        float dWurzel = baeume[0].Wurzel.Durchmesser, dBlattMin = float.MaxValue;
        foreach (Tree b in baeume)
        {
            knotenGesamt += b.Knoten.Count;
            segmenteGesamt += b.Segmente.Count;
            foreach (Node n in b.Knoten)
                if (n.IstBlatt) dBlattMin = MathF.Min(dBlattMin, n.Durchmesser);
        }
        if (dBlattMin == float.MaxValue) dBlattMin = 0f;

        Console.WriteLine($"Bake (Skelett-Vertrag v{s.Version}): {baeume.Count} Baum/Bäume, "
                          + $"{knotenGesamt} Knoten, {segmenteGesamt} Segmente.");
        Console.WriteLine($"Durchmesser: Wurzel {dWurzel:F2} mm, kleinstes Blatt {dBlattMin:F2} mm.");

        bool hohl = string.Equals(s.KanalTyp, "hollow", StringComparison.OrdinalIgnoreCase);
        bool nurWurzeln = string.Equals(s.Oeffnung, "wurzeln", StringComparison.OrdinalIgnoreCase);
        // --wand überschreibt die Wandstärke aus der Datei.
        float wand = ParseFloat(Option(args, "--wand"), s.Wandstaerke);

        string outStl = Option(args, "--out-stl") ?? "ausgabe.stl";
        float voxel = ParseFloat(Option(args, "--voxel"), s.VoxelGroesse);

        // --fillet überschreibt den Wert aus der Datei; 0 = keine Verrundung.
        string? filletArg = Option(args, "--fillet");
        float fillet = filletArg is not null ? ParseFloat(filletArg, 0f) : s.Fillet;

        string kanal = hohl
            ? $"hohl (Wandstärke {wand:F2} mm, {(nurWurzeln ? "nur Wurzeln offen" : "Enden offen")})"
            : "solid";
        Console.WriteLine(fillet > 0f
            ? $"Rendering (Voxelgröße {voxel:F2} mm; {kanal}; Mattheck-Fillet {fillet:F2} mm; Knotenkugeln {(s.KnotenKugeln ? "an" : "aus")}) ..."
            : $"Rendering (Voxelgröße {voxel:F2} mm; {kanal}; ohne Fillet; Knotenkugeln {(s.KnotenKugeln ? "an" : "aus")}) ...");

        new LatticeRenderer
        {
            VoxelGroesseMm = voxel,
            KnotenKugeln = s.KnotenKugeln,
            FilletMm = fillet,
            Hohl = hohl,
            Wandstaerke = wand,
            NurWurzelnOeffnen = nurWurzeln,
            FlanschDurchmesser = s.FlanschD,
            FlanschDicke = s.FlanschT > 0 ? s.FlanschT : 1.5f,
        }.RendereNachStl(baeume, outStl);

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    /// <summary>DTO für den Tessellation-Kern-Export (Streben-Liste aus dem Browser).</summary>
    private sealed class TessDto
    {
        public float Voxel { get; set; } = 0.3f;
        public float Fillet { get; set; } = 0f;
        public string? Smooth { get; set; }   // "closing" | "opening" | "beides" | "keine"
        public float[][]? Beams { get; set; }
    }

    /// <summary>DTO für den Mattheck/SKO-Kern-Export (Voxelfeld aus dem Browser).</summary>
    private sealed class SkoDto
    {
        public float H { get; set; }          // Rasterweite (mm) des FE-Voxelgitters
        public int Bx { get; set; }
        public int By { get; set; }
        public int Bz { get; set; }
        public int[]? Vox { get; set; }        // belegte Linearindizes: (k*By+j)*Bx+i
        public float Voxel { get; set; }       // Kern-Voxelgröße (Auflösung); 0 => H*0.5
        public float Fillet { get; set; }      // Verrundung (mm); 0 => aus
        public string? Smooth { get; set; }    // "closing" | "opening" | "beides" | "keine"
        public string? Modus { get; set; }     // "solid" (Vollmaterial) | "lattice" (Haut + BCC-Gitter)
        public float Strut { get; set; }       // Strebenradius (mm) im Gittermodus; 0 => H*0.18
        public bool Orient { get; set; }       // Gitter an Hauptspannungen ausrichten (Weg A)
        public float[]? Frames { get; set; }   // je belegtem Voxel 12 Floats: 3×(dx,dy,dz, radius)
    }

    /// <summary>DTO für das implizite Feld-Lattice (CAIO). Vertrag aus mattheck.html „CAIO-Feld-JSON".</summary>
    private sealed class FieldDto
    {
        public int Bx { get; set; }
        public int By { get; set; }
        public int Bz { get; set; }
        public float H { get; set; }
        public float Vmax { get; set; }
        public int[]? Occ { get; set; }        // Ntot: Form 0/1
        public float[]? Field { get; set; }    // Ntot: normalisierte von Mises 0..1 (Grading)
        public float[]? Dirs { get; set; }     // Ntot*9: Hauptspannungs-Frame v1,v2,v3 (CAIO-Warp)
        public float[]? Pvals { get; set; }    // Ntot*3: Hauptspannungswerte (für kohärenten Tensor); optional
        public float Cell { get; set; }        // Zellgröße mm; 0 => H*4
        public float Vol { get; set; }         // Volumenanteil 0..1 -> iso
        public float Grading { get; set; }     // 0..1: wie stark die Spannung die Wandstärke graded
        public float Caio { get; set; }        // 0..1: Kraftfluss-Intensität (Ausrichtung an Hauptspannungen; 0 isotrop)
        public float FlowLen { get; set; }     // -1..1: Zell-Streckung entlang Flussachse (>0 länglich, <0 gestaucht)
        public float FlowSmooth { get; set; }  // 0..1: Glättung/Kohärenz des Richtungsfelds
        public float Voxel { get; set; }       // Kern-Voxelgröße; 0 => H*0.5
        public float Fillet { get; set; }      // Glättung (DoubleOffset) mm
        public string? Type { get; set; }      // "gyroid" | "strut" | "shape"
        public string? ClipStl { get; set; }   // optional: base64 binäres STL (Bauraum-Mesh) -> MESSERSCHARFER Clip
    }

    /// <summary>
    /// Rendert ein belegtes Voxelfeld (SKO/CAO-Ergebnis) als glattes STL: jedes Voxel wird zu
    /// einer Kugel, benachbarte Voxel per Beam verbunden (Kapsel-Graph, keine Diagonal-Bleeds),
    /// dann optionale morphologische Glättung. Genutzt vom Server über POST /sko.
    /// </summary>
    private static int CmdSkoBake(string[] args)
    {
        string? inPfad = Option(args, "--in");
        string outStl = Option(args, "--out-stl") ?? "sko.stl";
        if (inPfad is null || !File.Exists(inPfad))
        {
            Console.Error.WriteLine("Fehlt: --in <sko.json>");
            return 1;
        }

        SkoDto? dto;
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
            dto = JsonSerializer.Deserialize<SkoDto>(File.ReadAllText(inPfad), opt);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("JSON-Fehler: " + e.Message);
            return 1;
        }
        if (dto?.Vox is null || dto.Vox.Length == 0 || dto.H <= 0f || dto.Bx <= 0 || dto.By <= 0 || dto.Bz <= 0)
        {
            Console.Error.WriteLine("Kein gültiges Voxelfeld in der Eingabe.");
            return 2;
        }

        float voxel = dto.Voxel > 0f ? dto.Voxel : dto.H * 0.5f;
        float fillet = MathF.Max(dto.Fillet, 0f);
        string glaettung = (dto.Smooth ?? "closing").ToLowerInvariant();
        string modus = (dto.Modus ?? "solid").ToLowerInvariant();
        float strut = MathF.Max(dto.Strut, 0f);
        bool orient = dto.Orient && dto.Frames is not null && dto.Frames.Length == dto.Vox.Length * 12;
        Console.WriteLine($"SkoBake: {dto.Vox.Length} Voxel, Raster {dto.H:F2} mm, Kern-Voxel {voxel:F2} mm, Modus {modus}{(orient ? " (lastpfad-orientiert)" : "")}, Fillet {fillet:F2} mm, Glättung {glaettung} ...");

        new LatticeRenderer { VoxelGroesseMm = voxel, FilletMm = fillet, GlaettungModus = glaettung }
            .RendereVoxelfeld(dto.Vox, dto.Bx, dto.By, dto.Bz, dto.H, outStl, modus, strut, orient, dto.Frames);

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    /// <summary>Baut das implizite Feld-Lattice (Gyroid/Streben, CAIO) aus dem CAIO-Feld-JSON. Server: POST /field-lattice.</summary>
    private static int CmdFieldLatticeBake(string[] args)
    {
        string? inPfad = Option(args, "--in");
        string outStl = Option(args, "--out-stl") ?? "field.stl";
        if (inPfad is null || !File.Exists(inPfad)) { Console.Error.WriteLine("Fehlt: --in <field.json>"); return 1; }

        FieldDto? dto;
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
            dto = JsonSerializer.Deserialize<FieldDto>(File.ReadAllText(inPfad), opt);
        }
        catch (Exception e) { Console.Error.WriteLine("JSON-Fehler: " + e.Message); return 1; }

        if (dto?.Occ is null || dto.Field is null || dto.Dirs is null || dto.Bx <= 0 || dto.By <= 0 || dto.Bz <= 0 || dto.H <= 0f)
        { Console.Error.WriteLine("Ungültiges CAIO-Feld."); return 2; }
        int Ntot = dto.Bx * dto.By * dto.Bz;
        if (dto.Occ.Length < Ntot || dto.Field.Length < Ntot || dto.Dirs.Length < Ntot * 9)
        { Console.Error.WriteLine("Feld-Arrays zu kurz (occ/field/dirs)."); return 2; }

        float cell   = dto.Cell  > 0f ? dto.Cell  : dto.H * 4f;
        float voxel  = dto.Voxel > 0f ? dto.Voxel : dto.H * 0.5f;
        float fillet = MathF.Max(dto.Fillet, 0f);
        string type  = (dto.Type ?? "gyroid").ToLowerInvariant();
        Console.WriteLine($"FieldLattice: {dto.Bx}×{dto.By}×{dto.Bz}, Zelle {cell:F2} mm, Typ {type}, Intensität {dto.Caio:F2}, Länge {dto.FlowLen:F2}, Smoothing {dto.FlowSmooth:F2}, Grading {dto.Grading:F2}, Kern-Voxel {voxel:F2} mm ...");

        // Optionaler scharfer Bauraum-Clip: base64-STL -> temp-Datei; deaktiviert den groben occ-Clip.
        string? clipStlPath = null;
        if (!string.IsNullOrEmpty(dto.ClipStl))
        {
            try
            {
                clipStlPath = Path.Combine(Path.GetTempPath(), $"bionicad-clip-{Environment.ProcessId}.stl");
                File.WriteAllBytes(clipStlPath, Convert.FromBase64String(dto.ClipStl));
                Console.WriteLine($"Scharfer Mesh-Clip: {new FileInfo(clipStlPath).Length / 1024.0:F1} KB Bauraum-STL.");
            }
            catch (Exception e) { Console.Error.WriteLine("Clip-STL-Fehler (ignoriert): " + e.Message); clipStlPath = null; }
        }

        var impl = new FieldLatticeImplicit(dto.Bx, dto.By, dto.Bz, dto.H, dto.Occ, dto.Field, dto.Dirs, dto.Pvals,
                                            cell, dto.Vol, dto.Grading, dto.Caio, dto.FlowLen, dto.FlowSmooth, type,
                                            clipShape: clipStlPath is null);
        FieldLatticeImplicit.Render(impl, voxel, fillet, outStl, clipStlPath);
        if (clipStlPath != null) { try { File.Delete(clipStlPath); } catch { } }

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    private sealed class BooleanDto
    {
        public string? A { get; set; }      // base64 binäres STL: Operand A (i.d.R. das Resultat)
        public string? B { get; set; }      // base64 binäres STL: Operand B (Bauraum/Import); bei "shell" ignoriert
        public string? Op { get; set; }     // union | subtract | intersect | shell | shellunion | solid
        public float Wall { get; set; }     // Wandstärke mm (shell/shellunion)
        public float Voxel { get; set; }    // Kern-Voxelgröße mm; 0 => 0.3
        public float Fillet { get; set; }   // optionale Kantenglättung mm
    }

    /// <summary>Boolesche Operation zwischen zwei STL-Körpern (bzw. Schale). Server: POST /boolean.</summary>
    private static int CmdBooleanBake(string[] args)
    {
        string? inPfad = Option(args, "--in");
        string outStl = Option(args, "--out-stl") ?? "boolean.stl";
        if (inPfad is null || !File.Exists(inPfad)) { Console.Error.WriteLine("Fehlt: --in <boolean.json>"); return 1; }

        BooleanDto? dto;
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
            dto = JsonSerializer.Deserialize<BooleanDto>(File.ReadAllText(inPfad), opt);
        }
        catch (Exception e) { Console.Error.WriteLine("JSON-Fehler: " + e.Message); return 1; }

        string op = (dto?.Op ?? "union").ToLowerInvariant();
        if (dto?.A is null) { Console.Error.WriteLine("Operand A (STL) fehlt."); return 2; }
        bool brauchtB = op is "union" or "subtract" or "intersect" or "shellunion";
        if (brauchtB && string.IsNullOrEmpty(dto.B)) { Console.Error.WriteLine("Operand B (STL) fehlt für " + op + "."); return 2; }

        float voxel = dto.Voxel > 0f ? dto.Voxel : 0.3f;
        float wall = dto.Wall > 0f ? dto.Wall : 1.5f;
        string tmp = Path.Combine(Path.GetTempPath(), "lattice-fraktal-serve");
        Directory.CreateDirectory(tmp);
        string aPath = Path.Combine(tmp, $"bool-a-{Environment.ProcessId}.stl");
        string? bPath = null;
        try
        {
            File.WriteAllBytes(aPath, Convert.FromBase64String(dto.A));
            if (!string.IsNullOrEmpty(dto.B))
            {
                bPath = Path.Combine(tmp, $"bool-b-{Environment.ProcessId}.stl");
                File.WriteAllBytes(bPath, Convert.FromBase64String(dto.B));
            }
        }
        catch (Exception e) { Console.Error.WriteLine("STL-Dekodierung fehlgeschlagen: " + e.Message); return 2; }

        Console.WriteLine($"Boolean: {op}, Wand {wall:F2} mm, Kern-Voxel {voxel:F2} mm ...");
        BooleanOps.Render(aPath, bPath, op, wall, voxel, dto.Fillet, outStl);
        try { File.Delete(aPath); if (bPath != null) File.Delete(bPath); } catch { }

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    /// <summary>
    /// Rendert eine Streben-Liste (Tessellation-Modul) als STL: jeder Beam = [ax,ay,az,bx,by,bz,r].
    /// Voxelgröße + Fillet steuern die Glättung. Genutzt vom Server über POST /tess.
    /// </summary>
    private static int CmdTessBake(string[] args)
    {
        string? inPfad = Option(args, "--in");
        string outStl = Option(args, "--out-stl") ?? "tess.stl";
        if (inPfad is null || !File.Exists(inPfad))
        {
            Console.Error.WriteLine("Fehlt: --in <beams.json>");
            return 1;
        }

        TessDto? dto;
        try
        {
            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
            dto = JsonSerializer.Deserialize<TessDto>(File.ReadAllText(inPfad), opt);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("JSON-Fehler: " + e.Message);
            return 1;
        }
        if (dto?.Beams is null || dto.Beams.Length == 0)
        {
            Console.Error.WriteLine("Keine Beams in der Eingabe.");
            return 2;
        }

        var beams = new List<(Vector3 A, Vector3 B, float Radius)>(dto.Beams.Length);
        foreach (float[] b in dto.Beams)
        {
            if (b.Length < 7) continue;
            beams.Add((new Vector3(b[0], b[1], b[2]), new Vector3(b[3], b[4], b[5]), MathF.Max(b[6], 0.02f)));
        }
        if (beams.Count == 0) { Console.Error.WriteLine("Keine gültigen Beams."); return 2; }

        float voxel = dto.Voxel > 0f ? dto.Voxel : 0.3f;
        float fillet = MathF.Max(dto.Fillet, 0f);
        string glaettung = (dto.Smooth ?? "closing").ToLowerInvariant();
        Console.WriteLine($"TessBake: {beams.Count} Streben, Voxelgröße {voxel:F2} mm, Radius {fillet:F2} mm, Glättung {glaettung} ...");

        new LatticeRenderer { VoxelGroesseMm = voxel, FilletMm = fillet, GlaettungModus = glaettung }.RendereBeams(beams, outStl);

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    /// <summary>
    /// Lokaler Bake-Server für die WYSIWYG-Vorschau. Lauscht auf localhost und
    /// rendert jedes per POST /bake geschickte Skelett zu STL. Jede Anfrage läuft
    /// in einem frischen Kindprozess (eigene PicoGK-Initialisierung) — robust und
    /// ohne Re-Init-Probleme.
    /// </summary>
    private static int CmdServe(string[] args)
    {
        string url = Option(args, "--url") ?? "http://localhost:5151/";
        if (!url.EndsWith('/')) url += "/";

        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        try { listener.Start(); }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Server-Start fehlgeschlagen: {e.Message}");
            Console.Error.WriteLine("Tipp: anderen Port mit --url http://localhost:PORT/ versuchen.");
            return 1;
        }

        // Eigene exe (für Kindprozesse). Bei 'dotnet exec dll' den DLL-Pfad voranstellen.
        string exe = Environment.ProcessPath ?? "dotnet";
        bool istDotnet = Path.GetFileNameWithoutExtension(exe)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        string? dll = istDotnet ? Assembly.GetEntryAssembly()?.Location : null;

        string? uiPfad = Option(args, "--ui") ?? FindeUiDatei();

        Console.WriteLine($"Bake-Server läuft auf {url}");
        if (uiPfad is not null)
            Console.WriteLine($"  >> Vorschau im Browser öffnen:  {url}");
        else
            Console.WriteLine("  [Hinweis] baum_vorschau.html nicht gefunden — per --ui <pfad> angeben, sonst nur POST /bake.");
        Console.WriteLine("  POST /bake   Body = skelett.json  (optional ?voxel=..&fillet=..)  -> STL");
        Console.WriteLine("  Strg+C zum Beenden.");

        while (true)
        {
            HttpListenerContext ctx = listener.GetContext();
            try { BehandleAnfrage(ctx, exe, dll, uiPfad); }
            catch (Exception e)
            {
                Console.Error.WriteLine($"  Fehler: {e.Message}");
                try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
            }
        }
    }

    /// <summary>Sucht baum_vorschau.html ausgehend von Arbeitsverzeichnis und exe-Ordner aufwärts.</summary>
    private static string? FindeUiDatei()
    {
        var startpunkte = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (string s in startpunkte)
        {
            var dir = new DirectoryInfo(s);
            for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
            {
                string kandidat = Path.Combine(dir.FullName, "ui", "baum_vorschau.html");
                if (File.Exists(kandidat)) return kandidat;
            }
        }
        return null;
    }

    private static void BehandleAnfrage(HttpListenerContext ctx, string exe, string? dll, string? uiPfad)
    {
        HttpListenerRequest req = ctx.Request;
        HttpListenerResponse res = ctx.Response;
        res.AddHeader("Access-Control-Allow-Origin", "*");
        res.AddHeader("Access-Control-Allow-Headers", "*");
        res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

        if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

        string pfad = req.Url?.AbsolutePath ?? "/";

        // GET / , /index.html -> Modul-Shell (bionicad.html), falls vorhanden; sonst Standard-UI. /vorschau -> immer Standard-UI (Baum).
        if (req.HttpMethod == "GET" && (pfad == "/" || pfad == "/index.html" || pfad == "/vorschau"))
        {
            string ausliefern = uiPfad;
            if (pfad != "/vorschau" && uiPfad is not null)
            {
                string shell = Path.Combine(Path.GetDirectoryName(uiPfad)!, "bionicad.html");
                if (File.Exists(shell)) ausliefern = shell;
            }
            if (ausliefern is null || !File.Exists(ausliefern)) { res.StatusCode = 404; SchreibeText(res, "UI nicht gefunden."); return; }
            byte[] html = File.ReadAllBytes(ausliefern);
            res.StatusCode = 200; res.ContentType = "text/html; charset=utf-8"; res.ContentLength64 = html.Length;
            res.OutputStream.Write(html, 0, html.Length); res.OutputStream.Close();
            return;
        }

        // GET /<name>.html|.js -> Geschwister-Datei aus dem ui-Ordner (Modul-Wechsel + mattheck_fem.js)
        if (req.HttpMethod == "GET" && uiPfad is not null && (pfad.EndsWith(".html") || pfad.EndsWith(".js")))
        {
            string name = Path.GetFileName(pfad.TrimStart('/'));
            string kandidat = Path.Combine(Path.GetDirectoryName(uiPfad)!, name);
            if (name.Length > 0 && File.Exists(kandidat))
            {
                byte[] data = File.ReadAllBytes(kandidat);
                res.StatusCode = 200;
                res.ContentType = pfad.EndsWith(".js") ? "text/javascript; charset=utf-8" : "text/html; charset=utf-8";
                res.ContentLength64 = data.Length;
                res.OutputStream.Write(data, 0, data.Length); res.OutputStream.Close();
                return;
            }
        }

        if (req.HttpMethod != "POST" || (pfad != "/bake" && pfad != "/tess" && pfad != "/sko" && pfad != "/field-lattice" && pfad != "/boolean"))
        {
            res.StatusCode = 404; SchreibeText(res, "GET / (Vorschau), POST /bake, /tess, /sko, /field-lattice oder /boolean."); return;
        }

        string body;
        using (var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
            body = sr.ReadToEnd();

        string tmpDir = Path.Combine(Path.GetTempPath(), "lattice-fraktal-serve");
        Directory.CreateDirectory(tmpDir);
        string id = Guid.NewGuid().ToString("N");
        string inJson = Path.Combine(tmpDir, id + ".json");
        string outStl = Path.Combine(tmpDir, id + ".stl");
        File.WriteAllText(inJson, body);

        var childArgs = new List<string>();
        if (dll is not null) childArgs.Add(dll);
        if (pfad == "/tess")
        {
            childArgs.Add("tessbake");
            childArgs.Add("--in"); childArgs.Add(inJson);
            childArgs.Add("--out-stl"); childArgs.Add(outStl);
        }
        else if (pfad == "/sko")
        {
            childArgs.Add("skobake");
            childArgs.Add("--in"); childArgs.Add(inJson);
            childArgs.Add("--out-stl"); childArgs.Add(outStl);
        }
        else if (pfad == "/field-lattice")
        {
            childArgs.Add("fieldlatticebake");
            childArgs.Add("--in"); childArgs.Add(inJson);
            childArgs.Add("--out-stl"); childArgs.Add(outStl);
        }
        else if (pfad == "/boolean")
        {
            childArgs.Add("booleanbake");
            childArgs.Add("--in"); childArgs.Add(inJson);
            childArgs.Add("--out-stl"); childArgs.Add(outStl);
        }
        else
        {
            childArgs.Add("bake");
            childArgs.Add("--skelett"); childArgs.Add(inJson);
            childArgs.Add("--out-stl"); childArgs.Add(outStl);
            string? voxel = req.QueryString["voxel"];
            string? fillet = req.QueryString["fillet"];
            if (voxel is not null) { childArgs.Add("--voxel"); childArgs.Add(voxel); }
            if (fillet is not null) { childArgs.Add("--fillet"); childArgs.Add(fillet); }
        }

        Console.WriteLine($"  {pfad} -> Kindprozess ({Path.GetFileName(outStl)}) ...");
        int code = StarteKind(exe, childArgs);

        if (code != 0 || !File.Exists(outStl))
        {
            res.StatusCode = 500;
            SchreibeText(res, $"bake fehlgeschlagen (Exit-Code {code}).");
            Aufraeumen(inJson, outStl);
            return;
        }

        byte[] stl = File.ReadAllBytes(outStl);
        res.StatusCode = 200;
        res.ContentType = "model/stl";
        res.ContentLength64 = stl.Length;
        res.OutputStream.Write(stl, 0, stl.Length);
        res.OutputStream.Close();
        Console.WriteLine($"  -> {stl.Length / 1024.0:F1} KB gesendet.");
        Aufraeumen(inJson, outStl);
    }

    private static int StarteKind(string exe, List<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string a in args) psi.ArgumentList.Add(a);

        using Process? pr = Process.Start(psi);
        if (pr is null) return -1;
        pr.StandardOutput.ReadToEnd();
        pr.StandardError.ReadToEnd();
        pr.WaitForExit();
        return pr.ExitCode;
    }

    private static void SchreibeText(HttpListenerResponse res, string text)
    {
        byte[] b = Encoding.UTF8.GetBytes(text);
        res.ContentType = "text/plain; charset=utf-8";
        res.ContentLength64 = b.Length;
        res.OutputStream.Write(b, 0, b.Length);
        res.OutputStream.Close();
    }

    private static void Aufraeumen(params string[] dateien)
    {
        foreach (string d in dateien)
            try { if (File.Exists(d)) File.Delete(d); } catch { }
    }

    private static int CmdOptimize(string[] args)
    {
        string? pfad = Option(args, "--params");
        if (pfad is null)
        {
            Console.Error.WriteLine("Fehlt: --params <eingabe.json>");
            return 1;
        }

        LatticeParameters? p = LadeParameter(pfad, out string? ladeFehler);
        if (p is null)
        {
            Console.Error.WriteLine($"Konnte Parameterdatei nicht lesen: {ladeFehler}");
            return 1;
        }

        float[] g = p.Bauraum.Groesse ?? new float[] { 50, 50, 50 };
        float[] bp = p.Bauraum.Position ?? new float[] { 0, 0, 0};
        Vector3 boxMin = new(bp[0], bp[1], bp[2]);
        Vector3 boxSize = new(g[0], g[1], g[2]);

        Vector3 quelle = ParameterMapper.Start(p);
        var senken = new List<Vector3>();
        foreach (Target t in ParameterMapper.Ziele(p))
            foreach (Vector3 pt in t.Attraktorpunkte())
                senken.Add(pt);

        OptimierungDto o = p.Optimierung;
        Console.WriteLine($"Fluss-Optimierer: Gitter {o.Aufloesung}³ = {o.Aufloesung * o.Aufloesung * o.Aufloesung} Knoten, γ={o.Gamma:F1}, {o.Iterationen} Iterationen ...");

        // Optimiert ein Netz von 'quelle' zu den Kapillaren (Zielen) und gibt die Beams zurück.
        List<(Vector3 A, Vector3 B, float Radius)> OptimiereVon(Vector3 q, string name)
        {
            var fo = new FlussOptimierer();
            fo.Baue(boxMin, boxSize, o.Aufloesung, q, senken, o.Jitter);
            fo.Laufe(o.Iterationen, o.Rate, o.Gamma, o.Prune);
            fo.GlaetteKnoten(o.Glaettung, 0.5f);
            var b = fo.Beams(o.MaxRadius);
            Console.WriteLine($"  {name}: {fo.SenkenZahl} Kapillaren, {b.Count} Kanäle.");
            return b;
        }

        var beams = new List<(Vector3 A, Vector3 B, float Radius)>();
        if (p.Doppelbaum && p.ZweiterBaum is not null)
        {
            // Wärmetauscher: Netz A (Einlass) und Netz B (Auslass) teilen sich die Kapillaren
            // -> Fluss läuft A → Kapillare → B.
            Vector3 startB = ParameterMapper.Start(p.ZweiterBaum);
            beams.AddRange(OptimiereVon(quelle, "Netz A (Einlass)"));
            beams.AddRange(OptimiereVon(startB, "Netz B (Auslass)"));
        }
        else
        {
            beams = OptimiereVon(quelle, "Netz");
        }
        Console.WriteLine($"Aktive Kanäle nach Optimierung: {beams.Count}.");

        string outStl = Option(args, "--out-stl") ?? "optimiert.stl";
        float voxel = ParseFloat(Option(args, "--voxel"), p.EngineParameter.VoxelGroesse);
        Console.WriteLine($"Rendering (Voxelgröße {voxel:F2} mm; Glättung {o.Glaettung}, Mattheck-Fillet {o.Fillet:F2} mm) ...");
        new LatticeRenderer { VoxelGroesseMm = voxel, FilletMm = o.Fillet }.RendereBeams(beams, outStl);

        var fi = new FileInfo(outStl);
        Console.WriteLine($"STL geschrieben: {fi.FullName} ({fi.Length / 1024.0:F1} KB).");
        return 0;
    }

    private static int CmdValidate(string[] args)
    {
        string? pfad = Option(args, "--params");
        if (pfad is null)
        {
            Console.Error.WriteLine("Fehlt: --params <eingabe.json>");
            return 1;
        }

        LatticeParameters? p = LadeParameter(pfad, out string? ladeFehler);
        if (p is null)
        {
            Console.Error.WriteLine($"Konnte Parameterdatei nicht lesen: {ladeFehler}");
            return 1;
        }

        ValidationResult r = ParameterValidator.Pruefe(p);
        foreach (string w in r.Warnungen) Console.WriteLine($"  [Warnung] {w}");
        foreach (string f in r.Fehler)   Console.WriteLine($"  [Fehler]  {f}");

        if (r.IstGueltig)
        {
            Console.WriteLine($"validate: OK ({r.Warnungen.Count} Warnung(en)).");
            return 0;
        }

        Console.WriteLine($"validate: UNGÜLTIG ({r.Fehler.Count} Fehler, {r.Warnungen.Count} Warnung(en)).");
        return 2;
    }

    /// <summary>Liest und deserialisiert eine Parameterdatei. Gibt null + Grund bei Fehler zurück.</summary>
    internal static LatticeParameters? LadeParameter(string pfad, out string? fehler)
    {
        fehler = null;
        try
        {
            if (!File.Exists(pfad))
            {
                fehler = $"Datei nicht gefunden: {pfad}";
                return null;
            }

            string json = File.ReadAllText(pfad);
            var opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            LatticeParameters? p = JsonSerializer.Deserialize<LatticeParameters>(json, opt);
            if (p is null) fehler = "Leere oder ungültige JSON-Datei.";
            return p;
        }
        catch (JsonException ex)
        {
            fehler = $"JSON-Syntaxfehler: {ex.Message}";
            return null;
        }
        catch (Exception ex)
        {
            fehler = ex.Message;
            return null;
        }
    }

    /// <summary>Liest und deserialisiert eine Skelett-Datei (Skelett-Vertrag).</summary>
    internal static SkelettDto? LadeSkelett(string pfad, out string? fehler)
    {
        fehler = null;
        try
        {
            if (!File.Exists(pfad))
            {
                fehler = $"Datei nicht gefunden: {pfad}";
                return null;
            }

            string json = File.ReadAllText(pfad);
            var opt = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            SkelettDto? s = JsonSerializer.Deserialize<SkelettDto>(json, opt);
            if (s is null) fehler = "Leere oder ungültige Skelett-Datei.";
            return s;
        }
        catch (JsonException ex)
        {
            fehler = $"JSON-Syntaxfehler: {ex.Message}";
            return null;
        }
        catch (Exception ex)
        {
            fehler = ex.Message;
            return null;
        }
    }

    private static string? Option(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }

    private static float ParseFloat(string? s, float fallback)
        => float.TryParse(s, System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out float v)
            ? v : fallback;

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unbekannter Befehl: '{command}'");
        return Usage(1);
    }

    private static int Usage(int exitCode)
    {
        PrintUsage();
        return exitCode;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Verwendung:");
        Console.WriteLine("  lattice-fraktal run      --params <eingabe.json> --out-stl <ausgabe.stl> [--voxel <mm>] [--fillet <mm>]");
        Console.WriteLine("  lattice-fraktal bake     --skelett <skelett.json> --out-stl <ausgabe.stl> [--voxel <mm>] [--fillet <mm>]   (fertiges Skelett voxelisieren)");
        Console.WriteLine("  lattice-fraktal serve    [--url http://localhost:5151/]   (lokaler Bake-Server für die WYSIWYG-Vorschau)");
        Console.WriteLine("  lattice-fraktal optimize --params <eingabe.json> --out-stl <ausgabe.stl> [--voxel <mm>]   (Fluss-Optimierer)");
        Console.WriteLine("  lattice-fraktal validate --params <eingabe.json>");
        Console.WriteLine("  lattice-fraktal help");
        Console.WriteLine();
        Console.WriteLine("Schnell-Schalter für 'run' (überschreiben die JSON-Werte):");
        Console.WriteLine("  --voxel <mm>    Voxelgröße (kleiner = feiner/langsamer, Standard 0.5)");
        Console.WriteLine("  --fillet <mm>   Verrundung der Übergänge; 0 = aus (roher Baum)");
    }
}
