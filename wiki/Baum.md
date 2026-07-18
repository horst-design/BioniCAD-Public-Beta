# Baum — Handbuch

Modul `ui/baum_vorschau.html`. Kette: **Bauraum/Start → Wachstum (Space Colonization) → Verzweigungsregeln/Physik → Kern-Geometrie (PicoGK, echte STL) → Booleans/Export**.

> Dieses Handbuch wird bei jedem Commit mitgepflegt.

## Start

```
cd "src\LatticeFraktal"
dotnet build -c Release
".\bin\Release\net9.0\lattice-fraktal.exe" serve --url http://localhost:5151/
```
Dann im Browser: `http://localhost:5151/baum_vorschau.html`. **Nach UI-Änderungen hart neu laden (Strg+F5).**

## Bauraum & Start

Bauraum-Form/-Größe wie gewohnt. **Start X/Y/Z** setzt den Wurzelpunkt; beim Einzelbaum lässt sich der Start auch frei per Gumball ziehen (Objekt anklicken → Gumball erscheint).

## Wachstum (Engine)

Space-Colonization-Algorithmus: Äste wachsen iterativ Richtung der nächstgelegenen **Zielpunkte** (automatische Zielwolke + manuelle/STL-Ziele), bis sie einen Zielpunkt „einfangen" (Killabstand) oder sich verzweigen.

- **Auto:** Schrittweite/Einflussradius/Killabstand werden automatisch aus dem lokalen Zielabstand berechnet (die drei mm-Regler werden dann übersteuert) — dann nur noch **Buschigkeit** regeln: 0 = dicker Stamm mit wenigen Ästen, 1 = fein und gleichmässig vielverzweigt.
- **Iterationen / Max. Iterationen:** Wachstumsschritte.
- **Jitter (Zufall):** zufällige Richtungsstörung für natürlicheres Wachstum.
- **Tropismus:** gerichteter Wachstumsbias (−1 stark abwärts … +1 stark aufwärts).

## Zielwolke & Attraktoren (Zielbereiche)

Die globale **Zielwolke** verteilt Zielpunkte automatisch im Bauraum (**Anzahl Ziele (global)**, auf 0 stellen um nur Attraktoren/Ziele aus STL-Kolonisierung zu nutzen). Zusätzlich:

- **＋ Zielpunkt:** manuell in die Szene klicken (auf Fläche/halbe Höhe).
- **Attraktoren:** setzen zusätzliche Zielpunkte gezielt in einem Bereich, zur globalen Wolke hinzu — mit **Gewichtung der Ziele**.
- **Ziele → Punkte:** wandelt die automatische Wolke + STL-Kolonisierung in bearbeitbare Einzelpunkte um (Verschieben per Gumball, löschen mit Entf).

## STL-Import

Mehrere importierte Objekte werden getrennt behandelt; jedem wird eine **Rolle** zugewiesen:

