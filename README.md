# RetroFootballManager

Ein textbasierter Fußballmanager im Stil klassischer Manager-Spiele (Transfers, Training,
Taktik, Ligen, Pokale, Vereinsführung) - gebaut mit .NET MAUI (C#).

## Open Source

Dieses Projekt ist Open Source. Ich habe es in meiner Freizeit als Hobbyprojekt entwickelt und
teile es, damit andere daran mitbauen, es forken oder einfach ausprobieren können. Beiträge
(Pull Requests, Issues, Ideen) sind willkommen.

Ich stehe dem Gedanken offen gegenüber, das Projekt perspektivisch von .NET MAUI auf eine
richtige Game-Engine wie **Godot** zu portieren, um mehr gestalterische und technische
Möglichkeiten zu haben (bessere Grafik/Animationen, plattformübergreifendes Deployment,
etc.). Das ist aktuell keine konkrete Roadmap-Position, sondern eine Option, über die ich mit
der Community diskutieren würde, falls daran Interesse besteht.

## Aktueller Stand / bekannte Lücken

- Das Spiel ist aktuell **nur auf die 4 deutschen Ligen** ausgelegt (4 Ligen à 18 Teams,
  Auf-/Abstieg, dazu Deutscher Pokal + zwei fiktive europäische Wettbewerbe). Andere Länder/
  Ligensysteme sind nicht vorgesehen bzw. müssten neu konzipiert werden.
- Es **fehlen noch einige Vereinswappen** (nicht jeder der 72 deutschen Vereine hat aktuell ein
  echtes Logo hinterlegt) sowie **einige Menü-/Hintergrundgrafiken**.
- Es ist ein Singleplayer-Spiel gegen COM-Vereine, kein Mehrspielermodus.

Wer Grafiken (Wappen, Hintergründe) beisteuern möchte - sehr gerne per PR oder Issue!

## Features

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

## Technik

- .NET MAUI (Windows), C#, MVVM (CommunityToolkit.Mvvm)
- SQLite als lokale Speicherung
- Umfangreiche automatisierte Testsuite (400+ Tests) für die Spiellogik (`RetroFootballManager.Core`)

## Mitmachen

Issues und Pull Requests sind willkommen - egal ob Bugfix, neues Feature, Grafik-Beitrag oder
Übersetzung. Bei größeren Änderungen gerne vorher ein Issue eröffnen, um das Vorgehen kurz
abzustimmen.
