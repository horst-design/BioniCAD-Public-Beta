using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Engines;

/// <summary>
/// Steuerparameter für die Skelett-Engines. Sinnvolle Defaults für eine
/// Szene im Bereich weniger Zentimeter; bei Bedarf später aus dem Bauraum
/// skaliert.
/// </summary>
public sealed class EngineOptionen
{
    /// <summary>Länge eines Wachstumsschritts in mm.</summary>
    public float Schrittweite { get; set; } = 2.0f;

    /// <summary>Reichweite, in der ein Attraktor einen Knoten beeinflusst (mm). &lt;= 0 = unbegrenzt.</summary>
    public float Einflussradius { get; set; } = 16.0f;

    /// <summary>Abstand, ab dem ein Attraktor als "erreicht" gilt und entfernt wird (mm).</summary>
    public float Killabstand { get; set; } = 4.0f;

    /// <summary>Zufällige Richtungsstörung pro Wachstumsschritt (0 = aus).</summary>
    public float Jitter { get; set; } = 0.0f;

    /// <summary>Seed für den Jitter-Zufallsgenerator (Reproduzierbarkeit).</summary>
    public int Seed { get; set; } = 7;

    /// <summary>Sicherheitsgrenze gegen Endlosläufe.</summary>
    public int MaxIterationen { get; set; } = 4000;

    /// <summary>Sperrzonen, die der Baum umwächst.</summary>
    public IReadOnlyList<Hindernis> Hindernisse { get; set; } = Array.Empty<Hindernis>();

    /// <summary>Mindestabstand der Astoberfläche zu Hindernissen/anderem Baum (mm).</summary>
    public float Clearance { get; set; } = 1.0f;

    /// <summary>Optional: Knoten eines anderen Baums, denen ausgewichen wird (Doppelbaum).</summary>
    public IReadOnlyList<Vector3>? VermeideKnoten { get; set; }

    /// <summary>Mindestabstand zu den VermeideKnoten (mm).</summary>
    public float VermeideAbstand { get; set; } = 0f;
}

/// <summary>Ergebnis eines Skelett-Wachstumslaufs inkl. einfacher Kennzahlen.</summary>
public sealed class SkelettErgebnis
{
    public required Model.Tree Baum { get; init; }
    public int ZieleGesamt { get; init; }
    public int ZieleErreicht { get; init; }
    public int Iterationen { get; init; }
}

/// <summary>Ergebnis eines verschränkten Doppelbaum-Laufs.</summary>
public sealed class DoppelErgebnis
{
    public required Model.Tree BaumA { get; init; }
    public required Model.Tree BaumB { get; init; }
    public int ZieleGesamtA { get; init; }
    public int ZieleErreichtA { get; init; }
    public int ZieleGesamtB { get; init; }
    public int ZieleErreichtB { get; init; }
    public int Iterationen { get; init; }
}
