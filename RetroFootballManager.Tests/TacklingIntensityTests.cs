using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TacklingIntensityTests
    {
        [Fact]
        public void Simulate_HardTacklingTeam_ConcedesMoreCardsAndPenaltiesThanCautious()
        {
            var random = new Random(500);
            int cautiousFouls = 0, cautiousCards = 0, cautiousPenaltiesConceded = 0;
            int hardFouls = 0, hardCards = 0, hardPenaltiesConceded = 0;
            const int matches = 80;

            for (int i = 0; i < matches; i++)
            {
                var cautious = TestHelpers.CreateTeam("Vorsichtig", baseRating: 65);
                cautious.TacklingIntensity = TacklingIntensity.Cautious;
                var opponentA = TestHelpers.CreateTeam("GegnerA", baseRating: 65);
                var resultA = new Match(opponentA, cautious, random).Simulate();

                cautiousFouls += resultA.MatchStatsAway.Fouls;
                cautiousCards += resultA.MatchStatsAway.YellowCards + resultA.MatchStatsAway.RedCards;
                cautiousPenaltiesConceded += resultA.MatchStatsHome.Penaltys;

                var hard = TestHelpers.CreateTeam("Hart", baseRating: 65);
                hard.TacklingIntensity = TacklingIntensity.Hard;
                var opponentB = TestHelpers.CreateTeam("GegnerB", baseRating: 65);
                var resultB = new Match(opponentB, hard, random).Simulate();

                hardFouls += resultB.MatchStatsAway.Fouls;
                hardCards += resultB.MatchStatsAway.YellowCards + resultB.MatchStatsAway.RedCards;
                hardPenaltiesConceded += resultB.MatchStatsHome.Penaltys;
            }

            Assert.True(hardFouls > cautiousFouls);
            Assert.True(hardCards > cautiousCards);
            Assert.True(hardPenaltiesConceded > cautiousPenaltiesConceded);
        }

        [Fact]
        public void PlayerOverride_TakesPrecedenceOverTeamSetting()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            team.TacklingIntensity = TacklingIntensity.Hard;
            var player = team.Players[0];

            Assert.Equal(TacklingIntensity.Hard, TacklingIntensityEffects.GetEffective(player, team));

            // Live-Anweisung während des Spiels: dieser eine Spieler soll jetzt vorsichtig spielen.
            player.TacklingIntensity = TacklingIntensity.Cautious;

            Assert.Equal(TacklingIntensity.Cautious, TacklingIntensityEffects.GetEffective(player, team));
            Assert.All(team.Players.Skip(1), p => Assert.Equal(TacklingIntensity.Hard, TacklingIntensityEffects.GetEffective(p, team)));
        }

        [Fact]
        public void TeamStrengthCalculator_HardTackling_SlightlyBoostsDefense()
        {
            var cautious = TestHelpers.CreateTeam("Vorsichtig", baseRating: 70);
            cautious.TacklingIntensity = TacklingIntensity.Cautious;

            var hard = TestHelpers.CreateTeam("Hart", baseRating: 70);
            hard.TacklingIntensity = TacklingIntensity.Hard;

            var cautiousProfile = TeamStrengthCalculator.Calculate(cautious, isHome: false);
            var hardProfile = TeamStrengthCalculator.Calculate(hard, isHome: false);

            Assert.True(hardProfile.Defense > cautiousProfile.Defense);
            Assert.True(hardProfile.DisciplineRisk > cautiousProfile.DisciplineRisk);
        }

        [Fact]
        public void HardTackling_SkilledPlayer_GetsDuelBonus_UnskilledPlayer_GetsMalus()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            team.TacklingIntensity = TacklingIntensity.Hard;

            var skilled = team.Players[0];
            skilled.DuelEfficiency = 90;

            var clumsy = team.Players[1];
            clumsy.DuelEfficiency = 20;

            double skilledMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(skilled, team);
            double clumsyMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(clumsy, team);

            Assert.True(skilledMultiplier > 1.0, "Guter Zweikämpfer soll durch hartes Tackling einen Bonus bekommen.");
            Assert.True(clumsyMultiplier < 1.0, "Schlechter Zweikämpfer soll durch hartes Tackling einen Malus bekommen.");
            Assert.True(skilledMultiplier > clumsyMultiplier);
        }

        [Fact]
        public void HardTackling_UnskilledPlayer_HasHigherCardRiskThanSkilledPlayer()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            team.TacklingIntensity = TacklingIntensity.Hard;

            var skilled = team.Players[0];
            skilled.DuelEfficiency = 90;

            var clumsy = team.Players[1];
            clumsy.DuelEfficiency = 20;

            double skilledRisk = TacklingIntensityEffects.GetFoulCardRiskMultiplier(skilled, team);
            double clumsyRisk = TacklingIntensityEffects.GetFoulCardRiskMultiplier(clumsy, team);

            Assert.True(clumsyRisk > skilledRisk, "Unbeholfener Zweikämpfer soll bei hartem Tackling ein deutlich höheres Kartenrisiko haben.");
        }

        [Fact]
        public void NormalIntensity_IsAlwaysSkillIndependent()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            team.TacklingIntensity = TacklingIntensity.Normal;

            var skilled = team.Players[0];
            skilled.DuelEfficiency = 95;

            var clumsy = team.Players[1];
            clumsy.DuelEfficiency = 10;

            Assert.Equal(1.0, TacklingIntensityEffects.GetDuelEffectivenessMultiplier(skilled, team));
            Assert.Equal(1.0, TacklingIntensityEffects.GetDuelEffectivenessMultiplier(clumsy, team));
            Assert.Equal(1.0, TacklingIntensityEffects.GetFoulCardRiskMultiplier(skilled, team));
            Assert.Equal(1.0, TacklingIntensityEffects.GetFoulCardRiskMultiplier(clumsy, team));
        }

        [Fact]
        public void CautiousTackling_IsAlwaysAMalus_NeverABonus()
        {
            var team = TestHelpers.CreateTeam("Team", baseRating: 65);
            team.TacklingIntensity = TacklingIntensity.Cautious;

            var skilled = team.Players[0];
            skilled.DuelEfficiency = 95;

            var clumsy = team.Players[1];
            clumsy.DuelEfficiency = 10;

            double skilledMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(skilled, team);
            double clumsyMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(clumsy, team);

            // Vorsichtig ist immer ein Malus, auch für einen sehr guten Zweikämpfer.
            Assert.True(skilledMultiplier < 1.0);
            Assert.True(clumsyMultiplier < 1.0);

            // Guter Wert mildert den Nachteil deutlich ab gegenüber einem schlechten Wert.
            Assert.True(skilledMultiplier > clumsyMultiplier);
        }

        [Fact]
        public void CautiousTackling_EvenWithGreatSkill_StaysBelowHardWithSameSkill()
        {
            var cautiousTeam = TestHelpers.CreateTeam("Vorsichtig", baseRating: 65);
            cautiousTeam.TacklingIntensity = TacklingIntensity.Cautious;
            var cautiousPlayer = cautiousTeam.Players[0];
            cautiousPlayer.DuelEfficiency = 95;

            var hardTeam = TestHelpers.CreateTeam("Hart", baseRating: 65);
            hardTeam.TacklingIntensity = TacklingIntensity.Hard;
            var hardPlayer = hardTeam.Players[0];
            hardPlayer.DuelEfficiency = 95;

            double cautiousMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(cautiousPlayer, cautiousTeam);
            double hardMultiplier = TacklingIntensityEffects.GetDuelEffectivenessMultiplier(hardPlayer, hardTeam);

            Assert.True(cautiousMultiplier < hardMultiplier,
                "Selbst ein sehr guter Zweikämpfer soll vorsichtig spielend weniger Bälle gewinnen als hart spielend.");
        }
    }
}
