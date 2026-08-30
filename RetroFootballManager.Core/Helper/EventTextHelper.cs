using RetroFootballManager.Models;

namespace RetroFootballManager.Helper
{
    // Provides varied event texts for the live ticker instead of repeating the
    // same sentence for every event type.
    public static class EventTextHelper
    {
        public static string DangerousAttackText(Team attackingTeam, Random random) =>
            string.Format(Pick(random, DangerousAttackTemplates), attackingTeam.Name);

        public static string ShotText(Player shooter, Random random) =>
            string.Format(Pick(random, ShotTemplates), shooter.Name);

        public static string ShotOnTargetText(Player shooter, Random random) =>
            string.Format(Pick(random, ShotOnTargetTemplates), shooter.Name);

        public static string SaveText(Team defendingTeam, Random random) =>
            string.Format(Pick(random, SaveTemplates), defendingTeam.Name);

        public static string CornerText(Team attackingTeam, Random random) =>
            string.Format(Pick(random, CornerTemplates), attackingTeam.Name);

        public static string GoalText(Player scorer, Team scoringTeam, Player? assistPlayer, Random random) =>
            assistPlayer is not null
                ? string.Format(Pick(random, GoalWithAssistTemplates), scoringTeam.Name, scorer.Name, assistPlayer.Name)
                : string.Format(Pick(random, GoalSoloTemplates), scoringTeam.Name, scorer.Name);

        public static string PenaltyAwardedText(Team attackingTeam, Player? fouler, Random random) =>
            string.Format(Pick(random, PenaltyTemplates), attackingTeam.Name, fouler?.Name ?? "der Abwehr");

        public static string PenaltyMissedText(Player taker, Random random) =>
            string.Format(Pick(random, PenaltyMissedTemplates), taker.Name);

        public static string PenaltySavedText(Team defendingTeam, Player taker, Random random) =>
            string.Format(Pick(random, PenaltySavedTemplates), defendingTeam.Name, taker.Name);

        public static string FoulText(Player fouler, Random random) =>
            string.Format(Pick(random, FoulTemplates), fouler.Name);

        public static string YellowCardText(Player player, Random random) =>
            string.Format(Pick(random, YellowCardTemplates), player.Name);

        public static string RedCardHardFoulText(Player player, Random random) =>
            string.Format(Pick(random, RedCardHardFoulTemplates), player.Name);

        public static string RedCardSecondYellowText(Player player, Random random) =>
            string.Format(Pick(random, RedCardSecondYellowTemplates), player.Name);

        public static string RedCardProfessionalFoulText(Player player, Random random) =>
            string.Format(Pick(random, RedCardProfessionalFoulTemplates), player.Name);

        public static string InjuryText(Player player, Random random) =>
            string.Format(Pick(random, InjuryTemplates), player.Name);

        public static string OffsideText(Player player, Random random) =>
            string.Format(Pick(random, OffsideTemplates), player.Name);

        public static string FreeKickAwardedText(Team attackingTeam, Player taker, Random random) =>
            string.Format(Pick(random, FreeKickTemplates), attackingTeam.Name, taker.Name);

        // Dramatic filler lines for the live ticker's staged penalty/free-kick reveal (see
        // LiveMatchTicker) - no player name needed since the payoff line (goal/save/miss text)
        // already names the taker.
        public static string PenaltyReadyText(Random random) => Pick(random, PenaltyReadyTemplates);

        public static string PenaltyRunUpText(Random random) => Pick(random, PenaltyRunUpTemplates);

        public static string FreeKickRunUpText(Random random) => Pick(random, FreeKickRunUpTemplates);

        // Picks a teammate as the assist provider for a goal - weighted by crossing and
        // passing accuracy so wingers/midfielders show up as assist givers more often
        // than center-backs or the goalkeeper.
        public static Player? PickAssistCandidate(Player scorer, Team team, Random random)
        {
            var candidates = team.Players
                .Where(p => p.Id != scorer.Id
                         && p.Status == PlayerStatus.InStartingXI
                         && p.EffectivePosition != Position.CentralDefender
                         && p.EffectivePosition != Position.Goalkeeper)
                .ToList();

            if (candidates.Count == 0)
                return null;

            return PickWeighted(candidates, p => (p.CrossingAccuracy * 0.6) + (p.PassingAccuracy * 0.4), random);
        }

        private static Player PickWeighted(List<Player> players, Func<Player, double> weightSelector, Random random)
        {
            double total = players.Sum(weightSelector);
            if (total <= 0)
                return players[random.Next(players.Count)];

            double roll = random.NextDouble() * total;
            double cumulative = 0;
            foreach (var player in players)
            {
                cumulative += weightSelector(player);
                if (roll <= cumulative)
                    return player;
            }

            return players[^1];
        }

        private static string Pick(Random random, string[] templates) => templates[random.Next(templates.Length)];

        private static readonly string[] DangerousAttackTemplates =
        [
            "{0} kombiniert sich gefährlich durch den Strafraum.",
            "{0} baut gefährlich auf und dringt in den Strafraum ein.",
            "Gefährlicher Angriff von {0}!",
            "{0} spielt sich mit schnellen Pässen frei vors Tor.",
            "{0} setzt zum Sturmlauf an - brenzlige Situation!",
        ];

