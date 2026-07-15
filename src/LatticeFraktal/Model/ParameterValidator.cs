namespace LatticeFraktal.Model;

/// <summary>Ergebnis einer Parameterprüfung: harte Fehler und weiche Warnungen.</summary>
public sealed class ValidationResult
{
    public List<string> Fehler { get; } = new();
    public List<string> Warnungen { get; } = new();
    public bool IstGueltig => Fehler.Count == 0;
}

/// <summary>
/// Plausibilitäts- und Schemaprüfung der Parameter (Spec Abschnitt 6).
/// Läuft IMMER, bevor irgendeine Berechnung beginnt.
/// </summary>
public static class ParameterValidator
{
    private static readonly string[] ErlaubteEngines =
        { "space_colonization", "l_system", "bejan_analytisch", "kombiniert" };

    private static readonly string[] ErlaubteSchritte =
        { "durchmesser", "winkel", "uebergaenge", "schleifen" };

    public static ValidationResult Pruefe(LatticeParameters p)
    {
        var r = new ValidationResult();

        // --- Start ---
        if (p.Start.Position is null || p.Start.Position.Length != 3)
            r.Fehler.Add("start.position muss genau 3 Werte [x,y,z] haben.");
        if (p.Start.Durchmesser <= 0)
            r.Fehler.Add("start.durchmesser muss größer 0 sein.");

        // --- Ziele ---
        if (p.Ziele.Count == 0)
            r.Fehler.Add("Es muss mindestens ein Ziel angegeben werden.");

        for (int i = 0; i < p.Ziele.Count; i++)
        {
            ZielDto z = p.Ziele[i];
            switch (z.Typ.ToLowerInvariant())
            {
                case "partikel":
                    if (z.Position is null || z.Position.Length != 3)
                        r.Fehler.Add($"ziele[{i}] (partikel): position mit 3 Werten erforderlich.");
                    break;
                case "flaechenpunkt":
                    if (z.Position is null || z.Position.Length != 3)
                        r.Fehler.Add($"ziele[{i}] (flaechenpunkt): position mit 3 Werten erforderlich.");
                    if (z.Normale is null || z.Normale.Length != 3)
                        r.Fehler.Add($"ziele[{i}] (flaechenpunkt): normale mit 3 Werten erforderlich.");
                    break;
                case "kurve":
                    if (z.Punkte is null || z.Punkte.Length < 2)
                        r.Fehler.Add($"ziele[{i}] (kurve): mindestens 2 Punkte erforderlich.");
                    break;
                default:
                    r.Fehler.Add($"ziele[{i}]: unbekannter typ '{z.Typ}'. Erlaubt: partikel | flaechenpunkt | kurve.");
                    break;
            }
            if (z.Gewicht <= 0)
                r.Warnungen.Add($"ziele[{i}]: gewicht <= 0 ist ungewöhnlich.");
        }

        // --- Engine ---
        if (Array.IndexOf(ErlaubteEngines, p.Engine.ToLowerInvariant()) < 0)
            r.Fehler.Add($"engine '{p.Engine}' unbekannt. Erlaubt: {string.Join(" | ", ErlaubteEngines)}.");

        // --- Kanal-Typ ---
        string kanal = p.KanalTyp.ToLowerInvariant();
        if (kanal != "solid" && kanal != "hollow")
            r.Fehler.Add("kanal_typ muss 'solid' oder 'hollow' sein.");
        if (kanal == "hollow" && (p.Wandstaerke is null || p.Wandstaerke <= 0))
            r.Fehler.Add("kanal_typ 'hollow' erfordert eine positive wandstaerke.");

        // --- Doppelbaum ---
        if (p.Doppelbaum && p.ZweiterBaum is null)
            r.Fehler.Add("doppelbaum=true erfordert einen zweiter_baum-Block.");

        // --- Schleifen / Mindestabstand ---
        if (p.Schleifenanteil < 0 || p.Schleifenanteil > 1)
            r.Warnungen.Add("schleifenanteil sollte zwischen 0 und 1 liegen.");
        if (p.Mindestabstand < 0)
            r.Fehler.Add("mindestabstand darf nicht negativ sein.");

        // --- Bauraum (vorerst nur Box geprüft) ---
        if (p.Bauraum.Typ.ToLowerInvariant() == "box")
        {
            if (p.Bauraum.Groesse is null || p.Bauraum.Groesse.Length != 3)
                r.Fehler.Add("bauraum (box): groesse mit 3 Werten erforderlich.");
            else if (Array.Exists(p.Bauraum.Groesse, v => v <= 0))
                r.Fehler.Add("bauraum (box): alle groesse-Werte müssen größer 0 sein.");
        }

        // --- Pipeline-Reihenfolge ---
        foreach (string schritt in p.PipelineReihenfolge)
            if (Array.IndexOf(ErlaubteSchritte, schritt.ToLowerInvariant()) < 0)
                r.Fehler.Add($"pipeline_reihenfolge: unbekannter Schritt '{schritt}'. " +
                             $"Erlaubt: {string.Join(" | ", ErlaubteSchritte)}.");

        int idxDurchmesser = p.PipelineReihenfolge.FindIndex(s => s.Equals("durchmesser", StringComparison.OrdinalIgnoreCase));
        int idxUebergaenge = p.PipelineReihenfolge.FindIndex(s => s.Equals("uebergaenge", StringComparison.OrdinalIgnoreCase));
        if (idxDurchmesser >= 0 && idxUebergaenge >= 0 && idxUebergaenge < idxDurchmesser)
            r.Warnungen.Add("pipeline_reihenfolge: 'uebergaenge' vor 'durchmesser' ist fachlich unüblich.");

        return r;
    }
}
