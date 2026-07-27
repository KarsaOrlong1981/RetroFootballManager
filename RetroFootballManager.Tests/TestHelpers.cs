using RetroFootballManager.Models;

namespace RetroFootballManager.Tests
{
    public static class TestHelpers
    {
        private static int _nextPlayerId = 1;

        // Eine schlichte 1-4-4-2 Standardformation, damit Tests mit positionsabhängiger
        // Logik (z.B. Assist-Auswahl) nicht nur Torhüter/Innenverteidiger zur Auswahl haben.
        private static readonly Position[] LineupPositions =
        [
            Position.Goalkeeper,
            Position.LeftDefender, Position.CentralDefender, Position.CentralDefender, Position.RightDefender,
            Position.LeftMidfielder, Position.CentralMidfielder, Position.CentralMidfielder, Position.RightMidfielder,
            Position.Forward, Position.Forward,
        ];

        public static Team CreateTeam(
            string name,
            int baseRating,
            PlayingStyle style = PlayingStyle.CounterAttack,
            TacticalOrientation orientation = TacticalOrientation.Balanced,
            Stadium? stadium = null,
            int morale = 50,
            int fitness = 90)
        {
            var team = new Team
            {
                Name = name,
                PlayingStyle = style,
                TacticalOrientation = orientation,
                Stadium = stadium,
                Statistics = new TeamStats(),
            };

            // Form so gestalten, dass CalculateMorale ungefähr dem gewünschten Wert entspricht,
            // sonst direkten Boost verwenden.
            team.Statistics.TeamMeeting();
            while (team.Statistics.Morale < morale)
                team.Statistics.BonusPayment();

            for (int i = 0; i < 11; i++)
            {
                team.Players.Add(new Player
                {
                    Id = _nextPlayerId++,
                    Name = $"{name} Spieler {i + 1}",
                    Age = 25,
                    Position = LineupPositions[i],
                    Rating = baseRating,
                    Fitness = fitness,
                    OffensivePower = baseRating,
                    DefensivePower = baseRating,
                    GameIntelligence = baseRating,
                    PressingIntensity = baseRating,
                    CounterSpeed = baseRating,
                    PassingAccuracy = baseRating,
                    DuelHardness = baseRating,
                    DuelEfficiency = baseRating,
                    CrossingAccuracy = baseRating,
                    GkReflexes = baseRating,
                    GkHandling = baseRating,
                    GkOneOnOne = baseRating,
                    GkDistribution = baseRating,
                    GkAerialControl = baseRating,
                    HeaderStrength = baseRating,
                    Jumping = baseRating,
                    Dribbling = baseRating,
                    LongShotAccuracy = baseRating,
                    PenaltyKick = baseRating,
                    FreeKick = baseRating,
                    Finishing = baseRating,
                    Positioning = baseRating,
                    Size = 1.83,
                    Personality = Personality.None,
                    Status = PlayerStatus.InStartingXI,
                });
            }

            return team;
        }
    }
}
