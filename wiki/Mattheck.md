# Mattheck / Struktur-Optimierung — Handbuch

Modul `ui/mattheck.html`. Kette: **Bauraum → Voxelgitter → Lastfälle → FEM → Optimierung → Ergebnis-Mesh + Spannungsfarben → Export**.

> Dieses Handbuch wird bei jedem Commit mitgepflegt.

## Start

```
cd "src\LatticeFraktal"
dotnet build -c Release
".\bin\Release\net9.0\lattice-fraktal.exe" serve --url http://localhost:5151/
```
Dann im Browser: `http://localhost:5151/mattheck.html`. **Nach UI-Änderungen hart neu laden (Strg+F5).** (Port 5252 ist auf dieser Maschine gesperrt → 5151 nutzen.)

## Bauraum festlegen

Oben Größe X/Y/Z und Voxelgröße `h` einstellen, oder ein **Bauraum-STL** importieren. Physische Größe bleibt fest; kleinere Voxelgröße = feineres Netz = genauer, aber langsamer.

## Bauraumsperren (Freeze-Zonen)

Panel **„Bauraumsperren"**: mit **＋ Quader / ＋ Kugel / ＋ Zylinder / ＋ STL** Zonen setzen, in denen die Optimierung **nichts verändert**. Pro Sperre ein **Typ** (Dropdown im Editor):

- **Leerraum (No-Go, rot):** kein Material — aus dem Bauraum geschnitten und nie aufgebaut (auch CAO wächst nicht hinein).
- **Vollmaterial (fix, grün):** bleibt immer Material — wird von SKO/SIMP/CAO nie abgetragen (wie geschützte Lager-/Kraft-Zonen).

Position per Gizmo, Größe/Radius/Länge per Zahlen. Werden mit **💾 Lastfall** mitgespeichert; beim Laden eines Presets geleert.

## Lastfälle (mehrere — seit #35)

Panel **„Lastfall"**. Jeder **Lastfall** hat **eigene Lager UND Kräfte**.

- **Fall-Leiste** oben im Panel: Dropdown zum Umschalten, **＋ Fall** (neuer, leerer Fall), **⧉** (aktiven Fall duplizieren), **🗑** (löschen), **Name** und **Gewicht `w`**.
- Der 3D-Editor darunter zeigt/bearbeitet immer den **gewählten** Fall.
- **Gewicht `w`**: wird für die Aggregation „gewichtete Summe" benutzt (siehe unten, in Arbeit).

### Lager, Kräfte, Schrauben

