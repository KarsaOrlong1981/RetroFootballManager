using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class GroupDrawServiceTests
    {
        private static List<Team> GermanQualifiers(int count, int startId = 1000)
        {
            var teams = new List<Team>();
            for (int i = 0; i < count; i++)
            {
                var t = TestHelpers.CreateTeam($"Qualifikant{i}", baseRating: 85);
                t.Id = startId + i;
                t.LeagueTier = 1;
                teams.Add(t);
            }
            return teams;
        }

        [Fact]
        public void BuildParticipants_ReplacesSmallPoolTeams_KeepsTotalAt32()
        {
            var foreignClubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(1));
            var qualifiers = GermanQualifiers(4);

            var participants = GroupDrawService.BuildParticipants(foreignClubs, qualifiers);

            Assert.Equal(32, participants.Count);
            Assert.True(qualifiers.All(q => participants.Contains(q)));
        }

        [Fact]
        public void DrawGroups_EachGroupHasExactlyOneTeamPerPot()
        {
            var foreignClubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(2));
            var qualifiers = GermanQualifiers(4);
            var participants = GroupDrawService.BuildParticipants(foreignClubs, qualifiers);

            var groups = GroupDrawService.DrawGroups(participants, new Random(2));

            Assert.Equal(8, groups.Count);
            // Identität statt Id vergleichen - erfundene Vereine sind vor der Persistierung
            // alle Id=0, HashSet<int> könnte sie also nicht unterscheiden.
            var sorted = participants.OrderByDescending(t => t.AverageRating).ToList();
            var pots = Enumerable.Range(0, 4).Select(p => sorted.Skip(p * 8).Take(8).ToHashSet()).ToList();

            foreach (var (_, teams) in groups)
            {
                Assert.Equal(4, teams.Count);
                for (int pot = 0; pot < 4; pot++)
                    Assert.Single(teams, t => pots[pot].Contains(t));
            }
        }

        [Fact]
        public void DrawGroups_ThrowsWhenNot32Participants()
        {
            var teams = GermanQualifiers(10);
            Assert.Throws<ArgumentException>(() => GroupDrawService.DrawGroups(teams));
        }

        [Fact]
        public void BuildGroupStageFixtures_TwelvePerGroup_NinetySixTotal()
        {
            var foreignClubs = ForeignClubGenerator.GenerateClubs(ForeignClubGenerator.Competition.ChampionsLeague, new Random(3));
            var qualifiers = GermanQualifiers(4);
            var participants = GroupDrawService.BuildParticipants(foreignClubs, qualifiers);
            var groups = GroupDrawService.DrawGroups(participants, new Random(3));

            var ties = GroupDrawService.BuildGroupStageFixtures(
                groups, CompetitionType.ChampionsLeague, season: 1, new DateTime(2026, 9, 1));

            Assert.Equal(96, ties.Count);
            foreach (var groupName in groups.Keys)
                Assert.Equal(12, ties.Count(t => t.Group == groupName));
        }

        [Fact]
        public void BuildRoundOfSixteen_NoTeamFacesOwnGroupOpponent()
        {
            var groupTables = new Dictionary<string, List<StandingRow>>();
            int id = 1;
            foreach (var name in new[] { "A", "B", "C", "D", "E", "F", "G", "H" })
            {
                groupTables[name] =
                [
                    new StandingRow(1, id++, "W", 6, 5, 0, 1, 10, 3, 7, 15, ""),
                    new StandingRow(2, id++, "R", 6, 3, 0, 3, 8, 8, 0, 9, ""),
                ];
            }

            var ties = GroupDrawService.BuildRoundOfSixteen(
                groupTables, CompetitionType.ChampionsLeague, season: 1,
                new DateTime(2026, 12, 1), new DateTime(2026, 12, 8), new Random(1));

            Assert.Equal(16, ties.Count); // 8 Paarungen x 2 (Hin-/Rückspiel)
            var runnerUpGroupOf = groupTables
                .SelectMany(kv => new[] { (kv.Value[1].TeamId, kv.Key) })
                .ToDictionary(x => x.Item1, x => x.Key);
            var winnerGroupOf = groupTables
                .SelectMany(kv => new[] { (kv.Value[0].TeamId, kv.Key) })
                .ToDictionary(x => x.Item1, x => x.Key);

            foreach (var tie in ties.Where(t => t.LegNumber == CupTie.LegFirst))
                Assert.NotEqual(winnerGroupOf[tie.HomeTeamId], runnerUpGroupOf[tie.AwayTeamId]);

            foreach (var pairing in ties.GroupBy(t => t.MatchNumberInRound))
            {
                var leg1 = pairing.Single(t => t.LegNumber == CupTie.LegFirst);
                var leg2 = pairing.Single(t => t.LegNumber == CupTie.LegSecond);
                Assert.Equal(leg1.HomeTeamId, leg2.AwayTeamId);
                Assert.Equal(leg1.AwayTeamId, leg2.HomeTeamId);
            }
        }

        [Fact]
        public void CalculateGroupTable_RanksByPoints_ThenGoalDifference_ThenGoalsFor()
        {
            var names = new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };
            var ties = new List<CupTie>
            {
                // Team 1: 1 Sieg (3 Pkt), Team 2: 1 Niederlage, Tordiff -2
                new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 0, Played = true },
                // Team 2 vs Team 3: Unentschieden (je 1 Pkt)
                new() { HomeTeamId = 2, AwayTeamId = 3, HomeGoals = 1, AwayGoals = 1, Played = true },
                // Team 3 vs Team 1: Team 3 gewinnt hoch -> gleiche Punkte wie Team1 (3), aber Tordiff entscheidet
                new() { HomeTeamId = 3, AwayTeamId = 1, HomeGoals = 3, AwayGoals = 0, Played = true },
            };

            var table = GroupDrawService.CalculateGroupTable(ties, names);

            // Team1: 3 Pkt, Tordiff 2-3=-1; Team3: 4 Pkt (Sieg+Unentschieden); Team2: 1 Pkt
            Assert.Equal(3, table[0].TeamId); // meiste Punkte (4)
            Assert.Equal(1, table[1].TeamId); // 3 Punkte
            Assert.Equal(2, table[2].TeamId); // 1 Punkt
        }
    }
}
