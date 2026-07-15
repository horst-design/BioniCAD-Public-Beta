using System.Numerics;

namespace LatticeFraktal.Model;

/// <summary>
/// Abstraktes Ziel (Attraktor) mit Fluss-/Lastgewicht (Spec 1.2 + 4.2).
/// Alle Zieltypen liefern letztlich Attraktorpunkte, auf die die Engine zuwächst.
/// </summary>
public abstract class Target
{
    public float Gewicht { get; set; } = 1.0f;

    /// <summary>Die Punkte, auf die das dendritische Wachstum zustrebt.</summary>
    public abstract IEnumerable<Vector3> Attraktorpunkte();
}

/// <summary>Einzelner Punkt im Volumen (Point-to-Volume).</summary>
public sealed class ZielPartikel : Target
{
    public Vector3 Position { get; set; }

    public override IEnumerable<Vector3> Attraktorpunkte()
    {
        yield return Position;
    }
}

/// <summary>Punkt auf einer Fläche mit Normale (Point-to-Surface).</summary>
public sealed class ZielFlaechenpunkt : Target
{
    public Vector3 Position { get; set; }
    public Vector3 Normale { get; set; }

    public override IEnumerable<Vector3> Attraktorpunkte()
    {
        yield return Position;
    }
}

/// <summary>Kurve auf einer Fläche (dicht gesampelt als Attraktorpunkte).</summary>
public sealed class ZielKurve : Target
{
    public List<Vector3> Punkte { get; set; } = new();
    public List<float>? GewichtProfil { get; set; }

    public override IEnumerable<Vector3> Attraktorpunkte() => Punkte;
}
