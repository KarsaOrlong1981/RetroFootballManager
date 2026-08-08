# Roadmap

*[English](#english) | [Deutsch](#deutsch)*

Planned features that aren't built yet. Feel free to pick one up in a PR, or open an issue to
discuss design details before starting.

---

## English

### Player contract negotiations (new)

Contract renewals currently happen invisibly (AI-only, single take-it-or-leave-it raise). This
should become a real negotiation the manager actively conducts:

- A **dedicated negotiation screen**, separate from the read-only contract note on the player
  profile, opened per player.
- The manager can submit **multiple offers** in sequence during one negotiation.
- Negotiation terms: **contract length**, **annual salary** (with the resulting **monthly
  salary** shown alongside it), and the player's **squad role** - Star Player, First-Team
  Regular, Impact Sub ("Joker"), Backup, Rotation Player, Youth Prospect.
- Each player has an **expected role** (based on rating, age and talent) that the player
  generator assigns going forward. Offering a role below what the player expects only works if
  the salary offer is **at least 30% above** their current one - otherwise the player won't
  accept regardless of the number.
- The player's **patience** drains with each round, faster or slower depending on rating, age,
  talent and the strength of the league we play in (a fringe player in Tier 4 is patient, a
  star in Tier 1 is not). This must be shown visually as a patience/mood meter.
- If patience runs out, the negotiation **fails outright** ("the collar bursts") - no further
  offer can be made to that player for the rest of the season. The lock resets automatically at
  the next season rollover.
- The opening offer should default to something reasonable, anchored on **what the player has
  earned so far**, and remain fully editable before sending.

**What this needs under the hood** (checked against the current code):
- `Player` has no squad-role field yet - add an `ExpectedRole` (or similar) enum and make
  `PlayerGenerator` compute it from rating/age/talent at generation time.
- `Contract`/`PlayerContractService` already model salary, length and market value end-to-end
  (from M6e) - renewals only need a real negotiation layer on top, not a new data model for the
  contract itself.
- `ContractRenewalAiService` today is a single deterministic AI-only raise with no offers,
  rounds or patience - it stays for COM teams, but the human path needs a new
  `ContractNegotiationService` (offer evaluation against rating/age/talent/league tier/role
  match, patience/frustration state, per-season lock flag reset at season rollover, same place
  `SeasonProgressionService` already resets other season-scoped state).
- New UI: a negotiation page/dialog - the Transfer Market's offer list/row/accept-reject pattern
  (`TransferMarketViewModel`/`TransferMarketPage.xaml`) is the closest existing template to
  adapt rather than build from scratch.

### Merchandise department (new)

A new department to buy fan merchandise at wholesale cost and sell it at a markup:

- At least **10 different articles** (variety to be designed - jerseys, scarves, mugs, etc.).
- **Star-player jerseys**: a jersey featuring the team's current best player should outsell
  the generic ones.
- Revenue should scale with how well the season is going (results, table position) - the
  better the team performs, the more merchandise income it generates.

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

### Spielervertrags-Verhandlungen (neu)

Vertragsverlängerungen laufen aktuell unsichtbar ab (nur KI, eine einzige Alles-oder-nichts-
Erhöhung). Daraus soll eine echte, vom Manager aktiv geführte Verhandlung werden:

- Eine **eigene Verhandlungs-Ansicht**, getrennt von der bisherigen reinen Info-Zeile im
  Spielerprofil, die pro Spieler geöffnet wird.
- Der Manager kann innerhalb einer Verhandlung **mehrere Angebote** nacheinander abgeben.
- Verhandlungspunkte: **Vertragslänge**, **Jahresgehalt** (mit direkt daneben angezeigtem
  daraus resultierendem **Monatsgehalt**) sowie die **Rolle des Spielers im Kader** -
  Starspieler, Stammspieler, Joker, Ersatzspieler, Rotationsspieler, Nachwuchstalent.
- Jeder Spieler bekommt eine **erwartete Rolle** (abhängig von Rating, Alter und Talent), die
  der Spielergenerator künftig mitberechnet. Wird eine niedrigere Rolle angeboten als erwartet,
  lässt sich der Spieler nur überzeugen, wenn das Gehaltsangebot **mindestens 30 % über** seinem
  bisherigen Gehalt liegt - sonst nützt auch ein noch so hohes Angebot nichts.
- Die **Geduld** des Spielers sinkt mit jeder Verhandlungsrunde - schneller oder langsamer je
  nach Rating, Alter, Talent und Stärke der Liga, in der wir spielen (ein Ergänzungsspieler in
  Liga 4 ist geduldig, ein Star in Liga 1 nicht). Das muss optisch als Geduld-/Stimmungsanzeige
  sichtbar sein.
- Ist die Geduld aufgebraucht, **scheitert die Verhandlung endgültig** ("der Kragen platzt") -
  für diesen Spieler ist für den Rest der Saison kein weiteres Angebot mehr möglich. Die Sperre
  wird automatisch mit dem nächsten Saisonwechsel zurückgesetzt.
- Das Startangebot soll von vornherein sinnvoll vorbelegt sein, orientiert an dem, **was der
  Spieler bisher verdient hat**, und vor dem Absenden frei editierbar bleiben.

**Was dafür technisch nötig ist** (gegen den aktuellen Code geprüft):
- `Player` hat noch kein Kader-Rollen-Feld - dafür ein `ExpectedRole`-Enum (o. ä.) ergänzen und
  `PlayerGenerator` bei der Generierung aus Rating/Alter/Talent berechnen lassen.
- `Contract`/`PlayerContractService` bilden Gehalt, Laufzeit und Marktwert bereits vollständig
  ab (aus M6e) - für Verlängerungen fehlt nur die echte Verhandlungsebene darüber, kein neues
  Datenmodell für den Vertrag selbst.
- `ContractRenewalAiService` ist aktuell eine einzige deterministische KI-Erhöhung ohne Angebote,
  Runden oder Geduld - bleibt so für KI-Teams, aber für den menschlichen Pfad braucht es einen
  neuen `ContractNegotiationService` (Angebotsbewertung anhand Rating/Alter/Talent/Liga-Stärke/
  Rollen-Abgleich, Geduld-/Frust-Zustand, Sperr-Flag pro Saison, das an derselben Stelle
  zurückgesetzt wird, an der `SeasonProgressionService` bereits andere saisongebundene Zustände
  zurücksetzt).
- Neue UI: eine Verhandlungs-Seite/-Dialog - das Angebotslisten/-zeilen/Annehmen-Ablehnen-Muster
  des Transfermarkts (`TransferMarketViewModel`/`TransferMarketPage.xaml`) eignet sich am besten
  als Vorlage, statt komplett neu zu bauen.

### Merchandise-Abteilung (neu)

Eine neue Abteilung, um Fanartikel günstig einzukaufen und mit Aufschlag zu verkaufen:

- Mindestens **10 verschiedene Artikel** (Auswahl noch zu gestalten - Trikots, Schals,
  Tassen, etc.).
- **Trikots mit dem besten Spieler**: ein Trikot mit dem aktuell besten Spieler des Teams soll
  sich besser verkaufen als die generischen Trikots.
- Die Einnahmen sollen mit dem Saisonverlauf skalieren (Ergebnisse, Tabellenplatz) - je besser
  es läuft, desto mehr Merchandise-Einnahmen werden generiert.

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

