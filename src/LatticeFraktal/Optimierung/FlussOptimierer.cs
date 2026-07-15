using System.Numerics;

namespace LatticeFraktal.Optimierung;

/// <summary>
/// Selbst-optimierendes Flussnetz auf einem 3D-Gitter (Physarum / Constructal Law).
/// Das Gitter ist das Voxelfeld: Quelle speist Fluss ein, Senken ziehen ihn ab;
/// genutzte Kanäle wachsen, ungenutzte schrumpfen und werden gekappt. Übrig bleibt
/// ein dendritisches Kanalnetz, das als Lattice-Beams zu STL gerendert wird.
///
/// Flusslöser: Hagen-Poiseuille-Leitwert G = D/L pro Kante, Kirchhoff-
/// Massenerhaltung an jedem Knoten -> lineares System für die Drücke (Gauss-Seidel).
/// Anpassung (Tero): D += rate·( f(|Q|) − D ), f sättigend. Kappen: D unter Schwelle.
/// </summary>
public sealed class FlussOptimierer
{
    private Vector3[] _pos = Array.Empty<Vector3>();
    private int[] _ea = Array.Empty<int>();
    private int[] _eb = Array.Empty<int>();
    private float[] _eL = Array.Empty<float>();
    private float[] _eD = Array.Empty<float>();
    private bool[] _alive = Array.Empty<bool>();
    private (int ei, int other)[][] _adj = Array.Empty<(int ei, int other)[]>();
    private float[] _p = Array.Empty<float>();
    private float[] _inj = Array.Empty<float>();
    private int _source;
    private int _ground;
    private List<int> _sinks = new();

    public int KnotenZahl => _pos.Length;

    public int KanteZahlAktiv
    {
        get { int c = 0; foreach (bool a in _alive) if (a) c++; return c; }
    }

    public int SenkenZahl => _sinks.Count;

    /// <summary>Baut das Gittergraph im Bauraum auf und ordnet Quelle/Senken zu.</summary>
    public void Baue(Vector3 boxMin, Vector3 boxSize, int res, Vector3 quelle, IReadOnlyList<Vector3> senken, float jitterFrac)
    {
        res = Math.Max(res, 2);
        int n = res * res * res;
        _pos = new Vector3[n];
        Vector3 d = new(boxSize.X / (res - 1), boxSize.Y / (res - 1), boxSize.Z / (res - 1));
        float jit = MathF.Min(d.X, MathF.Min(d.Y, d.Z)) * jitterFrac;
        var rng = new Random(12345);

        int Idx(int x, int y, int z) => (z * res + y) * res + x;
        for (int z = 0; z < res; z++)
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    Vector3 j = new(
                        ((float)rng.NextDouble() * 2 - 1) * jit,
                        ((float)rng.NextDouble() * 2 - 1) * jit,
                        ((float)rng.NextDouble() * 2 - 1) * jit);
                    _pos[Idx(x, y, z)] = boxMin + new Vector3(x * d.X, y * d.Y, z * d.Z) + j;
                }

        var ea = new List<int>();
        var eb = new List<int>();
        var eL = new List<float>();
        void Add(int i, int j) { ea.Add(i); eb.Add(j); eL.Add(Vector3.Distance(_pos[i], _pos[j])); }

