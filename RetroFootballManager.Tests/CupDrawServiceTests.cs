using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CupDrawServiceTests
    {
        private static List<Team> Create72Teams()
        {
            var teams = new List<Team>();
            int id = 1;
            for (int tier = 1; tier <= 4; tier++)
                for (int i = 0; i < 18; i++)
                    teams.Add(new Team { Id = id++, Name = $"Team{tier}-{i}", LeagueTier = tier });
            return teams;
        }

        // Heim gewinnt immer (kein Elfmeterschießen nötig) - deterministisch für die Simulation.
        private static void PlayRound(List<CupTie> ties)
        {
            foreach (var tie in ties)
            {
                tie.HomeGoals = 1;
                tie.AwayGoals = 0;
                tie.Played = true;
            }
        }

        private static readonly DateTime Date = new(2026, 9, 1);

        [Fact]
        public void BuildGermanCupFirstRound_With72Teams_ProducesThirtySixMatches_NoByes()
        {
            var teams = Create72Teams();
            var rng = new Random(1);

            var round1 = CupDrawService.BuildGermanCupFirstRound(teams, season: 1, Date, rng);

            Assert.Equal(36, round1.Count);
            Assert.DoesNotContain(round1, t => t.IsBye);
            var participantIds = round1.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).ToHashSet();
            Assert.Equal(72, participantIds.Count);
        }

        [Fact]
        public void BuildNextRound_OddWinnerCount_GivesExactlyOneRandomTeamABye_MarkedIsBye()
        {
            var teams = Enumerable.Range(1, 6).Select(id => new Team { Id = id, LeagueTier = 2 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);
            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = 5, MatchNumberInRound = 1,
                        HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = 5, MatchNumberInRound = 2,
                        HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 0, AwayGoals = 1, Played = true },
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = 5, MatchNumberInRound = 3,
                        HomeTeamId = 5, AwayTeamId = 6, HomeGoals = 1, AwayGoals = 0, Played = true },
            };
            // Three winners (1, 4, 5) enter the next round - an odd count needs exactly one bye.

            var next = CupDrawService.BuildNextRound(previousRound, teamsById, season: 1, Date, new Random(1));

            var byeTies = next.Where(t => t.IsBye).ToList();
            var realTies = next.Where(t => !t.IsBye).ToList();
            Assert.Single(byeTies);
            Assert.Single(realTies);
            Assert.True(byeTies[0].Played);
            Assert.Contains(byeTies[0].WinnerTeamId, new[] { 1, 4, 5 });
        }

        [Fact]
        public void FullBracket_72Teams_Produces71RealTiesAndThreeByes()
        {
            var teams = Create72Teams();
            var teamsById = teams.ToDictionary(t => t.Id);
            var rng = new Random(1);

            var round1 = CupDrawService.BuildGermanCupFirstRound(teams, season: 1, Date, rng);
            PlayRound(round1);

            int totalReal = round1.Count(t => !t.IsBye);
            int totalByes = round1.Count(t => t.IsBye);
            var current = round1;
            while (current.Count(t => !t.IsBye) + current.Count(t => t.IsBye) > 1
                   && current.Select(t => t.WinnerTeamId).Distinct().Count() > 1)
            {
                current = CupDrawService.BuildNextRound(current, teamsById, season: 1, Date, rng);
                PlayRound(current.Where(t => !t.IsBye).ToList());
                totalReal += current.Count(t => !t.IsBye);
                totalByes += current.Count(t => t.IsBye);
            }

            Assert.Single(current);
            Assert.Equal(CupDrawService.RoundFinal, current[0].Round);
            Assert.Equal(71, totalReal); // N teams always need N-1 real matches for one champion
            Assert.Equal(3, totalByes); // odd winner-counts at 9 -> 5 -> 3 -> 2
        }

        [Fact]
        public void PreliminaryRound_DifferentTiers_LowerLeagueTeamGetsHomeAdvantage()
        {
            var tier1Team = new Team { Id = 1, Name = "Tier1", LeagueTier = 1 };
            var tier4Team = new Team { Id = 2, Name = "Tier4", LeagueTier = 4 };
            var teams = new List<Team> { tier1Team, tier4Team };
            teams.AddRange(Enumerable.Range(3, 70).Select(i => new Team { Id = i, LeagueTier = 2 }));

            for (int trial = 0; trial < 200; trial++)
            {
                var rng = new Random(trial);
                var ties = CupDrawService.BuildGermanCupFirstRound(teams, season: 1, Date, rng);
                var tie = ties.FirstOrDefault(t =>
                    (t.HomeTeamId == 1 && t.AwayTeamId == 2) || (t.HomeTeamId == 2 && t.AwayTeamId == 1));
                if (tie is null)
                    continue;

                Assert.Equal(2, tie.HomeTeamId); // Tier4-Team (schwächere Liga) hat Heimrecht
                return;
            }
        }

        [Fact]
        public void FromRoundOfSixteen_HomeAdvantageIsNotTierDependent()
        {
            var strong = new Team { Id = 1, LeagueTier = 1 };
            var weak = new Team { Id = 2, LeagueTier = 4 };
            var teamsById = new Dictionary<int, Team> { [1] = strong, [2] = weak };

            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1,
                        Round = CupDrawService.RoundLastSixteen, MatchNumberInRound = 1,
                        HomeTeamId = 1, AwayTeamId = 1, HomeGoals = 1, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1,
                        Round = CupDrawService.RoundLastSixteen, MatchNumberInRound = 2,
                        HomeTeamId = 2, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0, Played = true },
            };

            bool strongWasHomeAtLeastOnce = false;
            for (int trial = 0; trial < 20; trial++)
            {
                var next = CupDrawService.BuildNextRound(previousRound, teamsById, season: 1, Date, new Random(trial));
                Assert.Single(next);
                if (next[0].HomeTeamId == strong.Id)
                    strongWasHomeAtLeastOnce = true;
            }

            // Zufällig, nicht liga-abhängig: über 20 Versuche muss das stärkere Team mindestens
            // einmal auch auswärts (bzw. das schwächere Team mindestens einmal daheim) auftreten.
            Assert.True(strongWasHomeAtLeastOnce);
        }

        [Fact]
        public void BuildGermanCupFirstRound_NeverIncludesForeignClubs()
        {
            // Regression: erfundene CL/Europa-Cup-Vereine (LeagueTier 0) dürfen nie im Deutschen
            // Pokal auftauchen, auch wenn der Aufrufer versehentlich die komplette Team-Liste
            // inkl. Auslandsvereinen übergibt statt nur der deutschen Liga-Teams.
            var teams = Create72Teams();
            var foreignClub = new Team { Id = 9999, Name = "Andalucía Sporting", LeagueTier = 0 };
            var allTeamsIncludingForeign = teams.Concat([foreignClub]).ToList();

            var round1 = CupDrawService.BuildGermanCupFirstRound(allTeamsIncludingForeign, season: 1, Date, new Random(1));

            var participantIds = round1.SelectMany(t => new[] { t.HomeTeamId, t.AwayTeamId }).ToHashSet();
            Assert.DoesNotContain(foreignClub.Id, participantIds);
        }

        [Fact]
        public void BuildNextRound_PairsWinnersArithmetically()
        {
            var teams = Enumerable.Range(1, 4).Select(id => new Team { Id = id, LeagueTier = 2 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);

            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = 5, MatchNumberInRound = 1,
                        HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = 5, MatchNumberInRound = 2,
                        HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 0, AwayGoals = 1, Played = true },
            };

            var next = CupDrawService.BuildNextRound(previousRound, teamsById, season: 1, Date, new Random(1));

            Assert.Single(next);
            Assert.Equal(6, next[0].Round);
            var participantIds = new[] { next[0].HomeTeamId, next[0].AwayTeamId };
            Assert.Contains(1, participantIds); // Sieger Partie 1
            Assert.Contains(4, participantIds); // Sieger Partie 2
        }

        private static readonly DateTime SecondLegDate = new(2026, 9, 8);

        [Fact]
        public void BuildNextRound_ChampionsLeagueLastSixteen_ProducesTwoLegsWithSwappedHomeAway()
        {
            var teams = Enumerable.Range(1, 4).Select(id => new Team { Id = id, LeagueTier = 1 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);

            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundLastThirtyTwo, MatchNumberInRound = 1,
                        HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundLastThirtyTwo, MatchNumberInRound = 2,
                        HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 0, AwayGoals = 1, Played = true },
            };

            var next = CupDrawService.BuildNextRound(
                previousRound, teamsById, season: 1, Date, secondLegDate: SecondLegDate);

            Assert.Equal(2, next.Count);
            var leg1 = next.Single(t => t.LegNumber == CupTie.LegFirst);
            var leg2 = next.Single(t => t.LegNumber == CupTie.LegSecond);
            Assert.Equal(1, leg1.HomeTeamId);
            Assert.Equal(4, leg1.AwayTeamId);
            Assert.Equal(4, leg2.HomeTeamId);
            Assert.Equal(1, leg2.AwayTeamId);
            Assert.Equal(Date, leg1.Date);
            Assert.Equal(SecondLegDate, leg2.Date);
        }

        [Fact]
        public void BuildNextRound_ChampionsLeagueSemiFinal_ToFinal_ProducesSingleNeutralMatch()
        {
            var teams = Enumerable.Range(1, 4).Select(id => new Team { Id = id, LeagueTier = 1 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);

            // Zwei Halbfinal-Paarungen (je Hin-/Rückspiel) -> 2 Sieger -> 1 Finale.
            var semiFinalLegs = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundSemiFinal, MatchNumberInRound = 1, LegNumber = CupTie.LegFirst,
                        HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundSemiFinal, MatchNumberInRound = 1, LegNumber = CupTie.LegSecond,
                        HomeTeamId = 2, AwayTeamId = 1, HomeGoals = 0, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundSemiFinal, MatchNumberInRound = 2, LegNumber = CupTie.LegFirst,
                        HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 1, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundSemiFinal, MatchNumberInRound = 2, LegNumber = CupTie.LegSecond,
                        HomeTeamId = 4, AwayTeamId = 3, HomeGoals = 0, AwayGoals = 0, Played = true },
            };

            var final = CupDrawService.BuildNextRound(semiFinalLegs, teamsById, season: 1, Date, new Random(1));

            Assert.Single(final);
            Assert.Equal(CupTie.LegNone, final[0].LegNumber);
            Assert.Equal(CupDrawService.RoundFinal, final[0].Round);
        }

        [Fact]
        public void BuildNextRound_TwoLeggedRound_ThrowsWhenSecondLegDateMissing()
        {
            var teams = Enumerable.Range(1, 4).Select(id => new Team { Id = id, LeagueTier = 1 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);
            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundLastThirtyTwo, MatchNumberInRound = 1,
                        HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.ChampionsLeague, Season = 1,
                        Round = CupDrawService.RoundLastThirtyTwo, MatchNumberInRound = 2,
                        HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 1, AwayGoals = 0, Played = true },
            };

            Assert.Throws<ArgumentException>(() =>
                CupDrawService.BuildNextRound(previousRound, teamsById, season: 1, Date));
        }

        [Fact]
        public void BuildNextRound_GermanCup_NeverProducesLegs_RegressionOfExistingBehavior()
        {
            var teams = Enumerable.Range(1, 4).Select(id => new Team { Id = id, LeagueTier = 2 }).ToList();
            var teamsById = teams.ToDictionary(t => t.Id);
            var previousRound = new List<CupTie>
            {
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = CupDrawService.RoundLastSixteen,
                        MatchNumberInRound = 1, HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 0, Played = true },
                new() { CompetitionType = CompetitionType.GermanCup, Season = 1, Round = CupDrawService.RoundLastSixteen,
                        MatchNumberInRound = 2, HomeTeamId = 3, AwayTeamId = 4, HomeGoals = 0, AwayGoals = 1, Played = true },
            };

            var next = CupDrawService.BuildNextRound(previousRound, teamsById, season: 1, Date, new Random(1));

            Assert.Single(next);
            Assert.Equal(CupTie.LegNone, next[0].LegNumber);
        }
    }
}
