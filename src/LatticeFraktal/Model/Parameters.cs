using System.Text.Json.Serialization;

namespace LatticeFraktal.Model;

// Diese Klassen bilden 1:1 das JSON-Parameter-Schema aus der technischen
// Spezifikation (Abschnitt 3) ab und werden mit System.Text.Json eingelesen.

public sealed class StartDto
{
    [JsonPropertyName("position")] public float[] Position { get; set; } = new float[3];
    [JsonPropertyName("durchmesser")] public float Durchmesser { get; set; }
}

public sealed class ZielDto
{
    [JsonPropertyName("typ")] public string Typ { get; set; } = "partikel";
    [JsonPropertyName("position")] public float[]? Position { get; set; }
    [JsonPropertyName("normale")] public float[]? Normale { get; set; }
    [JsonPropertyName("punkte")] public float[][]? Punkte { get; set; }
    [JsonPropertyName("gewicht")] public float Gewicht { get; set; } = 1.0f;
    [JsonPropertyName("gewicht_profil")] public float[]? GewichtProfil { get; set; }
}

public sealed class PrimitivDto
{
    [JsonPropertyName("typ")] public string Typ { get; set; } = "box";
    [JsonPropertyName("position")] public float[]? Position { get; set; }
    [JsonPropertyName("groesse")] public float[]? Groesse { get; set; }
    [JsonPropertyName("radius")] public float? Radius { get; set; }
}

public sealed class OutputDto
{
    [JsonPropertyName("stl")] public bool Stl { get; set; } = true;
    [JsonPropertyName("voxelfeld")] public bool Voxelfeld { get; set; }
    [JsonPropertyName("protokoll")] public bool Protokoll { get; set; }
}

/// <summary>Parameter für den Fluss-Optimierer (Voxel-Erosion/Aggregation).</summary>
public sealed class OptimierungDto
{
    [JsonPropertyName("aufloesung")] public int Aufloesung { get; set; } = 16;
    [JsonPropertyName("iterationen")] public int Iterationen { get; set; } = 150;
    [JsonPropertyName("rate")] public float Rate { get; set; } = 0.20f;
    [JsonPropertyName("gamma")] public float Gamma { get; set; } = 1.6f;
    [JsonPropertyName("prune")] public float Prune { get; set; } = 0.01f;   // Anteil am Fluss-Bedarf einer Senke (klein!)
    [JsonPropertyName("max_radius")] public float MaxRadius { get; set; } = 2.0f;
    [JsonPropertyName("jitter")] public float Jitter { get; set; } = 0.35f;
    [JsonPropertyName("glaettung")] public int Glaettung { get; set; } = 5;
    [JsonPropertyName("fillet")] public float Fillet { get; set; } = 0.6f;
}

/// <summary>Feinsteuerung der Engine — hält Vorschau und C#-Kern parametergleich.</summary>
public sealed class EngineParameterDto
{
    [JsonPropertyName("schrittweite")] public float Schrittweite { get; set; } = 2.0f;
    [JsonPropertyName("einflussradius")] public float Einflussradius { get; set; } = 16.0f;
    [JsonPropertyName("killabstand")] public float Killabstand { get; set; } = 4.0f;
    [JsonPropertyName("jitter")] public float Jitter { get; set; } = 0.0f;
    [JsonPropertyName("glaettung_paesse")] public int GlaettungPaesse { get; set; } = 2;
    [JsonPropertyName("glaettung_staerke")] public float GlaettungStaerke { get; set; } = 0.5f;
    [JsonPropertyName("murray_exponent")] public float MurrayExponent { get; set; } = 3.0f;
    [JsonPropertyName("min_durchmesser")] public float MinDurchmesser { get; set; } = 0.0f;
    [JsonPropertyName("max_iterationen")] public int MaxIterationen { get; set; } = 4000;
    [JsonPropertyName("seed")] public int Seed { get; set; } = 7;
    [JsonPropertyName("voxel_groesse")] public float VoxelGroesse { get; set; } = 0.5f;
    [JsonPropertyName("durchmesser_glaettung")] public int DurchmesserGlaettung { get; set; } = 0;
    [JsonPropertyName("max_volumenverhaeltnis")] public float MaxVolumenverhaeltnis { get; set; } = 50f;
}

public sealed class LatticeParameters
{
    [JsonPropertyName("start")] public StartDto Start { get; set; } = new();
    [JsonPropertyName("ziele")] public List<ZielDto> Ziele { get; set; } = new();
    [JsonPropertyName("engine")] public string Engine { get; set; } = "space_colonization";
    [JsonPropertyName("kanal_typ")] public string KanalTyp { get; set; } = "solid";
    [JsonPropertyName("wandstaerke")] public float? Wandstaerke { get; set; }
    [JsonPropertyName("doppelbaum")] public bool Doppelbaum { get; set; }
    [JsonPropertyName("zweiter_baum")] public LatticeParameters? ZweiterBaum { get; set; }
    [JsonPropertyName("schleifenanteil")] public float Schleifenanteil { get; set; }
    [JsonPropertyName("bauraum")] public PrimitivDto Bauraum { get; set; } = new();
    [JsonPropertyName("hindernisse")] public List<PrimitivDto> Hindernisse { get; set; } = new();
    [JsonPropertyName("mindestabstand")] public float Mindestabstand { get; set; }

    [JsonPropertyName("verbinde_enden")] public bool VerbindeEnden { get; set; }
    [JsonPropertyName("verbindungsreichweite")] public float Verbindungsreichweite { get; set; } = 6f;

    [JsonPropertyName("pipeline_reihenfolge")]
    public List<string> PipelineReihenfolge { get; set; } =
        new() { "durchmesser", "winkel", "uebergaenge", "schleifen" };

    [JsonPropertyName("pipeline_aktiv")]
    public Dictionary<string, bool> PipelineAktiv { get; set; } = new();

    /// <summary>Verrundungsradius (mm) für den Übergänge-Schritt (Mattheck-Fillet, Spec 4.8).</summary>
    [JsonPropertyName("uebergang_fillet_mm")]
    public float UebergangFilletMm { get; set; } = 1.0f;

    [JsonPropertyName("engine_parameter")]
    public EngineParameterDto EngineParameter { get; set; } = new();

    [JsonPropertyName("optimierung")]
    public OptimierungDto Optimierung { get; set; } = new();

    [JsonPropertyName("output")] public OutputDto Output { get; set; } = new();
}