        private static readonly string[] ShotTemplates =
        [
            "{0} schließt ab.",
            "{0} zieht ab aus der Distanz.",
            "{0} sucht sein Glück mit einem Schuss.",
            "{0} nimmt den Ball direkt und schießt.",
            "{0} probiert es aus der zweiten Reihe.",
        ];

        private static readonly string[] ShotOnTargetTemplates =
        [
            "{0} bringt den Ball aufs Tor.",
            "{0} zwingt den Torwart zu einer Aktion.",
            "Gefährlicher Schuss von {0} - aufs Tor!",
            "{0} trifft den Ball sauber und zielt aufs lange Eck.",
        ];

        private static readonly string[] SaveTemplates =
        [
            "Der Torwart von {0} pariert.",
            "Starke Parade des Torwarts von {0}!",
            "Der Keeper von {0} ist zur Stelle.",
            "Der Schlussmann von {0} lenkt den Ball zur Seite.",
        ];

        private static readonly string[] CornerTemplates =
        [
            "Ecke für {0}.",
            "{0} erspielt sich eine Ecke.",
            "Eckball für {0}.",
        ];

        private static readonly string[] GoalSoloTemplates =
        [
            "TOR für {0}! {1} trifft!",
            "TOOOR! {1} trifft für {0}!",
            "{1} trifft eiskalt für {0}!",
            "Da ist das Tor! {1} trifft für {0}.",
        ];

        private static readonly string[] GoalWithAssistTemplates =
        [
            "TOR für {0}! {1} trifft nach Vorlage von {2}!",
            "TOOOR! {2} bedient {1}, der eiskalt für {0} abschließt!",
            "{1} trifft für {0} - {2} hatte den Assist!",
            "Sehenswert: {2} legt auf, {1} verwandelt für {0}!",
        ];

        private static readonly string[] PenaltyTemplates =
        [
            "Elfmeter für {0}! Foul von {1} im Strafraum.",
            "Der Schiedsrichter zeigt auf den Punkt - Elfmeter für {0} nach Foul von {1}!",
            "Strafstoß für {0}! {1} kommt zu spät.",
        ];

        private static readonly string[] PenaltyMissedTemplates =
        [
            "{0} verschießt den Elfmeter!",
            "{0} setzt den Elfmeter daneben!",
            "Vergeben! {0} trifft nicht das Tor.",
        ];

        private static readonly string[] PenaltySavedTemplates =
        [
            "Der Torwart von {0} hält den Elfmeter von {1}!",
            "Starke Parade! Der Keeper von {0} pariert den Strafstoß von {1}.",
            "{1} scheitert vom Punkt am Torwart von {0}!",
        ];

        private static readonly string[] FoulTemplates =
        [
            "Foul von {0}.",
            "{0} kommt zu spät und foult.",
            "Pfiff! Foul von {0}.",
            "{0} bringt seinen Gegenspieler zu Fall.",
        ];

        private static readonly string[] YellowCardTemplates =
        [
            "Gelbe Karte für {0}.",
            "{0} sieht Gelb für das Foulspiel.",
            "Der Schiedsrichter verwarnt {0}.",
        ];

        private static readonly string[] RedCardHardFoulTemplates =
        [
            "Rot für {0}!",
            "Glatt Rot! {0} fliegt vom Platz.",
            "Zu hart! {0} sieht die Rote Karte.",
        ];

        private static readonly string[] RedCardSecondYellowTemplates =
        [
            "Gelb-Rot für {0} nach der zweiten Verwarnung!",
            "{0} sieht die zweite Gelbe Karte und muss vom Platz!",
            "Bitter für {0}: Gelb-Rot nach dem zweiten Foul!",
        ];

        private static readonly string[] RedCardProfessionalFoulTemplates =
        [
            "Rot für {0} wegen Notbremse!",
            "{0} verhindert die klare Torchance und sieht Rot!",
            "Notbremse! {0} muss vom Platz.",
        ];

        private static readonly string[] InjuryTemplates =
        [
            "{0} verletzt sich und muss behandelt werden.",
            "{0} bleibt liegen - Behandlung auf dem Platz nötig.",
            "Sorge um {0}: Der Physiotherapeut muss auf den Platz.",
        ];

        private static readonly string[] OffsideTemplates =
        [
            "Abseits! {0} stand zu früh in der Schusslinie.",
            "Die Fahne geht hoch - Abseits gegen {0}.",
            "{0} läuft sich im Abseits fest.",
        ];

        private static readonly string[] FreeKickTemplates =
        [
            "Freistoß für {0} in aussichtsreicher Position! {1} legt sich den Ball zurecht.",
            "Gefährlicher Freistoß für {0} - {1} übernimmt die Ausführung.",
            "{1} steht bereit für den direkten Freistoß zugunsten von {0}.",
        ];

        private static readonly string[] PenaltyReadyTemplates =
        [
            "Der Schütze legt sich den Ball auf den Punkt und macht sich bereit...",
            "Ruhe im Strafraum - der Elfmeterschütze sammelt sich...",
            "Alle Blicke auf den Punkt - der Schütze macht sich bereit...",
        ];

        private static readonly string[] PenaltyRunUpTemplates =
        [
            "Der Schiedsrichter pfeift an - Anlauf...",
            "Jetzt geht's los - der Schütze läuft an...",
            "Anlauf... und Schuss!",
        ];

        private static readonly string[] FreeKickRunUpTemplates =
        [
            "Die Mauer steht - Anlauf...",
            "Der Schütze läuft an...",
            "Jetzt kommt der Schuss...",
        ];
    }
}
