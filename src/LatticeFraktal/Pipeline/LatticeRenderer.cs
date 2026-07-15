using System.Numerics;
using LatticeFraktal.Model;
using PicoGK;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Rendert einen Tree als PicoGK-Lattice-Beams (Stäbe mit zwei Endradien) in
/// ein Voxelfeld und exportiert ihn als STL (Spec Abschnitt 5).
///
/// PicoGK 2.0 verlangt für Lattice/Voxels die über <c>Library.Go(...)</c>
/// initialisierte Umgebung. Mit <c>bEndAppWithTask: true</c> schließt sich das
/// Viewer-Fenster automatisch, sobald die Aufgabe fertig ist.
///
/// Übergänge (Spec 4.8): Optional werden an jeden Knoten Kugeln gesetzt (saubere,
/// zentrierte Verzweigungen) und ein morphologisches Closing
/// (<c>DoubleOffset(+f, -f)</c>) angewandt — das verrundet konkave Astgabeln
/// kerbspannungsarm (Mattheck), ohne die konvexen Rohrdurchmesser zu verändern.
///
/// Stand dünner Durchstich: nur KanalTyp solid (Vollstäbe).
/// </summary>
public sealed class LatticeRenderer
{
    /// <summary>Kantenlänge eines Voxels in mm. Kleiner = feiner, aber langsamer.</summary>
    public float VoxelGroesseMm { get; init; } = 0.5f;

    /// <summary>Setzt an jeden Knoten eine Kugel (Knotendurchmesser) für zentrierte Verzweigungen.</summary>
    public bool KnotenKugeln { get; init; } = false;

    /// <summary>Verrundungsradius in mm (Closing). 0 = aus.</summary>
    public float FilletMm { get; init; } = 0f;

    /// <summary>
    /// Glättungsverfahren (Tessellation-Kern-Export): "closing" rundet Innenkerben (Kerbspannung↓),
    /// "opening" rundet Außenkanten/entfernt dünne Grate, "beides" = Closing+Opening, "keine" = aus.
    /// </summary>
    public string GlaettungModus { get; init; } = "closing";

    /// <summary>Hohle Strömungskanäle statt Vollstäbe (Spec 4.4).</summary>
    public bool Hohl { get; init; } = false;

    /// <summary>Wandstärke in mm bei hohlen Kanälen.</summary>
    public float Wandstaerke { get; init; } = 0.8f;

    /// <summary>Bei hohlen Kanälen Ein-/Auslässe an Wurzel und Blättern öffnen.</summary>
    public bool OeffneEnden { get; init; } = true;

    /// <summary>
    /// Nur die Wurzeln öffnen, Blätter geschlossen lassen. Für den verbundenen
    /// Doppelbaum: die Blätter sind Kapillar-Verbindungen und dürfen keine Löcher haben.
    /// </summary>
    public bool NurWurzelnOeffnen { get; init; } = false;

    /// <summary>Flansch-Außendurchmesser (mm) an jedem Blatt-Auslass. 0 = kein Flansch.</summary>
    public float FlanschDurchmesser { get; init; } = 0f;

    /// <summary>Flansch-Dicke (mm).</summary>
    public float FlanschDicke { get; init; } = 1.5f;

    /// <summary>
    /// Kleinste lichte Bohrung (Lumen-Radius) in mm — muss voxel-auflösbar sein,
    /// sonst „verschwindet" der Kanal beim Voxelisieren. Skaliert mit der Voxelgröße.
    /// </summary>
    private float MinBohrung => MathF.Max(0.4f, VoxelGroesseMm * 1.5f);

    /// <summary>
    /// Außenradius eines Stabs. Bei hohlen Kanälen wird er so weit angehoben, dass
    /// überall Wandstärke + Mindestbohrung hineinpasst — sonst hätten dünne Blätter
    /// kein Lumen (massiv = verschlossener Auslass).
    /// </summary>
    private float AussenRadius(float durchmesser)
        => Hohl ? MathF.Max(durchmesser * 0.5f, Wandstaerke + MinBohrung)
                : MathF.Max(durchmesser * 0.5f, 0f);

    /// <summary>Lumen-Radius = Außenradius − Wandstärke, mindestens MinBohrung.</summary>
    private float LumenRadius(float durchmesser)
        => MathF.Max(AussenRadius(durchmesser) - Wandstaerke, MinBohrung);

    public void RendereNachStl(Tree baum, string stlPfad)
        => RendereNachStl(new[] { baum }, stlPfad);