- **＋ Lager** (grün): hält das Teil. Pro Achse ein-/ausschaltbar: alle fest = Einspannung, eine frei = Roller.
- **＋ Kraft** (orange, Pfeil): Kraft Fx/Fy/Fz (N) und optional Drehmoment um die Pfeilachse (Nmm). Vektor per Zahlen, **◈ Kraftvektor ziehen** oder **2 Punkte**.
- **＋ Schraube**: fixierter Zylinder (grounded Bolt). Vorspannung = zusätzliche koaxiale Zylinder-Kraft (axial).
- **Zylinder-Region** (Form „Zylinder"): Bohrung/Lager. Als Lager mit achsen-senkrechten festen DOF = **Bearing** (radial gehalten, axial frei); als Kraft mit „Lagerring-Last" = cosinus-Last über die belastete Bohrungshälfte.
- **Gizmo:** ↔ Verschieben / ⤢ Größe. Region auch per Zahlenfeldern setzen.
- **Achsenfarben:** In den Editor-Feldern sind **X rot, Y grün, Z blau** — passend zu den 3D-Achsen (Gizmo).

## Beispiele / Presets

Oben bei „Bauraum" das Dropdown **Beispiel/Preset** lädt einen fertigen Aufbau (Bauraumgröße + Lastfälle):

- **Kragträger:** einfacher Einstieg, ein Lastfall.
- **Brücke:** zwei Pfeiler-Lager; Lastfälle *verteilte Decklast* + *Einzellast mittig* → symmetrische, tragfähige Brücke.
- **Bracket:** zwei Bolzen-Lager (Zylinder); Lastfälle *Hauptlast −Z*, *Seite +Y*, *Seite −Y* → Konsole, die Zug und Seitenkräfte beidseitig trägt.
- **Tisch:** vier Bein-Lager; Lastfälle *Mittiglast* + *alle 4 Kanten* → symmetrischer Tischrahmen, der Last überall auf der Platte trägt.

**Design-Prinzip:** Die Lastfälle bilden das **reale Lastspektrum** ab, das das Teil aushalten muss — so erzeugt die Worst-Case-Optimierung eine **sinnvolle, benutzbare** Geometrie statt einer künstlichen Einzel-Last. Aggregation steht auf **Worst-Case (KS)**. Eigene Presets: einfach im `PRESETS`-Objekt (in `ui/mattheck.html`) ergänzen.

## Eigengewicht

Checkbox **„Eigengewicht (−Z)"** mit spez. Gewicht γ. Wirkt als Volumenlast in −Z.

## Rechnen & Optimieren

- **🔬 Lösen (FEM):** lineare Voxel-FEM, zeigt von-Mises-Spannung als Farbkarte.
- **SKO (Topologie):** entfernt unterspanntes Material bis zum Zielvolumen (Lager-/Kraft-Zonen bleiben). Schnell, gut für interaktive Vorschau.
- **SIMP-Pro:** Dreifeld-SIMP (Dichtefilter → Heaviside-Projektion + OC). Netzunabhängige, scharfe Stege; Filterradius = Mindest-Stegbreite, β = Kantenschärfe.
- **CAO (Kerbrundung):** trägt an hochbelasteten Oberflächen auf / an unterbelasteten ab → gleichmäßige Oberflächenspannung. Auf Vollteil oder SKO-Ergebnis.
- **Beulanalyse (3a):** kleinster Knick-Lastfaktor λ + Knickform (λ>1 = stabil).
- **Beul-Optimierung (3b):** maximiert das kleinste λ bei festem Volumen (Multi-Mode-Eigenlöser + KS + OC).

Jeder Optimierer (SKO/SIMP/CAO/Beul-Opt) hat **↺ Reset** (Ausgangszustand). **⏹ Abbrechen** (neben „Lösen") stoppt einen laufenden **SKO/SIMP/CAO**-Lauf nach der aktuellen Iteration und zeigt das Zwischenergebnis.

### Ergebnis-Transparenz

Regler **„Ergebnis-Transparenz"** (unter „Lösen"): macht das Ergebnis-Mesh durchscheinend, damit **Kraftflusslinien und Innenstruktur** sichtbar bleiben — hilft beim Editieren und Beurteilen.

## Speichern & Laden

Abschnitt **„Export & Speichern"**:

- **💾 Lastfall / 📂 laden:** speichert/lädt **alle** Lastfälle samt Bauraum-/Material-Einstellungen als JSON. Alte Einzel-Lastfall-Dateien werden beim Laden automatisch migriert.
- **🖼 PNG (+Legende)**, **📦 OBJ (Farbe)** (von-Mises als Vertexfarben), STL aus dem PicoGK-Kern.

## Worst-Case-Optimierung über Lastfälle (#35)

Bei **mehreren** Lastfällen aggregieren **SKO** und **SIMP-Pro** die Fälle in jeder Iteration. Steuerung im Panel „Lastfall" → **Aggregation**:

- **Worst-Case (KS):** verteilt Material so, dass der **ungünstigste** Fall abgesichert ist (glattes Maximum; Schärfe ρ — größer = näher am echten Maximum).
- **Gewichtete Summe:** optimiert den mit den Fall-**Gewichten `w`** gewichteten Durchschnitt.

Die von-Mises-Farbkarte zeigt die **Hüllkurve** (Maximum über alle Fälle). Bei genau einem Lastfall verhält sich alles wie zuvor. Warmstart je Fall hält die Rechenzeit im Rahmen (dennoch ~×N FE-Lösungen pro Iteration).

**CAO** (Kerbrundung) nutzt bei mehreren Fällen ebenfalls die **Spannungs-Hüllkurve** (Maximum über alle Fälle), unabhängig vom KS/Summe-Schalter.

Konzept/Details: `wissensbasis/11_mattheck_multilastfall_konzept_v1.md`.

## 2D-Formfindung

Panel **„2D-Formfindung" → ▦ öffnen** startet ein Modal: auf einer Fläche **Lager** (grün) und **Kräfte** (orange, Fx/Fy) als Rechteck/Kreis **ziehen** → **▶ SIMP** (feine Form) oder **⚡ SKO** (grobe Vorschau) → Dichtefeld. Sehr schnell (2D Plane-Stress). **STL-Schnitt** (＋ STL laden, Rotation X/Y/Z + Schnitt-Position) → die **Querschnitt-Silhouette** wird das 2D-Gebiet (blaue Kontur); ohne STL = Rechteck. **Export** der Form: **SVG/DXF** (Rand-Kontur) und **STL** (Extrusion, nur Außenflächen = wasserdicht), mit **Breite/Dicke (mm)** im Modal. Lager/Kräfte sind nachträglich verschiebbar (ohne Werkzeug anklicken+ziehen); **↺ Reset** löscht nur die Rechnung. Engine `ui/form2d.js`, UI `ui/form2d_ui.js`.

## In Arbeit / geplant

- **Beul-Optimierung** über mehrere Lastfälle (#35b).
- **Performance:** feinere Netze / viele Lastfälle — weitere Optimierung geplant.
