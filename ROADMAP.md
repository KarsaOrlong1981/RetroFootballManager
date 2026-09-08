# Roadmap

*[English](#english) | [Deutsch](#deutsch)*

Planned features that aren't built yet. Feel free to pick one up in a PR, or open an issue to
discuss design details before starting.

---

## English

### Infrastructure page (new)

Turn the club's facilities into their own management area instead of just the stadium:

- MainMenu's "Stadion" hotspot becomes an **"Infrastruktur"** hotspot, opening a new
  `InfrastructurePage` (hub/overview) instead of jumping straight into `StadiumPage`.
- The hub lists every manageable facility as its own section/card, each with a level and its
  own upgrade cost (same pattern as `StadiumService`'s existing Comfort/Catering/Merchandise/
  Infrastructure levels - scaling cost per level, deducted via `TryApplyUpgrade`-style balance
  checks). Tapping a card opens that facility's own sub-page.
- **Stadion** - the existing `StadiumPage` moves here unchanged (capacities, ticket prices,
  Comfort/Catering/Merchandise/Infrastructure levels, roof).
- **Jugendeinrichtung** - a facility level (new model, separate from `YouthViewModel`'s
  coaching/mentoring, which stays as-is) that improves youth development speed/quality and
  the number of youth-academy slots as it levels up.
- **Medizinischer Bereich** - a facility level that reduces injury duration/frequency and
  speeds up recovery, synergizing with (not replacing) the existing Physio staff role.
- **Fanbereich** (Fanshop/Vereinskneipe/Imbissbuden/Fan jobs) - grows a new club-membership
  count and matchday-independent income as it levels up; distinct from the existing
  `MerchandiseLevel`/`MerchandiseService` (which stays focused on matchday merch sales).
- **Parkplatzverwaltung** - parking capacity/price as a small extra matchday revenue source,
  with insufficient parking mildly capping attendance growth for high-demand matches.
- Other candidate sections worth considering: a **Trainingsgelände** (training-facility level
  boosting `TrainingService` efficiency, distinct from per-player coaching), and
  **Rasenpflege/Platzwart** (pitch-quality level affecting home advantage/injury risk).
- Every facility's level-up should have a clear, distinct payoff (more fans/attendance, more
  members, more/better staff slots, faster player development, less downtime) so each section
  earns its own space on the hub rather than duplicating an existing system.
- Note: `Stadium.InfrastructureLevel` already exists today as a narrow field (discounts
  stadium capacity-upgrade cost) - avoid confusing it with this new page-level "Infrastruktur"
  concept when implementing; rename or fold it in deliberately, don't let the two drift as
  same-named-but-different things.

### Universe editor (new)

A new "Editor" option in the start menu:

- Works like starting a new game (generates a universe), but everything can then be
  customized: player names and attributes, team names, team crests (via file picker for
  images), and cup/league names.
- For clubs, all club data should be editable too: stadium name and capacity, finances, etc.
- Edited universes must be saved so they can be reloaded at any time.
- A new option, **"Use edited teams"** (checkbox), controls whether starting a new game loads
  the edited universe instead of generating a fresh one. Show the player a short explanation
  of what this checkbox does next to it.

---

## Deutsch

### Infrastruktur-Seite (neu)

Die Vereinseinrichtungen bekommen einen eigenen Verwaltungsbereich statt nur das Stadion:

- Der "Stadion"-Hotspot im Hauptmenü wird zu einem **"Infrastruktur"**-Hotspot und öffnet eine
  neue `InfrastructurePage` (Übersicht/Hub) statt direkt in die `StadiumPage` zu springen.
- Der Hub listet jeden verwaltbaren Bereich als eigene Sektion/Karte mit eigener Stufe und
  eigenen Ausbaukosten (gleiches Muster wie die bestehenden Comfort-/Catering-/Merchandise-/
  Infrastructure-Stufen in `StadiumService` - Kosten steigen pro Stufe, Abzug übers Guthaben
  wie bei `TryApplyUpgrade`). Tippen auf eine Karte öffnet die zugehörige Unterseite.
- **Stadion** - die bestehende `StadiumPage` zieht unverändert hierher um (Kapazitäten,
  Ticketpreise, Comfort-/Catering-/Merchandise-/Infrastructure-Stufen, Dach).
- **Jugendeinrichtung** - eine Gebäude-Stufe (neues Modell, getrennt vom Coaching/Mentoring in
  `YouthViewModel`, das unverändert bleibt), die mit steigendem Level die
  Jugendentwicklungsgeschwindigkeit/-qualität und die Anzahl Jugendakademie-Plätze verbessert.
- **Medizinischer Bereich** - eine Stufe, die Verletzungsdauer/-häufigkeit senkt und die
  Genesung beschleunigt, im Zusammenspiel mit (nicht als Ersatz für) die bestehende
  Physio-Mitarbeiterrolle.
- **Fanbereich** (Fanshop/Vereinskneipe/Imbissbuden/Fanjobs) - lässt mit steigendem Level eine
  neue Vereinsmitgliederzahl sowie spieltagsunabhängige Einnahmen wachsen; getrennt von der
  bestehenden `MerchandiseLevel`/`MerchandiseService` (bleibt auf Spieltags-Merchverkauf
  fokussiert).
- **Parkplatzverwaltung** - Parkplatzkapazität/-preis als kleine zusätzliche Spieltags-
  Einnahmequelle; zu wenig Parkplätze deckelt bei sehr gefragten Spielen leicht das
  Zuschauerwachstum.
- Weitere Kandidaten fürs Nachdenken: ein **Trainingsgelände** (Gebäude-Stufe, die die
  `TrainingService`-Effizienz erhöht, getrennt vom individuellen Spieler-Coaching), sowie
  **Rasenpflege/Platzwart** (Platzqualität-Stufe mit Einfluss auf Heimvorteil/Verletzungsrisiko).
- Jede Ausbaustufe sollte einen klaren, eigenständigen Nutzen bringen (mehr Fans/Zuschauer,
  mehr Mitglieder, mehr/bessere Personal-Slots, schnellere Spielerentwicklung, weniger
  Ausfallzeiten), damit jede Sektion ihre eigene Daseinsberechtigung im Hub hat statt ein
  bestehendes System zu duplizieren.
- Hinweis: `Stadium.InfrastructureLevel` existiert bereits heute als schmales Einzelfeld
  (rabattiert Stadion-Kapazitätsausbau) - bei der Umsetzung nicht mit diesem neuen
  Seiten-Konzept "Infrastruktur" verwechseln; bewusst umbenennen oder einbinden, statt zwei
  gleichnamige, aber unterschiedliche Dinge auseinanderdriften zu lassen.

### Universen-Editor (neu)

Eine neue Option "Editor" im Startmenü:

- Funktioniert wie ein neues Spiel (das Universum wird generiert), aber danach lässt sich
  alles anpassen: Spielernamen und Fähigkeiten, Teamnamen, Vereinswappen (per Dateiauswahl
  für Bilder) sowie Pokal- und Liganamen.
- Bei Clubs sollen ebenfalls alle Daten editierbar sein: Stadionname und -größe, Finanzen usw.
- Editierte Universen müssen gespeichert werden, damit sie jederzeit wieder geladen werden
  können.
- Eine neue Option **"Editierte Teams verwenden"** (Checkbox) legt fest, ob bei "Neues Spiel"
  das editierte Universum geladen wird statt ein neues zu generieren. Neben der Checkbox soll
  dem Spieler kurz erklärt werden, was sie bewirkt.