    public void RendereNachStl(IReadOnlyList<Tree> baeume, string stlPfad)
    {
        Exception? fehlerImTask = null;

        // Eigene, prozess-eigene Log-Datei, damit wir nicht mit der von PicoGK
        // standardmäßig genutzten (evtl. von einem offenen Viewer gesperrten)
        // Datei kollidieren.
        string logPfad = Path.Combine(Path.GetTempPath(),
            $"lattice-fraktal-picogk-{Environment.ProcessId}.log");

        Library.Go(VoxelGroesseMm, () =>
        {
            try
            {
                var lattice = new Lattice();
                foreach (Tree baum in baeume)
                {
                    foreach (Segment s in baum.Segmente)
                    {
                        // PicoGK arbeitet mit Radien (= halber Durchmesser); runde
                        // Kappen sorgen für glatte Übergänge an den Knoten.
                        float radiusVon = AussenRadius(s.VonNode.Durchmesser);
                        float radiusZu  = AussenRadius(s.ZuNode.Durchmesser);
                        lattice.AddBeam(s.VonNode.Position, s.ZuNode.Position, radiusVon, radiusZu, true);
                    }

                    if (KnotenKugeln)
                    {
                        // Kugel mit dem Knotendurchmesser sorgt für saubere, zentrierte
                        // Verzweigungspunkte (alle Äste gehen aus derselben Kugel hervor).
                        foreach (Node n in baum.Knoten)
                            lattice.AddSphere(n.Position, AussenRadius(n.Durchmesser));
                    }

                    // Flansch-Scheiben: pro Knoten (Node.Flansch*) ODER — falls global gesetzt —
                    // an jedem Blatt. Scheibe steht quer zur Astachse, optional um WinkelX/Y gekippt.
                    {
                        var elternVonF = new Dictionary<int, Node>();
                        var kindVonF = new Dictionary<int, Node>();
                        foreach (Segment s in baum.Segmente)
                        {
                            elternVonF[s.ZuNode.Id] = s.VonNode;
                            if (!kindVonF.ContainsKey(s.VonNode.Id)) kindVonF[s.VonNode.Id] = s.ZuNode;
                        }
                        foreach (Node n in baum.Knoten)
                        {
                            bool proKnoten = n.FlanschDurchmesser > 0f;
                            float fd = proKnoten ? n.FlanschDurchmesser
                                     : (n.IstBlatt ? FlanschDurchmesser : 0f);   // globaler Fallback nur an Blättern
                            if (fd <= 0f) continue;
                            float ft = proKnoten ? n.FlanschDicke : FlanschDicke;
                            if (ft <= 0f) continue;
                            float ax = proKnoten ? n.FlanschWinkelX : 0f;
                            float ay = proKnoten ? n.FlanschWinkelY : 0f;

                            // Basisrichtung: Blatt = vom Eltern weg, Wurzel = zum Kind.
                            Vector3 dirBase;
                            if (elternVonF.TryGetValue(n.Id, out Node? eltern)) dirBase = n.Position - eltern.Position;
                            else if (kindVonF.TryGetValue(n.Id, out Node? kind)) dirBase = kind.Position - n.Position;
                            else continue;

                            Vector3 achse = FlanschAchse(SicherNorm(dirBase), ax, ay);
                            float fr = fd * 0.5f;
                            Vector3 a = n.Position - achse * (ft * 0.5f), b = n.Position + achse * (ft * 0.5f);
                            lattice.AddBeam(a, b, fr, fr, false);   // flache Scheibe
                        }
                    }
                }

                var voxels = new Voxels(lattice);

                // Hohle Kanäle: inneres Lumen (Radius - Wandstärke) abziehen. Damit
                // entsteht ein Rohr mit Wandstärke; an Wurzel + Blättern wird das
                // Lumen nach außen verlängert, damit echte Ein-/Auslässe entstehen.
                if (Hohl && Wandstaerke > 0f)
                {
                    var lumen = new Lattice();
                    foreach (Tree baum in baeume)
                    {
                        foreach (Segment s in baum.Segmente)
                        {
                            float ri = LumenRadius(s.VonNode.Durchmesser);
                            float ro = LumenRadius(s.ZuNode.Durchmesser);
                            lumen.AddBeam(s.VonNode.Position, s.ZuNode.Position, ri, ro, true);
                        }
                        foreach (Node n in baum.Knoten)
                            lumen.AddSphere(n.Position, LumenRadius(n.Durchmesser));

                        if (OeffneEnden)
                            OeffneKanalEnden(baum, lumen);
                    }

                    var lumenVox = new Voxels(lumen);
                    voxels.BoolSubtract(lumenVox);
                }

                if (FilletMm > 0f)
                {
                    // Morphologisches Closing: erst nach außen, dann zurück nach innen.
                    // Verrundet konkave Astgabeln (Kerbspannungsabbau, Mattheck 4.8),
                    // konvexe Rohrquerschnitte bleiben praktisch unverändert.
                    voxels.DoubleOffset(FilletMm, -FilletMm);
                }

                voxels.mshAsMesh().SaveToStlFile(stlPfad);
            }
            catch (Exception e)
            {
                fehlerImTask = e;
            }
        },
        strLogFilePath: logPfad,
        bEndAppWithTask: true);

        if (fehlerImTask is not null)
            throw new Exception($"Rendering fehlgeschlagen: {fehlerImTask.Message}", fehlerImTask);
    }

