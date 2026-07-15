using System.Numerics;

namespace LatticeFraktal.Model;

/// <summary>Wandelt die eingelesenen JSON-DTOs in Domänenobjekte um.</summary>
public static class ParameterMapper
{
    public static Vector3 Start(LatticeParameters p) => Vec(p.Start.Position);

    public static List<Target> Ziele(LatticeParameters p)
    {
        var liste = new List<Target>();
        foreach (ZielDto z in p.Ziele)
        {
            switch (z.Typ.ToLowerInvariant())
            {
                case "partikel":
                    liste.Add(new ZielPartikel { Position = Vec(z.Position!), Gewicht = z.Gewicht });
                    break;
                case "flaechenpunkt":
                    liste.Add(new ZielFlaechenpunkt
                    {
                        Position = Vec(z.Position!),
                        Normale = Vec(z.Normale!),
                        Gewicht = z.Gewicht,
                    });
                    break;
                case "kurve":
                    var kurve = new ZielKurve { Gewicht = z.Gewicht };
                    foreach (float[] pt in z.Punkte!)
                        kurve.Punkte.Add(Vec(pt));
                    if (z.GewichtProfil is not null)
                        kurve.GewichtProfil = new List<float>(z.GewichtProfil);
                    liste.Add(kurve);
                    break;
            }
        }
        return liste;
    }

    public static List<Hindernis> Hindernisse(LatticeParameters p)
    {
        var liste = new List<Hindernis>();
        foreach (PrimitivDto h in p.Hindernisse)
        {
            Vector3 pos = h.Position is not null ? Vec(h.Position) : Vector3.Zero;
            if (h.Typ.ToLowerInvariant() == "box")
            {
                Vector3 g = h.Groesse is not null ? Vec(h.Groesse) : new Vector3(10, 10, 10);
                liste.Add(new Hindernis { IstBox = true, Zentrum = pos, HalbGroesse = g * 0.5f });
            }
            else
            {
                liste.Add(new Hindernis { IstBox = false, Zentrum = pos, Radius = h.Radius ?? 5f });
            }
        }
        return liste;
    }

    private static Vector3 Vec(float[] a) => new(a[0], a[1], a[2]);
}
