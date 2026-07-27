# RetroFootballManager

*[English](#english) | [Deutsch](#deutsch)*

---

## English

A text-based football manager game in the style of classic manager titles (transfers,
training, tactics, leagues, cups, club management) - built with .NET MAUI (C#).

### Open Source

This project is open source. I built it in my spare time as a hobby project and I'm sharing
it so others can build on it, fork it, or just try it out. Contributions (pull requests,
issues, ideas) are welcome.

I'm open to the idea of eventually porting the project from .NET MAUI to a proper game engine
like **Godot**, to get more creative and technical headroom (better graphics/animation,
cross-platform deployment, etc.). This isn't a concrete roadmap item right now, just an
option I'd be happy to discuss with the community if there's interest.

### Current state / known gaps

- The game is currently built **only around the 4 German leagues** (4 leagues of 18 teams
  each, promotion/relegation, plus the German Cup and two fictional European competitions).
  Other countries/league systems aren't supported and would need to be designed from
  scratch.
- **Some club crests are still missing** (not every one of the 72 German clubs has a real
  logo yet), as well as **some menu/background images**.
- It's a single-player game against COM clubs, no multiplayer mode.

If you'd like to contribute artwork (crests, backgrounds) - PRs or issues very welcome!

### Features

**League & competition operation**
- 4 German leagues (18 teams each) with promotion/relegation, auto-generated fixtures and
  tables
- German Cup (knockout, 72 teams)
- Two fictional European competitions ("Europa Pokal der Meister", "Europa Cup") with group
  stage and home/away knockout rounds
- Cup overview page with group tables, knockout rounds and top-scorer lists per competition
- Friendly matches (schedulable during preseason/winter break) and training camps (multiple
  tiers, with morale/attribute bonuses)

**Match day & tactics**
- Live match day with a minute-by-minute ticker (goals, cards, subs, injuries, …), speed
  slider and pause
- In-match team management: formation, drag & drop lineup, tactic/orientation, substitutions
  (max 5), tackling intensity - all changeable live during the match
- Multiple playing styles (counter-attack, tiki-taka, pressing, wing play, crosses to
  striker) x tactical orientation (very defensive to very offensive)
- 8 formations, position-aware lineup selection incl. flexible full-back/wingback duty
- Pre-match analysis from the analysis department (opponent tactics, top-tier analysts reveal
  the exact starting XI)

**Squad & development**
- Individual player attributes, training by category (offense/defense/goalkeeping/fitness),
  seasonal development (youths grow, veterans decline)
- Youth academy with mentoring and promotion into the senior squad
- Detailed player profile (attributes, career/season stats, contract, transfer status)

**Scouting**
- Dedicated scouting department: targeted scouting of individual players (duration depends on
  scout quality), scout recommendations for squad planning
- Offers for scouted-only (unlisted) players from other clubs - the COM club decides on its
  own (refusal, counter-offer, or acceptance)

**Transfer market**
- Offer/buy players for transfer or loan, manage incoming and outgoing offers, negotiate via
  counter-offers
- AI opponents act on their own: list players, submit offers, renew contracts, develop their
  clubs further (stadium, staff)
- Player contracts (duration, salary, market value)

**Club management & finances**
- Stadium expansion (seating/standing/box capacity, comfort/catering/merchandising/
  infrastructure, roof) with a dynamic attendance forecast
- Staff management (coaches, scouts, analysts, physios, …) - hire/fire
- Sponsorship (main/perimeter/kit sponsor)
- Detailed finances (income/expenses, monthly settlement) incl. taking out loans (interest
  rate/term/amortization)
- Calendar fast-forward ("advance time") day by day, automatically stopping at relevant events
  (match day, new inbox message)

**Other**
- Inbox/message system for transfer offers, injuries, expiring contracts, finance warnings,
  scouting results, match analyses and more
- Trophy case (championships across all 4 leagues, German Cup, both European cups)
- Difficulty level affects how active the AI clubs are

### Tech stack

- .NET MAUI (Windows), C#, MVVM (CommunityToolkit.Mvvm)
- SQLite for local storage
- Extensive automated test suite (400+ tests) covering the game logic
  (`RetroFootballManager.Core`)

### Contributing

Issues and pull requests are welcome - bug fixes, new features, artwork contributions, or
translations. For larger changes, please open an issue first to align on the approach.

---

## Deutsch

Ein textbasierter Fußballmanager im Stil klassischer Manager-Spiele (Transfers, Training,
Taktik, Ligen, Pokale, Vereinsführung) - gebaut mit .NET MAUI (C#).

### Open Source

Dieses Projekt ist Open Source. Ich habe es in meiner Freizeit als Hobbyprojekt entwickelt und
teile es, damit andere daran mitbauen, es forken oder einfach ausprobieren können. Beiträge
(Pull Requests, Issues, Ideen) sind willkommen.

Ich stehe dem Gedanken offen gegenüber, das Projekt perspektivisch von .NET MAUI auf eine
richtige Game-Engine wie **Godot** zu portieren, um mehr gestalterische und technische
Möglichkeiten zu haben (bessere Grafik/Animationen, plattformübergreifendes Deployment,
etc.). Das ist aktuell keine konkrete Roadmap-Position, sondern eine Option, über die ich mit
der Community diskutieren würde, falls daran Interesse besteht.

### Aktueller Stand / bekannte Lücken

- Das Spiel ist aktuell **nur auf die 4 deutschen Ligen** ausgelegt (4 Ligen à 18 Teams,
  Auf-/Abstieg, dazu Deutscher Pokal + zwei fiktive europäische Wettbewerbe). Andere Länder/
  Ligensysteme sind nicht vorgesehen bzw. müssten neu konzipiert werden.
- Es **fehlen noch einige Vereinswappen** (nicht jeder der 72 deutschen Vereine hat aktuell ein
  echtes Logo hinterlegt) sowie **einige Menü-/Hintergrundgrafiken**.
- Es ist ein Singleplayer-Spiel gegen COM-Vereine, kein Mehrspielermodus.

Wer Grafiken (Wappen, Hintergründe) beisteuern möchte - sehr gerne per PR oder Issue!

### Features

**Liga- & Wettbewerbsbetrieb**
- 4 deutsche Ligen (je 18 Teams) mit Auf-/Abstieg, automatisch generierten Spielplänen und
  Tabellen
- Deutscher Pokal (K.-o.-System, 72 Teams)
- Zwei fiktive europäische Wettbewerbe ("Europa Pokal der Meister", "Europa Cup") mit
  Gruppenphase und Hin-/Rückspielen im K.-o.-Baum
- Pokal-Übersichtsseite mit Gruppentabellen, K.-o.-Runden und Torschützenlisten je Wettbewerb
- Freundschaftsspiele (planbar in Vorbereitung/Winterpause) und Trainingslager (mehrere Stufen,
  mit Moral-/Attributboni)

**Spieltag & Taktik**
- Live-Spieltag mit Minute-für-Minute-Ticker (Tore, Karten, Wechsel, Verletzungen, …),
  Geschwindigkeitsregler und Pausierbarkeit
- In-Match-Teammanagement: Formation, Aufstellung per Drag & Drop, Taktik/Ausrichtung,
  Wechsel (max. 5), Tackling-Härte - live während des Spiels änderbar
- Mehrere Spielstile (Konterfußball, Tiki-Taka, Pressing, Flügelspiel, Flanken auf den
  Stürmer) x taktische Ausrichtung (sehr defensiv bis sehr offensiv)
- 8 Formationen, positionsgerechte Aufstellung inkl. Flexibel einsetzbarer Außenverteidiger/
  Wingbacks
- Vor-dem-Spiel-Analyse durch die Analyse-Abteilung (gegnerische Taktik, bei guten Analysten
  bis hin zur exakten Startelf)

**Kader & Entwicklung**
- Individuelle Spielerattribute, Training nach Kategorie (Offensiv/Defensiv/Torwart/Fitness),
  saisonale Entwicklung (Jugendliche wachsen, ältere Spieler bauen ab)
- Jugendabteilung mit Mentoring und Beförderung in den Profikader
- Ausführliches Spielerprofil (Attribute, Karriere-/Saisonstatistiken, Vertrag,
  Transferstatus)

**Scouting**
- Eigene Scouting-Abteilung: gezieltes Scouten einzelner Spieler (Zeitdauer, abhängig von der
  Scout-Qualität), Scout-Empfehlungen für die eigene Kaderplanung
- Angebote für ausschließlich gescoutete (nicht gelistete) Spieler anderer Vereine - der
  COM-Verein entscheidet eigenständig (Ablehnung, Gegenangebot oder Zusage)

**Transfermarkt**
- Spieler zum Transfer oder zur Leihe anbieten/kaufen, eingehende und ausgehende Angebote
  verwalten, Verhandlungen mit Gegenangeboten
- KI-Gegner agieren eigenständig: listen Spieler, geben Angebote ab, verlängern Verträge,
  entwickeln ihre Vereine (Stadion, Personal) weiter
- Spielerverträge (Laufzeit, Gehalt, Marktwert)

**Vereinsführung & Wirtschaft**
- Stadionausbau (Sitzplatz-/Steh-/Logenkapazität, Komfort/Catering/Merchandising/
  Infrastruktur, Dach) mit dynamischer Zuschauerzahl-Prognose
- Personalverwaltung (Trainer, Scouts, Analysten, Physios, …) - Einstellen/Entlassen
- Sponsoring (Haupt-/Bande-/Trikotsponsor)
- Detaillierte Finanzen (Einnahmen/Ausgaben, monatliche Abrechnung) inkl. Kreditaufnahme
  (Zinssatz/Laufzeit/Tilgung)
- Kalender-Vorspulen ("Zeit vorstellen") Tag für Tag, hält automatisch an relevanten
  Ereignissen (Spieltag, neue Postfach-Nachricht) an

**Sonstiges**
- Postfach/Nachrichtensystem für Transferangebote, Verletzungen, auslaufende Verträge,
  Finanzwarnungen, Scouting-Ergebnisse, Spielanalysen u.v.m.
- Trophäenschrank (Meisterschaften aller 4 Ligen, Deutscher Pokal, beide Europapokale)
- Schwierigkeitsgrad beeinflusst die Aktivität der KI-Vereine

### Technik

- .NET MAUI (Windows), C#, MVVM (CommunityToolkit.Mvvm)
- SQLite als lokale Speicherung
- Umfangreiche automatisierte Testsuite (400+ Tests) für die Spiellogik (`RetroFootballManager.Core`)

### Mitmachen

Issues und Pull Requests sind willkommen - egal ob Bugfix, neues Feature, Grafik-Beitrag oder
Übersetzung. Bei größeren Änderungen gerne vorher ein Issue eröffnen, um das Vorgehen kurz
abzustimmen.
