# BioniCAD — Wiki & Bedienungsanleitung

Willkommen im Kompendium zu **BioniCAD**, einer browserbasierten bionischen/generativen Design-Suite mit C#/PicoGK-Kern.

> **Pflege-Regel:** Dieses Wiki ist ein *lebendes Dokument*. Es wird **bei jedem Commit mitgepflegt** — neue Funktionen, Änderungen und Korrekturen werden hier eingetragen bzw. erweitert. Die Versionsgeschichte liegt in git (kein separates `_vN`-Schema nötig).

## Was ist BioniCAD?

Eine Werkzeugsammlung, um Bauteile nach Vorbildern aus der Natur zu erzeugen und mechanisch zu optimieren. Der Kern rechnet lokal (Voxel-Geometrie, FEM, Booleans); die Oberfläche läuft im Browser (Three.js, kein Build-Schritt).

## Module

| Modul | Datei | Zweck |
|---|---|---|
| **Mattheck / Struktur** | `ui/mattheck.html` | Lastfälle → FEM-Spannung → Topologie-Optimierung (SKO, SIMP-Pro), Kerbrundung (CAO), Beulanalyse/-optimierung, Booleans. → [Handbuch](Mattheck.md) |
| **Tessellation** | `ui/tessellation.html` | Gitter/Schaum/Voronoi, Attraktoren, Kraftfluss, Bauraum-STL, Booleans. Kelvin/Weaire-Randzellen enden seit #71 flach an den Bauraumwänden (Spiegel-Aussaat — behebt die "random struts", siehe BIONICAD_CONTEXT.md #71). |
| **Baum** | `ui/baum_vorschau.html` | Space-Colonization-Baum, Wachstum entlang Kraftfluss, Booleans. |

## Grundbegriffe

- **Einheiten:** mm, N, MPa (N/mm²). Stahl: E ≈ 210 000 MPa, spez. Gewicht γ ≈ 7,66·10⁻⁵ N/mm³.
- **Lastfall:** eine Kombination aus **Lagern** (wo ist das Teil gehalten) und **Kräften/Momenten** (was greift an). Seit #35 sind **mehrere Lastfälle** möglich (siehe Mattheck-Handbuch).
- **Bauraum:** der zur Verfügung stehende Raum (Box oder importiertes STL), der zu einem **Voxelgitter** wird, auf dem gerechnet wird.
- **Kraftfluss:** die Linien, entlang derer die Last durch das Bauteil „fließt" — Grundlage für bionische Strukturen.

## Inhaltsverzeichnis

- [Mattheck / Struktur-Optimierung](Mattheck.md)
- *(weitere Modul-Handbücher folgen)*

## Start & Bedienung (Kurz)

1. Kern bauen und Server starten (Details im Mattheck-Handbuch, Abschnitt „Start").
2. **Shell öffnen: `http://localhost:5151/bionicad.html`** — alle Module in **Tabs** (Mattheck/Tessellation/Baum). Wechseln verliert keine Daten, und ein Modul rechnet im Hintergrund weiter, während du in einem anderen arbeitest. Einzelne Module gehen weiterhin direkt, z. B. `…/mattheck.html`. (Hinweis: `/` und `/index.html` sind vom Kern-Server für die Standard-UI reserviert, daher heißt die Shell `bionicad.html`.)
3. **Nach JS/UI-Änderungen: hart neu laden (Strg+F5).**
