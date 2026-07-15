using PicoGK;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Generische Booleschen Operationen zwischen zwei STL-Körpern (A = Resultat, B = Bauraum
/// oder importierte STL), plus wasserdichte Schale (Shell). Alles im PicoGK-Voxelraum:
/// robust, wasserdicht, unabhängig von der Mesh-Qualität der Eingaben.
///
///   op = "union"      -> A ∪ B
///        "subtract"   -> A − B            (Split „außen behalten")
///        "intersect"  -> A ∩ B            (Split „innen behalten")
///        "shell"      -> Schale von A     (Dicke = wall, nach innen)
///        "shellunion" -> A ∪ Schale(B)    (STL-Wand + Lattice verschmolzen)
///        "solid"      -> nur A voxelisiert + optional Fillet (kein zweiter Körper nötig;
///                         genutzt für Tessellation-Flächen/Platten-Netze -> glatte Kern-Geometrie)
/// </summary>
public static class BooleanOps
{
    public static void Render(string aStlPath, string? bStlPath, string op,
                              float wall, float voxelSize, float fillet, string outStlPath)
    {
        Exception? fehler = null;
        string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lattice-fraktal-picogk-{Environment.ProcessId}.log");
        op = (op ?? "union").ToLowerInvariant();

        Library.Go(MathF.Max(voxelSize, 0.05f), () =>
        {
            try
            {
                Voxels vA = VoxFromStl(aStlPath);
                Voxels result;

                switch (op)
                {
                    case "subtract":
                        result = vA; result.BoolSubtract(VoxFromStl(bStlPath)); break;
                    case "intersect":
                        result = vA; result.BoolIntersect(VoxFromStl(bStlPath)); break;
                    case "shell":
                        result = Shell(aStlPath, wall); break;
                    case "shellunion":
                        result = vA; result.BoolAdd(Shell(bStlPath, wall)); break;
                    case "solid":
                        result = vA; break;
                    case "union":
                    default:
                        result = vA; result.BoolAdd(VoxFromStl(bStlPath)); break;
                }

                if (fillet > 0f) result.DoubleOffset(fillet, -fillet);   // leichte Kantenglättung
                result.mshAsMesh().SaveToStlFile(outStlPath);
            }
            catch (Exception e) { fehler = e; }
        }, strLogFilePath: log, bEndAppWithTask: true);

        if (fehler != null) throw new Exception($"Boolean-Operation fehlgeschlagen: {fehler.Message}", fehler);
    }

    static Voxels VoxFromStl(string? path)
    {
        if (path is null || !System.IO.File.Exists(path))
            throw new Exception("Zweiter Körper (STL) fehlt für diese Operation.");
        return new Voxels(Mesh.mshFromStlFile(path));
    }

    /// <summary>Wasserdichte Schale: Vollkörper − (Vollkörper nach innen versetzt) = Wand der Dicke wall.</summary>
    static Voxels Shell(string? path, float wall)
    {
        wall = MathF.Max(wall, 0.1f);
        Voxels solid = VoxFromStl(path);
        Voxels inner = VoxFromStl(path);
        inner.Offset(-wall);               // nach innen schrumpfen
        solid.BoolSubtract(inner);         // nur die Wand bleibt
        return solid;
    }
}
