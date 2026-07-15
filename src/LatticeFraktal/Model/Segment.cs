namespace LatticeFraktal.Model;

/// <summary>Solider Stab oder hohler Strömungskanal (Spec 4.4).</summary>
public enum KanalTyp
{
    Solid,
    Hollow
}

/// <summary>
/// Verbindung zwischen zwei Knoten. Der maßgebliche Durchmesser ist der des
/// kindseitigen Knotens (Spec Abschnitt 1.1).
/// </summary>
public sealed class Segment
{
    public Node VonNode { get; }
    public Node ZuNode { get; }

    public float Durchmesser { get; set; }
    public KanalTyp KanalTyp { get; set; } = KanalTyp.Solid;

    /// <summary>Nur bei hohlen Kanälen relevant.</summary>
    public float? Wandstaerke { get; set; }

    public Segment(Node vonNode, Node zuNode)
    {
        VonNode = vonNode;
        ZuNode = zuNode;
    }
}
