using System.Numerics;
using LatticeFraktal.Model;

namespace LatticeFraktal.Engines;

/// <summary>
/// Gemeinsamer Vertrag aller Skelett-Engines (Spec 1.4): aus Startpunkt und
/// Zielen wird ein Skelett (Knoten + Kanten) erzeugt — noch ohne finale
/// Durchmesser. Hindernisse/Constraint-Voxelfeld folgen in späterer Ausbaustufe.
/// </summary>
public interface IWachstumsEngine
{
    SkelettErgebnis Erzeuge(Vector3 start, IReadOnlyList<Target> ziele, EngineOptionen optionen);
}