- **Oberfläche/Volumen kolonisieren:** liefert Zielpunkte auf/in der importierten Form (Anzahl über „Zielpunkte" steuerbar).
- **Bauraum:** definiert die äussere Form, in der gewachsen wird.
- **Hindernis (kollisionsfrei):** Äste weichen aus, nutzt den **Clearance**-Regler.

Importe sind sitzungsbezogen.

## Hindernisse (Sperrzonen)

Eigene, im 3D-Fenster platzierbare Hindernisse (Quader/Kugel/Zylinder) zusätzlich zu importierten STL-Hindernissen. Anklicken → Gumball (Ziehen verschiebt/skaliert, Zahlenfelder bleiben synchron).

## Template / Verzweigungsregeln

Steuert die Ast-Geometrie nach dem Wachstumsschritt:

- **Astwinkel (°) / Astwinkel-Variation (°):** typischer Verzweigungswinkel und dessen Streuung.
- **Durchmesserverhältnis (0–1) / Murray-Exponent:** wie sich der Durchmesser an Verzweigungen auf Tochteräste verteilt (Murrays Gesetz — minimaler Strömungswiderstand bei gegebenem Materialaufwand).
- **Min. Ast-Verhältnis (Kind/Eltern):** Untergrenze, damit Tochteräste nicht beliebig dünn werden.
- **Ast-Verhältnis begrenzen (Fertigung, verfälscht Murray):** Fertigungs-Kompromiss gegen zu dünne Äste, auch wenn das die reine Murray-Optimalität verletzt.
- **Natürlichkeit (perfekt → organisch) / Ordnung (gewachsen → perfekt):** Regler zwischen mathematisch exakter und organisch wirkender Form.
- **Kurvigkeit (Spline):** 0 = gerade Segmente, 1 = weiche Spline-Kurven.
- **Glättung (Knoten) / Glättung Pässe / Glättung Stärke:** Nachglättung des Skeletts.

## 🌊 Kraftfluss-Verzahnung

Importierter Kraftfluss (z. B. aus Mattheck) kann das Wachstum beeinflussen: **Folge-Intensität** steuert, wie stark Äste an den Fluss-Linien „kleben". **Fluss-Punktdichte** regelt Detail/Folgetreue, **Fluss-Exponent γ** die Nichtlinearität des Einflusses.

## Doppelbaum (Wärmetauscher)

Zwei ineinander wachsende Bäume (z. B. Ein-/Auslass eines Wärmetauschers) mit eigenen **Flansch-Enden**: „Flansch an" + Modus, dann im 3D-Fenster auf einen Ast klicken, um dort einen Anschlussflansch zu setzen.

## Physik (Durchfluss-Simulation)

Hagen-Poiseuille-Modell: Einlass = Wurzel (Druck), Auslässe = Umgebung. Farbdarstellung Rot = hoher Druck (Einlass), Blau = Umgebung (Auslass) — **Druckfeld in 3D einfärben** / **Auslasswerte im 3D anzeigen**.

- **Durchfluss: dicke Enden drosseln** bzw. **dünne Kanäle aufweiten:** zwei Wege zum gleichen Auslass-Durchfluss — (1) dicke/kurze Enden verjüngen (weniger Gesamtdurchsatz) oder (2) dünne/lange Äste verdicken (mehr Material). Wirkung ablesbar am Analysewert „Fluss-Ausgleich" (Ziel: → 1.0). Beide kombinierbar.
- **Einlassdruck (bar) / Medium:** Randbedingungen der Simulation.
- **Hohle Kanäle (Strömung):** wirkt erst in der echten Kern-Geometrie (🧊) und im Export — die Live-Vorschau zeigt weiterhin Vollkörper.

## 🍃 Enden verbinden (Blattadern / Kapillaren)

Verbindet nahe beieinanderliegende Astenden nachträglich (Kapillar-Verbindungen, ähnlich Blattadern) — macht das Netz robuster/vermascht statt rein baumförmig.

## Kollision

Killabstand/Trennabstand-Regler gegen zu dicht wachsende, sich überschneidende Äste; **Stopp vor Obj.** (−1 aus, 0 = berühren, >0 = Anschnitt-Spalt) und **Umwachsen** (−1 aus, ≥0 = Äste weichen dem Hindernis mit diesem Abstand aus) steuern das Verhalten an Hindernissen/STL-Importen im Detail.

## Kern-Geometrie (echte STL / 🧊)

**🧊** rendert die echte PicoGK-Voxel-Geometrie (Server „serve" muss laufen) — glatte, wasserdichte Röhren statt der kantigen Vorschau. **Voxel (mm)** bestimmt die Auflösung: kleiner = feiner, aber langsamer (gilt auch für den Skelett-Export/bake).

## Booleans (Resultat ↔ Bauraum/STL)

Verrechnet die zuletzt gerenderte Kern-Geometrie mit der Bauraum-STL oder einer importierten STL: **Vereinen**, **Abziehen**, **Split** (innen/aussen) — wasserdicht im Kern. Reihenfolge: erst 🧊 Kern-Geometrie rendern, dann Boolean anwenden; Ergebnis über **⬇ Echte STL** sichern.

## Export

- **⬇ eingabe.json:** reproduziert den Baum im C#/PicoGK-Tool (Rohdaten für den Kern).
- **⬇ Echte STL:** die gerenderte/verrechnete Kern-Geometrie.
- **Export-Dateiname:** frei wählbar.

## In Arbeit / geplant

- Weitere Feinabstimmung der Kollisions-/Umwachsen-Logik an komplexen STL-Hindernissen.
