# Tessellation — Handbuch

Modul `ui/tessellation.html`. Kette: **Bauraum → Zelltyp/Grundmuster → Attraktoren/Kraftfluss (optional) → Darstellung (Röhren/Linien/Flächen) → Kern-Export (PicoGK, glatt) → Booleans/Export**.

> Dieses Handbuch wird bei jedem Commit mitgepflegt.

## Start

```
cd "src\LatticeFraktal"
dotnet build -c Release
".\bin\Release\net9.0\lattice-fraktal.exe" serve --url http://localhost:5151/
```
Dann im Browser: `http://localhost:5151/tessellation.html`. **Nach UI-Änderungen hart neu laden (Strg+F5).**

## Bauraum

Box (Größe X/Y/Z) oder **＋ Bauraum-STL** importieren. Checkbox **Transparent** zum Durchschauen auf die Struktur darin.

## Grundmuster (Struktur-Typ)

Dropdown **Struktur-Typ**, acht Gittertypen:

- **Periodisches Gitter (Kristall):** klassisches Raumgitter (SC/BCC/FCC/Octet/Diamant über **Kristallsystem**), Zellwinkel α/β/γ frei einstellbar.
- **Voronoi-Schaum (irregulär):** offenzelliger Schaum (Gabriel-Graph) aus zufälligen/manuellen Punkten. Manuelle Punkte (**＋ Voronoi-Punkt**) kommen zu den automatischen dazu.
- **Waben (Honeycomb, extrudiert):** klassische 2D-Sechseck-Wabe, in Z extrudiert.
- **Kelvin-Zelle (BCC-Schaum, 14-Flächner):** Voronoi-Zelle des BCC-Gitters (Tetrakaidekaeder).
- **Weaire-Phelan (A15-Schaum):** Voronoi-Zelle des A15-Gitters, zwei Zellformen (Dodekaeder + Tetrakaidekaeder), geringste bekannte Oberflächenenergie für einen Raum-Schaum.
- **Rhombendodekaeder (FCC-Schaum, 12-Flächner):** Voronoi-Zelle des FCC-Gitters.
- **3D-Waben (Bienenzelle, FCC-Schaum gedreht+gestreckt):** wie Rhombendodekaeder, aber um die 3-fach-Achse gedreht (Sechseck-Querschnitt zeigt nach oben) und um die **Zelllänge** gestreckt — der Bienenwinkel (109,47°) bleibt dabei für jede Zelle exakt erhalten, unabhängig von der Zelllänge. Zelllänge 0 = reines Rhombendodekaeder. Randzellen sind an den Bauraumwänden leicht unregelmässig angeschnitten (grosszügige Marge statt Spiegel-Fix, siehe `wissensbasis/30`). In dieser Version ohne Zonen-Blend/Zelltyp-Mischung, ohne STL-Bauraum, ohne Zellstreckung per Kraftfluss-Attraktor — am aussagekräftigsten im Flächen-Modus.
- **Kagome (Pyrochlore-Netz):** eckenteilende Tetraeder, offenes Fachwerk (kein Schaum). Röhren/Linien zeigen die Fachwerk-Kanten, Flächen die Tetraeder-Dreiecke als perforierte Panele.

„Zellgröße" ist bei den Voronoi-Familien (Kelvin/Weaire-Phelan/Rhombendodekaeder/3D-Waben/Kagome) die FCC/BCC-Kantenlänge `a`.

## Darstellung

Dropdown **Darstellung**:

- **Röhren / Streben (Voll):** jede Gitterkante als runde Strebe (Durchmesser einstellbar).
- **Linien (schnell):** dieselben Kanten als reine Linien, für schnelle Vorschau bei grossen Strukturen.
- **Flächen — geschlossene Zellen/Panele:** nur für Voronoi/Waben/Kelvin/Weaire-Phelan/Rhombendodekaeder/3D-Waben/Kagome — geschlossene Wandplatten statt Streben, mit optionalem Lochrand (siehe AM-Constraints).

## Attraktoren (Dichte · Zelltyp · Streben)

Attraktoren beeinflussen das Gitter innerhalb ihres Radius (linearer Falloff). **＋ Gradient** für einen Punkt-Attraktor (in die Szene klicken zum Platzieren) oder **＋ Bauraum-STL** für Einfluss als Schale um eine Oberfläche.

- **Dichte:** erhöht lokal Zelldichte und Strebendicke.
- **Zelltyp:** beim periodischen Gitter (Kristall) wählt es lokal eine andere Zellform (SC/BCC/FCC/Octet/Diamant). Bei Voronoi/Kelvin/Weaire-Phelan (experimentell) mischt es lokal zwischen diesen drei Typen, da sie dieselbe Zellschnitt-Engine teilen — an Übergängen können unregelmässige Zellen entstehen. Wirkt nicht bei Waben/3D-Waben/Kagome/Kristall.
- **Ziel-Zone + Übergangsunschärfe:** markiert eine Zone, in der statt des Grundmusters das global im Panel „Zielmuster" gewählte Muster erscheint (Zonen-Blend, siehe unten).

## Zielmuster (Zonen-Blend)

Checkbox **Zielmuster aktivieren**. Ziel-Struktur-Typ, -Zelltyp und -Zellgröße sind unabhängig vom Grundmuster wählbar (Kristallwinkel, Wandstärke, Lochrand, Strebendurchmesser und Rotation werden aktuell noch vom Grundmuster übernommen). Wo im Bauraum das Zielmuster erscheint, legen Attraktoren mit angehakter **Ziel-Zone** fest — harte Zonengrenze mit Voronoi-Übergang, funktioniert für jedes Musterpaar. **Übergang: Kristall-Auflösung** erzeugt bei einem beteiligten periodischen Gitter mehr Referenzpunkte an der Nahtstelle (dichteres Übergangsnetz), ohne das gerenderte Kristallgitter selbst zu verändern — hilft gegen lückenhafte Verbindungen bei grosser Zellgrösse.