    /// <summary>
    /// Verlängert das Lumen an Wurzel und Blättern ein Stück nach außen, sodass es
    /// die Außenwand durchstößt — so entstehen offene Ein-/Auslässe (Ports).
    /// </summary>
    private void OeffneKanalEnden(Tree baum, Lattice lumen)
    {
        // Elternzuordnung über die Segmente (ZuNode = Kind, VonNode = Eltern).
        var elternVon = new Dictionary<int, Node>();
        var kindVon = new Dictionary<int, Node>();
        foreach (Segment s in baum.Segmente)
        {
            elternVon[s.ZuNode.Id] = s.VonNode;
            if (!kindVon.ContainsKey(s.VonNode.Id)) kindVon[s.VonNode.Id] = s.ZuNode;
        }

        // Verlängert das Lumen von 'knoten' nach außen, bis es die (verdickte)
        // Außenwand sicher durchstößt -> offener Port.
        void Oeffne(Node knoten, Node nachbar)
        {
            Vector3 dir = SicherNorm(knoten.Position - nachbar.Position);
            float r = LumenRadius(knoten.Durchmesser);
            // Flanschdicke an DIESEM Knoten (pro-Knoten oder globaler Blatt-Fallback).
            float nodeFt = knoten.FlanschDurchmesser > 0f ? knoten.FlanschDicke
                         : (knoten.IstBlatt && FlanschDurchmesser > 0f ? FlanschDicke : 0f);
            // Überstand größer als der Außenradius + Reserve; bei Flansch auch durch die Scheibe.
            float ueberstand = AussenRadius(knoten.Durchmesser) + Wandstaerke + VoxelGroesseMm * 3f + nodeFt;
            Vector3 aus = knoten.Position + dir * ueberstand;
            lumen.AddBeam(knoten.Position, aus, r, r, true);
        }

        // Wurzel: nach außen = von ihrem Kind weg (immer ein Port: Ein-/Auslass).
        if (kindVon.TryGetValue(baum.Wurzel.Id, out Node? wkind))
            Oeffne(baum.Wurzel, wkind);

        // Blätter: beim verbundenen Doppelbaum NICHT öffnen (das sind die
        // Kapillar-Verbindungen — sie müssen dicht bleiben).
        if (NurWurzelnOeffnen) return;

        foreach (Node n in baum.Knoten)
        {
            if (!n.IstBlatt) continue;
            if (elternVon.TryGetValue(n.Id, out Node? eltern))
                Oeffne(n, eltern);
        }
    }

    private static Vector3 SicherNorm(Vector3 v)
    {
        float l = v.Length();
        return l > 1e-6f ? v / l : new Vector3(0f, 0f, 1f);
    }

    private static readonly Vector3 UP = new(0f, 1f, 0f);

    /// <summary>
    /// Achse einer Flansch-Scheibe: Basis UP→dir, dann im lokalen Astframe um
    /// axDeg/ayDeg (Euler 'XYZ') gekippt. Repliziert three.js (setFromUnitVectors +
    /// Euler-XYZ) bit-genau, damit Kern und Vorschau identisch orientieren (WYSIWYG).
    /// </summary>
    private static Vector3 FlanschAchse(Vector3 dir, float axDeg, float ayDeg)
    {
        Quaternion qBase = QuatVonNach(UP, SicherNorm(dir));
        Quaternion qTilt = QuatVonEulerXYZ(axDeg * MathF.PI / 180f, 0f, ayDeg * MathF.PI / 180f);
        Quaternion q = qBase * qTilt;   // System.Numerics * entspricht three.js .multiply
        return Vector3.Normalize(Vector3.Transform(UP, q));
    }