        for (int z = 0; z < res; z++)
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int i = Idx(x, y, z);
                    if (x + 1 < res) Add(i, Idx(x + 1, y, z));
                    if (y + 1 < res) Add(i, Idx(x, y + 1, z));
                    if (z + 1 < res) Add(i, Idx(x, y, z + 1));
                }

        _ea = ea.ToArray();
        _eb = eb.ToArray();
        _eL = eL.ToArray();
        _eD = new float[_ea.Length]; Array.Fill(_eD, 1f);
        _alive = new bool[_ea.Length]; Array.Fill(_alive, true);
        BaueAdj();

        _p = new float[n];
        _source = Naechster(quelle);
        _sinks = new List<int>();
        foreach (Vector3 s in senken)
        {
            int idx = Naechster(s);
            if (idx != _source && !_sinks.Contains(idx)) _sinks.Add(idx);
        }
        if (_sinks.Count == 0) _sinks.Add((_source + 1) % n);
        SetupFlow();
    }

    private void BaueAdj()
    {
        var lists = new List<(int, int)>[_pos.Length];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<(int, int)>();
        for (int e = 0; e < _ea.Length; e++)
        {
            if (!_alive[e]) continue;
            lists[_ea[e]].Add((e, _eb[e]));
            lists[_eb[e]].Add((e, _ea[e]));
        }
        _adj = new (int, int)[_pos.Length][];
        for (int i = 0; i < lists.Length; i++) _adj[i] = lists[i].ToArray();
    }

    private void SetupFlow()
    {
        _inj = new float[_pos.Length];
        _ground = _sinks[0];
        // Gesamtfluss 1: Quelle speist 1 ein, jede Senke zieht 1/Anzahl (Ground absorbiert den Rest).
        float q = 1f / _sinks.Count;
        _inj[_source] = 1f;
        for (int s = 1; s < _sinks.Count; s++) _inj[_sinks[s]] = -q;
    }

    private int Naechster(Vector3 p)
    {
        int bi = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < _pos.Length; i++)
        {
            float dd = Vector3.DistanceSquared(_pos[i], p);
            if (dd < bd) { bd = dd; bi = i; }
        }
        return bi;
    }

    private void Solve(int iters)
    {
        const float omega = 1.0f;    // reines Gauss-Seidel: stabil auch bei extremen Leitwert-Unterschieden
        for (int it = 0; it < iters; it++)
            for (int i = 0; i < _pos.Length; i++)
            {
                if (i == _ground) { _p[i] = 0f; continue; }
                float sumC = 0f, sumCP = 0f;
                foreach ((int ei, int other) in _adj[i])
                {
                    float c = _eD[ei] / _eL[ei];
                    sumC += c;
                    sumCP += c * _p[other];
                }
                if (sumC > 1e-12f)
                {
                    float gs = (_inj[i] + sumCP) / sumC;
                    _p[i] = (1f - omega) * _p[i] + omega * gs;
                }
            }
    }

    private float Fluss(int e) => (_eD[e] / _eL[e]) * (_p[_ea[e]] - _p[_eb[e]]);

    /// <summary>Führt die Optimierung aus: Fluss lösen, Kanäle anpassen, kappen.</summary>
    public void Laufe(int iterationen, float rate, float gamma, float prune, int solverIters = 400)
    {
        float qSink = 1f / Math.Max(_sinks.Count, 1);
        float pruneFlow = prune * qSink;   // Kappen relativ zum Fluss-Bedarf EINER Senke
        var q = new float[_ea.Length];
        int warmup = Math.Min(12, iterationen / 4);   // erst kappen, wenn das Druckfeld steht

        for (int t = 0; t < iterationen; t++)
        {
            Solve(solverIters);

            // Durchfluss EINMAL messen (konsistent: aktuelle D + frisch gelöste Drücke)
            // und denselben Wert für Anpassen UND Kappen verwenden.
            for (int e = 0; e < _ea.Length; e++)
                q[e] = _alive[e] ? MathF.Abs(Fluss(e)) : 0f;

            // Anpassung: superlinear (rich get richer), durch Gesamtfluss 1 von Natur aus beschränkt.
            for (int e = 0; e < _ea.Length; e++)
            {
                if (!_alive[e]) continue;
                _eD[e] += rate * (MathF.Pow(q[e], gamma) - _eD[e]);
                if (_eD[e] < 0f) _eD[e] = 0f;
            }

            // Kappen NACH demselben Fluss: Zuleitungen jeder Senke tragen >= 1/Anzahl und
            // überleben; nur tote Schleifen (fast kein Fluss) verschwinden -> kein Kollaps.
            if (t < warmup) continue;   // Aufwärmphase: noch nicht kappen

            bool changed = false;
            for (int e = 0; e < _ea.Length; e++)
            {
                if (_alive[e] && _ea[e] != _ground && _eb[e] != _ground && q[e] < pruneFlow)
                {
                    _alive[e] = false;
                    changed = true;
                }
            }
            if (changed) BaueAdj();
        }
    }

    /// <summary>
    /// Laplace-Glättung der Knotenpositionen im aktiven Netz: jeder Knoten wandert
    /// Richtung Mittel seiner verbundenen Nachbarn → die Gitter-Zickzacks weichen
    /// organischen Kurven. Quelle und Senken bleiben fix.
    /// </summary>
    public void GlaetteKnoten(int paesse, float staerke)
    {
        if (paesse <= 0 || staerke <= 0f) return;
        var fix = new bool[_pos.Length];
        fix[_source] = true;
        foreach (int s in _sinks) fix[s] = true;

        for (int p = 0; p < paesse; p++)
        {
            var neu = (Vector3[])_pos.Clone();
            for (int i = 0; i < _pos.Length; i++)
            {
                if (fix[i]) continue;
                var nb = _adj[i];
                if (nb.Length == 0) continue;
                Vector3 summe = Vector3.Zero;
                foreach ((int ei, int other) in nb) summe += _pos[other];
                Vector3 mittel = summe / nb.Length;
                neu[i] = _pos[i] + (mittel - _pos[i]) * staerke;
            }
            _pos = neu;
        }
    }

    /// <summary>Aktive Kanäle als Beams (Position A/B + Radius). Radius ∝ D^(1/4)·maxRadius.</summary>
    public List<(Vector3 A, Vector3 B, float Radius)> Beams(float maxRadius)
    {
        float dMax = 1e-6f;
        for (int e = 0; e < _ea.Length; e++)
            if (_alive[e] && _eD[e] > dMax) dMax = _eD[e];

        const float renderCut = 0.08f;   // sehr schwache Kanäle nicht rendern
        var list = new List<(Vector3, Vector3, float)>();
        for (int e = 0; e < _ea.Length; e++)
        {
            if (!_alive[e]) continue;
            float dn = _eD[e] / dMax;
            if (dn < renderCut) continue;
            float r = maxRadius * MathF.Pow(dn, 0.7f);    // kontrastreicher: dünn bleibt dünn
            if (r <= 0f) continue;
            list.Add((_pos[_ea[e]], _pos[_eb[e]], r));
        }
        return list;
    }
}
