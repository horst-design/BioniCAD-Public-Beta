namespace LatticeFraktal.Model;

/// <summary>
/// Eine zusammenhängende Baum-/Skelettstruktur (Knoten + Segmente).
/// Bei einem Doppelbaum (Spec 4.5) existieren zwei unabhängige Tree-Instanzen.
/// </summary>
public sealed class Tree
{
    public Node Wurzel { get; }
    public List<Node> Knoten { get; } = new();
    public List<Segment> Segmente { get; } = new();

    /// <summary>z.B. "heiss" / "kalt" beim Doppelbaum.</summary>
    public string BaumId { get; set; } = "haupt";

    public Tree(Node wurzel)
    {
        Wurzel = wurzel;
        Wurzel.IstWurzel = true;
        Knoten.Add(wurzel);
    }

    /// <summary>Fügt einen Kindknoten an einen bestehenden Elternknoten an.</summary>
    public Node Verzweige(Node eltern, System.Numerics.Vector3 position)
    {
        var kind = new Node(Knoten.Count, position);
        Knoten.Add(kind);
        Segmente.Add(new Segment(eltern, kind));
        return kind;
    }
}