    private static Quaternion QuatVonNach(Vector3 from, Vector3 to)
    {
        from = SicherNorm(from); to = SicherNorm(to);
        float r = Vector3.Dot(from, to) + 1f;
        Quaternion q;
        if (r < 1e-6f)
        {
            if (MathF.Abs(from.X) > MathF.Abs(from.Z)) q = new Quaternion(-from.Y, from.X, 0f, 0f);
            else q = new Quaternion(0f, -from.Z, from.Y, 0f);
        }
        else
        {
            Vector3 c = Vector3.Cross(from, to);
            q = new Quaternion(c.X, c.Y, c.Z, r);
        }
        return Quaternion.Normalize(q);
    }

    /// <summary>Wendet das gewählte morphologische Glättungsverfahren auf das Voxelfeld an.</summary>
    private void WendeGlaettungAn(Voxels voxels)
    {
        if (FilletMm <= 0f) return;
        switch (GlaettungModus)
        {
            case "opening": voxels.DoubleOffset(-FilletMm, FilletMm); break;
            case "beides":  voxels.DoubleOffset(FilletMm, -FilletMm); voxels.DoubleOffset(-FilletMm, FilletMm); break;
            case "keine":   break;
            default:        voxels.DoubleOffset(FilletMm, -FilletMm); break;   // closing
        }
    }

    private static Quaternion QuatVonEulerXYZ(float x, float y, float z)
    {
        float c1 = MathF.Cos(x / 2), c2 = MathF.Cos(y / 2), c3 = MathF.Cos(z / 2);
        float s1 = MathF.Sin(x / 2), s2 = MathF.Sin(y / 2), s3 = MathF.Sin(z / 2);
        return new Quaternion(
            s1 * c2 * c3 + c1 * s2 * s3,
            c1 * s2 * c3 - s1 * c2 * s3,
            c1 * c2 * s3 + s1 * s2 * c3,
            c1 * c2 * c3 - s1 * s2 * s3);
    }

    /// <summary>Rendert eine freie Beam-Liste (z.B. ein optimiertes Netz mit Schleifen) zu STL.</summary>
    public void RendereBeams(IReadOnlyList<(Vector3 A, Vector3 B, float Radius)> beams, string stlPfad)
    {
        Exception? fehlerImTask = null;
        string logPfad = Path.Combine(Path.GetTempPath(),
            $"lattice-fraktal-picogk-{Environment.ProcessId}.log");

        Library.Go(VoxelGroesseMm, () =>
        {
            try
            {
                var lattice = new Lattice();
                foreach ((Vector3 a, Vector3 b, float r) in beams)
                {
                    float radius = MathF.Max(r, 0f);
                    lattice.AddBeam(a, b, radius, radius, true);
                }

                var voxels = new Voxels(lattice);
                WendeGlaettungAn(voxels);
                voxels.mshAsMesh().SaveToStlFile(stlPfad);
            }
            catch (Exception e)
            {
                fehlerImTask = e;
            }
        },
        strLogFilePath: logPfad,
        bEndAppWithTask: true);

        if (fehlerImTask is not null)
            throw new Exception($"Rendering fehlgeschlagen: {fehlerImTask.Message}", fehlerImTask);
    }

