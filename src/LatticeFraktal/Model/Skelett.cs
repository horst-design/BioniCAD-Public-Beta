using System.Numerics;
using System.Text.Json.Serialization;

namespace LatticeFraktal.Model;

// ===========================================================================
// Skelett-Vertrag (Phase 1 der Roadmap, siehe wissensbasis/07).
//
// Das Skelett ist die *Design-Absicht*: Mittellinien + Durchmesser pro Knoten.
// Es ist das verbindliche Austauschformat zwischen Vorschau (Design) und Kern
// (Voxelisierung). Der Kern berechnet hier NICHTS neu (kein RNG) — er baut
// exakt die Geometrie, die das Skelett beschreibt. Dadurch entspricht die
// Kern-Ausgabe genau dem, was in der Vorschau entworfen wurde.
// ===========================================================================

/// <summary>Ein Knoten des fertigen Skeletts: Position, Elternindex, Durchmesser.</summary>
public sealed class SkelettKnotenDto
{
    /// <summary>Position [x,y,z] in mm.</summary>
    [JsonPropertyName("pos")] public float[] Pos { get; set; } = new float[3];

    /// <summary>Index des Elternknotens im selben Baum; -1 = Wurzel.</summary>
    [JsonPropertyName("parent")] public int Parent { get; set; } = -1;

    /// <summary>Durchmesser an diesem Knoten in mm (bereits final, z.B. nach Murray).</summary>
    [JsonPropertyName("durchmesser")] public float Durchmesser { get; set; }

    /// <summary>Optionaler Pro-Knoten-Flansch: Außendurchmesser in mm (fehlt/0 = keiner).</summary>
    [JsonPropertyName("flansch_d")] public float? FlanschD { get; set; }
    /// <summary>Flansch-Dicke in mm.</summary>
    [JsonPropertyName("flansch_t")] public float? FlanschT { get; set; }
    /// <summary>Kippwinkel X der Scheibe gegen die Astachse (Grad).</summary>
    [JsonPropertyName("flansch_ax")] public float? FlanschAx { get; set; }
    /// <summary>Kippwinkel Y der Scheibe gegen die Astachse (Grad).</summary>
    [JsonPropertyName("flansch_ay")] public float? FlanschAy { get; set; }
}

/// <summary>Ein zusammenhängender Baum als Knotenliste (Eltern-vor-Kind-Reihenfolge empfohlen).</summary>
public sealed class SkelettBaumDto
{
    [JsonPropertyName("knoten")] public List<SkelettKnotenDto> Knoten { get; set; } = new();

    /// <summary>Optionales Etikett, z.B. "A"/"B" beim Doppelbaum.</summary>
    [JsonPropertyName("id")] public string? Id { get; set; }
}

/// <summary>Wurzel-Objekt der Skelett-Datei.</summary>
public sealed class SkelettDto
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    /// <summary>Voxel-Kantenlänge in mm (kann per --voxel überschrieben werden).</summary>
    [JsonPropertyName("voxel_groesse")] public float VoxelGroesse { get; set; } = 0.5f;

    /// <summary>Mattheck-Verrundungsradius in mm (kann per --fillet überschrieben werden). 0 = aus.</summary>
    [JsonPropertyName("fillet")] public float Fillet { get; set; } = 0f;

    /// <summary>"solid" (Vollstab) oder "hollow" (hohler Strömungskanal mit Wandstärke).</summary>
    [JsonPropertyName("kanal_typ")] public string KanalTyp { get; set; } = "solid";

    /// <summary>Wandstärke in mm bei hohlen Kanälen (Offset nach innen).</summary>
    [JsonPropertyName("wandstaerke")] public float Wandstaerke { get; set; } = 0.8f;

    /// <summary>Flansch-Außendurchmesser in mm an jedem Auslass (0 = kein Flansch).</summary>
    [JsonPropertyName("flansch_d")] public float FlanschD { get; set; } = 0f;

    /// <summary>Flansch-Dicke in mm.</summary>
    [JsonPropertyName("flansch_t")] public float FlanschT { get; set; } = 0f;

    /// <summary>Sollen an die Knoten Kugeln gesetzt werden (zentrierte, lückenfreie Gelenke)?</summary>
    [JsonPropertyName("knoten_kugeln")] public bool KnotenKugeln { get; set; } = true;

    /// <summary>
    /// Welche Kanal-Enden geöffnet werden (nur bei hohlen Kanälen):
    /// "enden" = Wurzel + alle Blätter (Einzelbaum), "wurzeln" = nur die Wurzeln
    /// (verbundener Doppelbaum — die Blätter sind Kapillar-Verbindungen und bleiben dicht).
    /// </summary>
    [JsonPropertyName("oeffnung")] public string Oeffnung { get; set; } = "enden";

    [JsonPropertyName("baeume")] public List<SkelettBaumDto> Baeume { get; set; } = new();
}

