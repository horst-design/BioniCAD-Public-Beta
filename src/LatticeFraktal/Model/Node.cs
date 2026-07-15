using System.Numerics;

namespace LatticeFraktal.Model;

/// <summary>
/// Ein Knoten im Baum: Wurzel-, Verzweigungs- oder Blattpunkt.
/// Entspricht "Node" aus der technischen Spezifikation (Abschnitt 1.1).
/// </summary>
public sealed class Node
{
    public int Id { get; }
    public Vector3 Position { get; set; }

    /// <summary>Nach Bejan/Murray zugewiesen (Schritt "Durchmesser").</summary>
    public float Durchmesser { get; set; }

    public bool IstWurzel { get; set; }
    public bool IstBlatt { get; set; }

    /// <summary>Pro-Knoten-Flansch: Außendurchmesser in mm (0 = kein Flansch an diesem Knoten).</summary>
    public float FlanschDurchmesser { get; set; } = 0f;
    /// <summary>Flansch-Dicke in mm.</summary>
    public float FlanschDicke { get; set; } = 0f;
    /// <summary>Kippwinkel X der Scheibe gegen die Astachse (Grad).</summary>
    public float FlanschWinkelX { get; set; } = 0f;
    /// <summary>Kippwinkel Y der Scheibe gegen die Astachse (Grad).</summary>
    public float FlanschWinkelY { get; set; } = 0f;

    public Node(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }
}