    /// <summary>
    /// Rendert ein belegtes Voxelfeld (SKO/CAO-Ergebnis) zu STL. Genutzt vom Server über POST /sko.
    /// <para><b>modus="solid"</b>: jedes Voxel = Kugel (Radius ≈ halbe Rasterweite), +x/+y/+z-Nachbarn per
    /// Beam verbunden — ein „Kapsel-Graph" (Vollmaterial), der die Topologie treu wiedergibt.</para>
    /// <para><b>modus="lattice"</b>: geschlossene Außenhaut (~1 Voxel dick) + im Inneren ein BCC-Strebengitter
    /// (Zellzentrum → 8 Würfelecken; Ecken liegen auf dem Raster, daher sind Nachbarzellen automatisch
    /// verbunden) → leichte, knochenartige Trabekelstruktur. <paramref name="strutR"/> = Strebenradius (mm).</para>
    /// Kern-Voxelgröße (<see cref="VoxelGroesseMm"/>) steuert die Auflösung, <see cref="FilletMm"/>/<see cref="GlaettungModus"/> die Rundung.
    /// </summary>
    public void RendereVoxelfeld(int[] vox, int bx, int by, int bz, float h, string stlPfad,
                                 string modus = "solid", float strutR = 0f,
                                 bool orient = false, float[]? frames = null)
    {
        Exception? fehlerImTask = null;
        string logPfad = Path.Combine(Path.GetTempPath(),
            $"lattice-fraktal-picogk-{Environment.ProcessId}.log");

        var occ = new HashSet<int>(vox);
        int Idx(int i, int j, int k) => (k * by + j) * bx + i;
        bool IsOcc(int i, int j, int k) => i >= 0 && j >= 0 && k >= 0 && i < bx && j < by && k < bz && occ.Contains(Idx(i, j, k));
        bool IsSurface(int i, int j, int k) => !IsOcc(i + 1, j, k) || !IsOcc(i - 1, j, k) || !IsOcc(i, j + 1, k) || !IsOcc(i, j - 1, k) || !IsOcc(i, j, k + 1) || !IsOcc(i, j, k - 1);
        Vector3 Center(int i, int j, int k) => new((i + 0.5f) * h, (j + 0.5f) * h, (k + 0.5f) * h);
        float rSkin = h * 0.5f * 1.08f;                        // Haut/Vollmaterial: leichte Überlappung
        bool gitter = modus == "lattice" || modus == "gitter";
        float rStrut = strutR > 0f ? strutR : h * 0.18f;       // BCC-Strebenradius
        bool useFrames = gitter && orient && frames != null && frames.Length == vox.Length * 12;   // 12 Floats/Voxel: 3×(dir,r)

        Library.Go(VoxelGroesseMm, () =>
        {
            try
            {
                var lattice = new Lattice();
                for (int n = 0; n < vox.Length; n++)
                {
                    int lin = vox[n];
                    int i = lin % bx, j = (lin / bx) % by, k = lin / (bx * by);
                    Vector3 c = Center(i, j, k);
                    if (gitter)
                    {
                        if (useFrames)
                        {
                            // Streben entlang der lokalen Hauptspannungs-Achsen; Länge > h/2 => Überlappung
                            // mit Nachbarzellen (beim Voxelisieren verschmolzen => verbunden).
                            int b0 = n * 12;
                            for (int ax = 0; ax < 3; ax++)
                            {
                                int o = b0 + ax * 4;
                                var d = new Vector3(frames![o], frames[o + 1], frames[o + 2]);
                                float ra = frames[o + 3];
                                if (ra < 1e-4f || d.LengthSquared() < 1e-12f) continue;
                                d = Vector3.Normalize(d);
                                lattice.AddBeam(c - d * (h * 0.6f), c + d * (h * 0.6f), ra, ra, true);
                            }
                        }
                        else
                        {
                            // BCC-Streben: Zellzentrum -> 8 Würfelecken (Ecken auf dem Raster => Nachbarzellen verbunden)
                            for (int dz = 0; dz <= 1; dz++) for (int dy = 0; dy <= 1; dy++) for (int dx = 0; dx <= 1; dx++)
                                lattice.AddBeam(c, new Vector3((i + dx) * h, (j + dy) * h, (k + dz) * h), rStrut, rStrut, true);
                        }
                        // geschlossene Haut nur an Oberflächenvoxeln
                        if (IsSurface(i, j, k))
                        {
                            lattice.AddSphere(c, rSkin);
                            if (IsOcc(i + 1, j, k) && IsSurface(i + 1, j, k)) lattice.AddBeam(c, Center(i + 1, j, k), rSkin, rSkin, true);
                            if (IsOcc(i, j + 1, k) && IsSurface(i, j + 1, k)) lattice.AddBeam(c, Center(i, j + 1, k), rSkin, rSkin, true);
                            if (IsOcc(i, j, k + 1) && IsSurface(i, j, k + 1)) lattice.AddBeam(c, Center(i, j, k + 1), rSkin, rSkin, true);
                        }
                    }
                    else
                    {
                        lattice.AddSphere(c, rSkin);
                        if (IsOcc(i + 1, j, k)) lattice.AddBeam(c, Center(i + 1, j, k), rSkin, rSkin, true);
                        if (IsOcc(i, j + 1, k)) lattice.AddBeam(c, Center(i, j + 1, k), rSkin, rSkin, true);
                        if (IsOcc(i, j, k + 1)) lattice.AddBeam(c, Center(i, j, k + 1), rSkin, rSkin, true);
                    }
                }

                var voxels = new Voxels(lattice);
                WendeGlaettungAn(voxels);
                voxels.mshAsMesh().SaveToStlFile(stlPfad);
            }
            catch (Exception e)
            {
                fehlerImTask = e;
            }
        },
        strLogFilePath: logPfad,
        bEndAppWithTask: true);

        if (fehlerImTask is not null)
            throw new Exception($"Rendering fehlgeschlagen: {fehlerImTask.Message}", fehlerImTask);
    }
}