## Kraftfluss (Glättung · Ausrichtung)

Ein importierter Kraftfluss (aus Mattheck oder Baum, bzw. eigene Linien) kann das Gitter ausrichten:

- **Linien-Glättung:** glättet die importierten Kurven — wirkt auf Rails, Attraktor-Einfluss und Anzeige, bei jeder Struktur.
- **Ausrichten-Stärke** (nur Voronoi-Schaum): dreht Streben im Einflussbereich Richtung Fluss. Bei hohen Werten lösen sich Knoten leicht — dagegen **Kugelgelenke an Knoten** aktivieren (Kugeln verbinden die Enden).
- **Dicken-Stärke:** Streben parallel zum Fluss dicker, quer dünner (0 = aus). Bei **Mindest-Dicke bei schlechter Ausrichtung** = 1.0 verdickt es nur noch gut ausgerichtete Streben, ohne schlecht ausgerichtete unter die Basisdicke zu verdünnen.
- **Rails:** Lastpfade als kräftige Streben; „jede N-te Linie" reduziert die Anzahl, **an Knoten koppeln** (`railSnap`) führt die Rail-Punkte auf die nächsten Netzknoten.

## Streben bearbeiten & Filter

- **🗑 Strut löschen:** Modus an → einzelne Strebe anklicken, oder Shift+Ziehen = Rechteck-Auswahl (löscht alle Streben darin).
- **✂ offene Enden entfernen:** löscht iterativ alle Streben, die nur an einem Ende hängen.
- **Mindest-Strebenlänge (mm):** entfernt kurze Streuner (Rails/Verbinder ausgenommen).
- **Höchst-Strebenlänge (mm):** entfernt unplausibel lange Streben/Wandkanten (Rails ausgenommen, gilt auch im Flächen-Modus für Wandkanten).
- **↺ zurücksetzen:** macht alle Löschungen rückgängig.

## AM-Constraints

Fertigungsbezogene Regler für additive Fertigung (Pulverbett/Harz):

- **Lochrand min (mm):** Mindest-Randbreite je Zellwand im Flächen-Modus (Lochgrösse steuerbar).
- **Mindest-Lochgrösse (mm):** garantiert mindestens diesen Lochdurchmesser je Zellwand — auch wenn Lochrand aus ist oder Wandstärke/Feldeinfluss die Zelle sonst ganz schliessen würden. Zweck: Pulver/Harz kann bei der Nachbearbeitung ausfliessen.

Bei 3D-Waben nutzt der Lochrand einen echten kanten-parallelen Versatz (`insetPolygonOffset`) statt der sonst üblichen Zentrum-Skalierung, wegen der stark länglichen Seitenflächen dieses Zelltyps — Details: `wissensbasis/30`, Abschnitt 8.

## Symmetrie (Spiegeln)

**Hälfte behalten + spiegeln:** die Struktur wird an der/den aktiven Ebene(n) gespiegelt → symmetrisch. Bis zu drei Ebenen (X/Y/Z), Position/Rotation per Gizmo (**bearb.** → Verschieben/Drehen) oder Zahlenfelder.

## Kern-Export (PicoGK · glatt)

**🧊 Kern rendern & anzeigen** sendet die Streben (Röhren/Schaum) oder Wandplatten (Flächen) an den PicoGK-Kern (Server „serve" muss laufen) → glatte Voxel-Geometrie statt der kantigen Vorschau-Mesh. Jede Änderung an den Reglern schaltet zurück auf die Vorschau (erneut rendern nötig). **STL-Wand (Schale):** erzeugt nach dem Kern-Render zusätzlich eine wasserdichte Wand aus der Bauraum-STL und verschmilzt sie mit dem Gitter (`shellunion`).

## Booleans (Kern-Resultat ↔ Bauraum/STL)

Verschneidet das gerenderte Kern-Resultat mit dem Bauraum bzw. einer importierten STL (**Boolean Lattice ∩ Bauraum** u. ä.) — z. B. um das Gitter exakt auf eine Bauteilform zu begrenzen.

## Speichern & Laden

- **💾 Speichern (Datei) / 📂 Laden:** alle Einstellungen (inkl. Attraktoren, Symmetrieebenen, Bauraum-STL-Rohdaten) als JSON.
- Als Teil eines **BioniCAD-Gesamtprojekts (`.bio`)**: die Shell (`bionicad.html`) fragt den Modul-Zustand per `postMessage` ab bzw. spielt ihn zurück (`bioCollect`/`bioApply`) — siehe Wiki-README, Abschnitt „Start & Bedienung".
- **⬇ Kern-STL speichern / ⬇ STL exportieren (Vorschau):** Export der geglätteten Kern-Geometrie bzw. der schnellen Vorschau-Mesh.

## In Arbeit / geplant

- Kraftfluss-Ausrichtung auch im Flächen-Modus (aktuell nur Röhren/Linien bzw. nur Voronoi-Schaum für Dicken-/Ausrichten-Stärke).
- Zonen-Blend/Zelltyp-Mischung und Zellstreckung per Kraftfluss-Attraktor für 3D-Waben (aktuell bewusst ausgeklammert, siehe oben).