/// <summary>Baut aus einem <see cref="SkelettDto"/> die internen <see cref="Tree"/>-Objekte.</summary>
public static class SkelettBauer
{
    /// <summary>
    /// Wandelt das Skelett-DTO in Tree-Instanzen. Gibt null + Grund zurück, wenn
    /// die Struktur ungültig ist (fehlende Wurzel, ungültiger Elternindex usw.).
    /// </summary>
    public static List<Tree>? Baue(SkelettDto dto, out string? fehler)
    {
        fehler = null;
        var baeume = new List<Tree>();

        if (dto.Baeume.Count == 0)
        {
            fehler = "Skelett enthält keine Bäume.";
            return null;
        }

        for (int bi = 0; bi < dto.Baeume.Count; bi++)
        {
            SkelettBaumDto bdto = dto.Baeume[bi];
            int n = bdto.Knoten.Count;
            if (n == 0)
            {
                fehler = $"Baum {bi} enthält keine Knoten.";
                return null;
            }

            SkelettKnotenDto k0 = bdto.Knoten[0];
            if (k0.Parent >= 0)
            {
                fehler = $"Baum {bi}: erster Knoten muss die Wurzel sein (parent = -1).";
                return null;
            }

            var nodes = new Node[n];
            nodes[0] = new Node(0, Vek(k0.Pos)) { Durchmesser = k0.Durchmesser };
            SetzeFlansch(nodes[0], k0);
            var baum = new Tree(nodes[0]) { BaumId = bdto.Id ?? $"baum{bi}" };

            // Knoten anlegen (Id = Index)
            for (int i = 1; i < n; i++)
            {
                SkelettKnotenDto kd = bdto.Knoten[i];
                nodes[i] = new Node(i, Vek(kd.Pos)) { Durchmesser = kd.Durchmesser };
                SetzeFlansch(nodes[i], kd);
                baum.Knoten.Add(nodes[i]);
            }

            // Segmente anhand der Elternindizes
            var kindZahl = new int[n];
            for (int i = 1; i < n; i++)
            {
                int p = bdto.Knoten[i].Parent;
                if (p < 0 || p >= n)
                {
                    fehler = $"Baum {bi}, Knoten {i}: ungültiger Elternindex {p}.";
                    return null;
                }
                var seg = new Segment(nodes[p], nodes[i]) { Durchmesser = nodes[i].Durchmesser };
                baum.Segmente.Add(seg);
                kindZahl[p]++;
            }

            // Blätter markieren
            for (int i = 0; i < n; i++)
                nodes[i].IstBlatt = kindZahl[i] == 0 && i != 0;

            baeume.Add(baum);
        }

        return baeume;
    }

    private static void SetzeFlansch(Node node, SkelettKnotenDto dto)
    {
        if (dto.FlanschD is > 0f)
        {
            node.FlanschDurchmesser = dto.FlanschD.Value;
            node.FlanschDicke = dto.FlanschT ?? 1.5f;
            node.FlanschWinkelX = dto.FlanschAx ?? 0f;
            node.FlanschWinkelY = dto.FlanschAy ?? 0f;
        }
    }

    private static Vector3 Vek(float[] a)
        => new(a.Length > 0 ? a[0] : 0f, a.Length > 1 ? a[1] : 0f, a.Length > 2 ? a[2] : 0f);
}
