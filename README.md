# BioniCAD (Public Beta)

Browserbasierte, bionisch/generativ inspirierte Design-Suite (Struktur-Optimierung, Gitter/Voronoi-Tessellation, Space-Colonization-Bäume) mit einem lokalen C#/PicoGK-Rechenkern und einer Three.js-Oberfläche im Browser.

Dies ist ein **Source-Available-Snapshot** (Stand v0.1.3.1) — keine Open-Source-Lizenz, siehe [LICENSE.md](LICENSE.md). Für Kooperation oder Lizenzanfragen: bm@intergga.ch

## Installation (empfohlener Weg — vorgefertigter Installer)

Du brauchst dafür **nur eine einzige Datei**, nicht den ganzen Ordner und nicht das `.bat`-Skript direkt.

1. Auf GitHub zu [`installer/v0.1.3.1/download/BioniCAD-Setup.exe`](installer/v0.1.3.1/download/BioniCAD-Setup.exe) navigieren.
2. Auf der Dateiseite oben rechts das Download-Symbol anklicken (oder „Download raw file“) — dadurch wird **nur** `BioniCAD-Setup.exe` heruntergeladen (ca. 33 MB).
3. Die heruntergeladene `BioniCAD-Setup.exe` doppelklicken und ausführen.
   - Windows zeigt bei unsignierten Installern ggf. eine SmartScreen-Warnung ("Unbekannter Herausgeber"). Falls das passiert: **Weitere Informationen** → **Trotzdem ausführen**.
   - Keine Administratorrechte nötig — die App installiert sich nach `%LocalAppData%\BioniCAD` (nur für dein Benutzerkonto).
4. Nach der Installation über das Icon **BioniCAD** (Startmenü oder Desktop-Verknüpfung, je nach Auswahl im Installer) starten.
   - Das öffnet automatisch ein Server-Fenster und danach den Browser auf `http://localhost:5151/bionicad.html`.
   - Server-Fenster beim Beenden der App einfach schliessen.

**Systemvoraussetzung:** Windows 10/11, x64. Die App ist self-contained (.NET-Runtime ist bereits enthalten, muss nicht separat installiert werden).

## Alternative: aus dem Quellcode selbst bauen

Für alle, die lieber selbst kompilieren statt den fertigen Installer zu nutzen (z. B. um den Code zu prüfen):

```
cd src\LatticeFraktal
dotnet build -c Release
.\bin\Release\net9.0\lattice-fraktal.exe serve --url http://localhost:5151/
```

Voraussetzung: [.NET 9 SDK](https://dotnet.microsoft.com/download). Danach im Browser `http://localhost:5151/bionicad.html` öffnen. Details und Modul-Handbücher: [wiki/README.md](wiki/README.md).

## Was ist enthalten

| Modul | Datei | Zweck |
|---|---|---|
| Mattheck / Struktur | `ui/mattheck.html` | Lastfälle → FEM-Spannung → Topologie-Optimierung, Kerbrundung, Beulanalyse |
| Tessellation | `ui/tessellation.html` | Gitter/Schaum/Voronoi-Zelltypen, Attraktoren, Kraftfluss-Ausrichtung |
| Baum | `ui/baum_vorschau.html` | Space-Colonization-Wachstum entlang Kraftfluss |

Ausführliche Doku im [Wiki](wiki/README.md).

## Lizenz

Source-Available, alle Rechte vorbehalten — siehe [LICENSE.md](LICENSE.md). Ansehen, Verlinken und Zitieren mit Quellenangabe sind erlaubt; Kopieren, Verändern, Weiterverbreiten oder Nutzen des Codes nicht, ohne vorherige Absprache.
