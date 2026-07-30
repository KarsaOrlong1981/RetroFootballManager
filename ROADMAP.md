# Roadmap

*[English](#english) | [Deutsch](#deutsch)*

Planned features that aren't built yet. Feel free to pick one up in a PR, or open an issue to
discuss design details before starting.

---

## English

### Sponsor objectives

Currently, sponsor offers (e.g. in League 4) always come as two offers where one is simply
better than the other in every respect - there's no real decision to make. This should become
a proper trade-off:

- At least **3 distinct offers** per sponsor slot.
- **1 unconditional offer**: no performance clause, but a lower base amount.
- **2 conditional offers**: each tied to a season objective that varies per offer (e.g.
  "finish top 5", "win the league", "get promoted", "finish mid-table", …). Meeting the
  condition pays out an extra bonus at season end.
- The size of the bonus should be inversely related to the base offer: a sponsor offering a
  very high bonus for a hard condition should have a lower base amount than the unconditional
  offer, not a higher one. The player has to weigh "can we realistically hit this condition,
  and is the extra payout worth the risk?" before signing.

### Merchandise department (new)

A new department to buy fan merchandise at wholesale cost and sell it at a markup:

- At least **10 different articles** (variety to be designed - jerseys, scarves, mugs, etc.).
- **Star-player jerseys**: a jersey featuring the team's current best player should outsell
  the generic ones.
- Revenue should scale with how well the season is going (results, table position) - the
  better the team performs, the more merchandise income it generates.

### Club morale (new)

A new main-menu section showing two live morale meters: **fan mood** and **board mood**
(both as percentages).

- If **both** drop below 45%, the manager gets a warning that their job is at risk.
- If **board mood** drops below 30%, the game ends (fired).
- If **fan mood** drops below 30%, the game also ends and the manager loses their job.
- Ways to raise morale: match wins, win streaks, stadium expansions (+5% fan mood), advancing
  a cup round (+5% both), winning a cup (+25%, +30% for the Champions League/Europa Cup
  equivalents), winning the league (+30%), promotion (+25%), relegation (-30%), and similar
  events.

### Formation position fixes

The DM/CM assignment and the AV/WB toggle currently don't match real formation logic:

- **4-2-3-1**: the two deepest midfielders should both be CMs, not one CM + one DM.
- **4-3-3**: the middle of the three should be a DM, the other two should be CMs.
- **4-2-2-2**: depending on team mentality, the two deepest midfielders should be CMs
  (offensive), or DMs (defensive and balanced).
- The FB/WB toggle button should only ever appear for LB/RB positions - it currently also
  shows up (incorrectly) for LM/RM.

### Long-term player development

Players should improve not only through training but also gradually over the years, based on
age and talent:

- Younger players develop faster than older ones; higher talent also speeds up growth.
- Growth must stay balanced - no overpowered players as a result.

### Fitness & recovery

- After a match, players should need at least **3 days** to return to 100% fitness, instead of
  bouncing back to full fitness immediately after every game.

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

### Sponsoren-Ziele

Aktuell bestehen Sponsorenangebote (z. B. in Liga 4) immer aus zwei Angeboten, bei denen eines
in jeder Hinsicht einfach besser ist als das andere - eine echte Entscheidung gibt es dabei
nicht. Das soll zu einer echten Abwägung werden:

- Mindestens **3 verschiedene Angebote** pro Sponsorenplatz.
- **1 Angebot ohne Auflagen**: keine Leistungsklausel, dafür ein niedrigerer Grundbetrag.
- **2 Angebote mit Auflagen**: jeweils an ein Saisonziel gekoppelt, das je Angebot variiert
  (z. B. "mindestens 5. Platz", "werde Meister", "steige auf", "Platz im Mittelfeld", …). Wird
  die Auflage erfüllt, gibt es am Saisonende einen zusätzlichen Bonus.
- Die Bonushöhe soll umgekehrt zum Grundangebot stehen: ein Sponsor mit einem sehr hohen Bonus
  für eine schwere Auflage soll ein niedrigeres Grundangebot haben als das auflagenfreie
  Angebot, nicht ein höheres. Der Spieler muss abwägen: "Können wir diese Auflage realistisch
  schaffen, und lohnt sich der zusätzliche Betrag das Risiko?", bevor er unterschreibt.

### Merchandise-Abteilung (neu)

Eine neue Abteilung, um Fanartikel günstig einzukaufen und mit Aufschlag zu verkaufen:

- Mindestens **10 verschiedene Artikel** (Auswahl noch zu gestalten - Trikots, Schals,
  Tassen, etc.).
- **Trikots mit dem besten Spieler**: ein Trikot mit dem aktuell besten Spieler des Teams soll
  sich besser verkaufen als die generischen Trikots.
- Die Einnahmen sollen mit dem Saisonverlauf skalieren (Ergebnisse, Tabellenplatz) - je besser
  es läuft, desto mehr Merchandise-Einnahmen werden generiert.

### Vereinsstimmung (neu)

Eine neue Sektion im Hauptmenü mit zwei Stimmungsanzeigen: **Fan-Stimmung** und
**Vorstands-Stimmung** (jeweils in Prozent).

- Fallen **beide** unter 45 %, erhält der Manager eine Warnung, dass sein Job in Gefahr ist.
- Fällt die **Vorstands-Stimmung** unter 30 %, ist das Spiel vorbei (Entlassung).
- Fällt die **Fan-Stimmung** unter 30 %, ist das Spiel ebenfalls vorbei und der Manager
  verliert seinen Job.
- Möglichkeiten, die Stimmung zu heben: gewonnene Spiele, Siegesserien, Stadionausbau
  (+5 % Fan-Stimmung), Weiterkommen in einer Pokalrunde (+5 % auf beides), Pokalsieg (+25 %,
  bei Champions League/Europa Pokal +30 %), Meisterschaft (+30 %), Aufstieg (+25 %), Abstieg
  (-30 %) und ähnliche Ereignisse.

### Formations-Positionen korrigieren

Die DM/ZM-Zuordnung und der AV/WB-Umschaltknopf entsprechen aktuell nicht der realen
Formationslogik:

- **4-2-3-1**: die beiden tiefsten Mittelfeldspieler sollen beide ZMs sein, nicht ein ZM und
  ein DM.
- **4-3-3**: der mittlere der drei soll ein DM sein, die anderen beiden ZMs.
- **4-2-2-2**: je nach Mannschaftsausrichtung sollen die beiden tiefsten Mittelfeldspieler
  ZMs sein (offensiv) oder DMs (defensiv und ausgeglichen).
- Der AV/WB-Umschaltknopf darf nur bei LV/RV-Positionen erscheinen - aktuell wird er
  fälschlicherweise auch bei LM/RM angezeigt.

### Langfristige Spielerentwicklung

Spieler sollen sich nicht nur durch Training, sondern auch über die Jahre hinweg
weiterentwickeln, abhängig von Alter und Talent:

- Jüngere Spieler entwickeln sich schneller als ältere; höheres Talent beschleunigt die
  Entwicklung zusätzlich.
- Der Fortschritt muss dabei gesund bleiben - es sollen keine überstarken Spieler entstehen.

### Fitness & Regeneration

- Nach einem Spiel sollen Spieler mindestens **3 Tage** benötigen, um wieder auf 100 %
  Fitness zu kommen, statt nach jedem Spiel sofort wieder voll fit zu sein.

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

