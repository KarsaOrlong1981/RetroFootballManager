using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class EmployeeStackingTests
    {
        // --- StaffMarketService.CanHire / MaxEmployeesPerType ---

        [Theory]
        [InlineData(4, 3)]
        [InlineData(3, 4)]
        [InlineData(2, 5)]
        [InlineData(1, 6)]
        public void MaxEmployeesPerType_ScalesWithLeagueTier_ForNonDirectorTypes(int leagueTier, int expectedMax)
        {
            int max = StaffMarketService.MaxEmployeesPerType(leagueTier, EmployeeType.Scout);
            Assert.Equal(expectedMax, max);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(3)]
        [InlineData(2)]
        [InlineData(1)]
        public void MaxEmployeesPerType_DirectorOfFootball_AlwaysOne(int leagueTier)
        {
            int max = StaffMarketService.MaxEmployeesPerType(leagueTier, EmployeeType.DirectorOfFootball);
            Assert.Equal(1, max);
        }

        [Fact]
        public void CanHire_RejectsWhenAtCap()
        {
            var team = TestHelpers.CreateTeam("Voll", baseRating: 60);
            team.LeagueTier = 4;
            for (int i = 0; i < 3; i++)
                team.Employees.Add(new Employee { EmployeeType = EmployeeType.Scout, ScoutingAbility = 50 });

            bool allowed = StaffMarketService.CanHire(team, EmployeeType.Scout, out string? error);

            Assert.False(allowed);
            Assert.NotNull(error);
        }

        [Fact]
        public void CanHire_AllowsWhenBelowCap()
        {
            var team = TestHelpers.CreateTeam("Platz frei", baseRating: 60);
            team.LeagueTier = 1; // max 6

            for (int i = 0; i < 5; i++)
                team.Employees.Add(new Employee { EmployeeType = EmployeeType.Scout, ScoutingAbility = 50 });

            bool allowed = StaffMarketService.CanHire(team, EmployeeType.Scout, out string? error);

            Assert.True(allowed);
            Assert.Null(error);
        }

        [Fact]
        public void CanHire_DirectorOfFootball_RejectsSecondHire()
        {
            var team = TestHelpers.CreateTeam("DoF", baseRating: 60);
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.DirectorOfFootball, Rating = 70 });

            bool allowed = StaffMarketService.CanHire(team, EmployeeType.DirectorOfFootball, out string? error);

            Assert.False(allowed);
            Assert.NotNull(error);
        }

        // --- Match.ApplyMedicalStaffReduction (Physio/MedicalStaff stacking + overload) ---

        [Fact]
        public void ApplyMedicalStaffReduction_NoStaff_ReturnsUnchangedDuration()
        {
            var team = TestHelpers.CreateTeam("Ohne Physio", baseRating: 60);
            int result = Match.ApplyMedicalStaffReduction(20, team);
            Assert.Equal(20, result);
        }

        [Fact]
        public void ApplyMedicalStaffReduction_ManyGoodStaff_FewInjured_ReducesDuration()
        {
            var team = TestHelpers.CreateTeam("Viel Personal, wenig verletzt", baseRating: 60);
            for (int i = 0; i < 3; i++)
                team.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 90 });
            team.Players[0].Status = PlayerStatus.Injured; // only 1 injured

            int result = Match.ApplyMedicalStaffReduction(20, team);

            Assert.True(result < 20, $"result={result}");
        }

        [Fact]
        public void ApplyMedicalStaffReduction_FewStaff_ManyInjured_CanLengthenDuration()
        {
            var team = TestHelpers.CreateTeam("Wenig Personal, viel verletzt", baseRating: 60);
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 50 });
            foreach (var p in team.Players.Take(5))
                p.Status = PlayerStatus.Injured; // overloaded relative to 1 staff member

            int result = Match.ApplyMedicalStaffReduction(20, team);

            Assert.True(result > 20, $"result={result}");
        }

        [Fact]
        public void ApplyMedicalStaffReduction_CombinesPhysiotherapistAndMedicalStaff()
        {
            var team = TestHelpers.CreateTeam("Gemischt", baseRating: 60);
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 90 });
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.MedicalStaff, FitnessTraining = 90 });
            team.Players[0].Status = PlayerStatus.Injured;

            int resultCombined = Match.ApplyMedicalStaffReduction(20, team);

            var soloTeam = TestHelpers.CreateTeam("Nur einer", baseRating: 60);
            soloTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 90 });
            soloTeam.Players[0].Status = PlayerStatus.Injured;
            int resultSolo = Match.ApplyMedicalStaffReduction(20, soloTeam);

            Assert.True(resultCombined <= resultSolo, $"combined={resultCombined}, solo={resultSolo}");
        }

        // --- MatchDayService morale boosts (Physio/Psychologist stacking) ---

        [Fact]
        public void ApplyPhysioMoraleBoost_StacksAcrossMultipleStaff()
        {
            var team = TestHelpers.CreateTeam("Physios", baseRating: 60);
            team.Statistics = new TeamStats();
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 90 });
            MatchDayService.ApplyPhysioMoraleBoost(team);
            int oneStaffBoost = team.Statistics.PhysioMoraleBoost;

            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Physiotherapist, FitnessTraining = 90 });
            MatchDayService.ApplyPhysioMoraleBoost(team);
            int twoStaffBoost = team.Statistics.PhysioMoraleBoost;

            Assert.True(twoStaffBoost > oneStaffBoost, $"one={oneStaffBoost}, two={twoStaffBoost}");
        }

        [Fact]
        public void ApplyPsychologistMoraleBoost_StacksAcrossMultipleStaff()
        {
            var team = TestHelpers.CreateTeam("Psychologen", baseRating: 60);
            team.Statistics = new TeamStats();
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Psychologist, Motivation = 90 });
            MatchDayService.ApplyPsychologistMoraleBoost(team);
            int oneStaffBoost = team.Statistics.PsychologistMoraleBoost;

            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Psychologist, Motivation = 90 });
            MatchDayService.ApplyPsychologistMoraleBoost(team);
            int twoStaffBoost = team.Statistics.PsychologistMoraleBoost;

            Assert.True(twoStaffBoost > oneStaffBoost, $"one={oneStaffBoost}, two={twoStaffBoost}");
        }

        [Fact]
        public void ApplyPsychologistMoraleBoost_ResetsToZero_WhenNoLongerEmployed()
        {
            var team = TestHelpers.CreateTeam("Ex-Psychologe", baseRating: 60);
            team.Statistics = new TeamStats();
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.Psychologist, Motivation = 90 });
            MatchDayService.ApplyPsychologistMoraleBoost(team);
            Assert.True(team.Statistics.PsychologistMoraleBoost > 0);

            team.Employees.Clear();
            MatchDayService.ApplyPsychologistMoraleBoost(team);

            Assert.Equal(0, team.Statistics.PsychologistMoraleBoost);
        }

        // --- DevelopmentService (YouthCoach stacking) ---

        [Fact]
        public void ApplyMonthlyDevelopment_MultipleYouthCoaches_GrowYouthFasterThanOne()
        {
            // RecalculateRating derives Rating purely from the (initially all-zero) trainable
            // attributes, so the post-development Rating itself is a direct, robust proxy for
            // how many growth points were actually distributed this month - averaged over many
            // seeds to smooth out RollPoints' randomness.
            const int trials = 80;
            double totalRatingOneCoach = 0;
            double totalRatingTwoCoaches = 0;

            for (int seed = 0; seed < trials; seed++)
            {
                var oneCoachTeam = TestHelpers.CreateTeam("Ein Coach", baseRating: 60);
                oneCoachTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.YouthCoach, YouthDevelopment = 90 });
                var youth1 = new Player { Id = 500 + seed, Age = 17, Talent = 50, Position = Position.Forward, IsYouthProspect = true };
                oneCoachTeam.YouthPlayers.Add(youth1);
                DevelopmentService.ApplyMonthlyDevelopment(oneCoachTeam, new DateTime(2026, 1, 1), new Random(seed));
                totalRatingOneCoach += youth1.Rating;

                var twoCoachTeam = TestHelpers.CreateTeam("Zwei Coaches", baseRating: 60);
                twoCoachTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.YouthCoach, YouthDevelopment = 90 });
                twoCoachTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.YouthCoach, YouthDevelopment = 90 });
                var youth2 = new Player { Id = 600 + seed, Age = 17, Talent = 50, Position = Position.Forward, IsYouthProspect = true };
                twoCoachTeam.YouthPlayers.Add(youth2);
                DevelopmentService.ApplyMonthlyDevelopment(twoCoachTeam, new DateTime(2026, 1, 1), new Random(seed));
                totalRatingTwoCoaches += youth2.Rating;
            }

            Assert.True(totalRatingTwoCoaches > totalRatingOneCoach,
                $"oneCoach={totalRatingOneCoach}, twoCoaches={totalRatingTwoCoaches}");
        }

        // --- TransferAiService.DirectorOfFootballPriceFactor ---

        [Fact]
        public void DirectorOfFootballPriceFactor_NoDirector_ReturnsNeutral()
        {
            var team = TestHelpers.CreateTeam("Ohne DoF", baseRating: 60);
            Assert.Equal(1.0, TransferAiService.DirectorOfFootballPriceFactor(team, favorSeller: true));
            Assert.Equal(1.0, TransferAiService.DirectorOfFootballPriceFactor(team, favorSeller: false));
        }

        [Fact]
        public void DirectorOfFootballPriceFactor_FavorsSeller_AboveOne_AndBuyer_BelowOne()
        {
            var team = TestHelpers.CreateTeam("Mit DoF", baseRating: 60);
            team.Employees.Add(new Employee { EmployeeType = EmployeeType.DirectorOfFootball, Rating = 80 });

            double sellFactor = TransferAiService.DirectorOfFootballPriceFactor(team, favorSeller: true);
            double buyFactor = TransferAiService.DirectorOfFootballPriceFactor(team, favorSeller: false);

            Assert.True(sellFactor > 1.0, $"sellFactor={sellFactor}");
            Assert.True(buyFactor < 1.0, $"buyFactor={buyFactor}");
        }
    }
}
