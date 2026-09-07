using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TeamAssessmentEstimatorTests
    {
        private static Finances CreateFinances() => new()
        {
            CurrentBalance = 1_000_000,
            TransferBudget = 200_000,
            FinancialHealth = 70,
        };

        [Fact]
        public void Estimate_NoAnalyst_EverythingIsNull()
        {
            var estimate = TeamAssessmentEstimator.Estimate(70, 80, CreateFinances(), teamAssessmentAbility: null, new Random(1));

            Assert.Null(estimate.Rating);
            Assert.Null(estimate.Morale);
            Assert.Null(estimate.Balance);
            Assert.Null(estimate.TransferBudget);
            Assert.Null(estimate.FinancialHealth);
            Assert.False(estimate.IsExact);
            Assert.Equal("Unbekannt", estimate.AccuracyLabel);
        }

        [Fact]
        public void Estimate_TopAnalyst_ReturnsExactValues()
        {
            var finances = CreateFinances();

            var estimate = TeamAssessmentEstimator.Estimate(70, 80, finances, teamAssessmentAbility: 90, new Random(1));

            Assert.Equal(70, estimate.Rating);
            Assert.Equal(80, estimate.Morale);
            Assert.Equal(finances.CurrentBalance, estimate.Balance);
            Assert.Equal(finances.TransferBudget, estimate.TransferBudget);
            Assert.Equal(finances.FinancialHealth, estimate.FinancialHealth);
            Assert.True(estimate.IsExact);
        }

        [Fact]
        public void Estimate_HighAbility_StaysCloseToRealValues()
        {
            var finances = CreateFinances();

            var estimate = TeamAssessmentEstimator.Estimate(70, 80, finances, teamAssessmentAbility: 85, new Random(2));

            Assert.NotNull(estimate.Balance);
            double deviation = Math.Abs(estimate.Balance!.Value - finances.CurrentBalance) / finances.CurrentBalance;
            Assert.True(deviation < 0.10, $"deviation={deviation}");
            Assert.False(estimate.IsExact);
        }

        [Fact]
        public void Estimate_LowAbility_CanDeviateWidely()
        {
            var finances = CreateFinances();
            double maxDeviation = 0;

            for (int seed = 0; seed < 30; seed++)
            {
                var estimate = TeamAssessmentEstimator.Estimate(70, 80, finances, teamAssessmentAbility: 5, new Random(seed));
                double deviation = Math.Abs(estimate.Balance!.Value - finances.CurrentBalance) / finances.CurrentBalance;
                maxDeviation = Math.Max(maxDeviation, deviation);
            }

            Assert.True(maxDeviation > 0.10, $"maxDeviation={maxDeviation}");
        }

        [Fact]
        public void Estimate_MoraleAndRating_StayWithinClampedRealisticBounds()
        {
            var finances = CreateFinances();

            for (int seed = 0; seed < 30; seed++)
            {
                var estimate = TeamAssessmentEstimator.Estimate(70, 80, finances, teamAssessmentAbility: 5, new Random(seed));
                Assert.InRange(estimate.Morale!.Value, 0, 100);
                Assert.InRange(estimate.FinancialHealth!.Value, 0, 100);
            }
        }

        [Fact]
        public void Estimate_SameSeed_IsDeterministic()
        {
            var finances = CreateFinances();

            var estimateA = TeamAssessmentEstimator.Estimate(70, 80, finances, 40, new Random(HashCode.Combine(1, 2026, 8)));
            var estimateB = TeamAssessmentEstimator.Estimate(70, 80, finances, 40, new Random(HashCode.Combine(1, 2026, 8)));

            Assert.Equal(estimateA.Balance, estimateB.Balance);
            Assert.Equal(estimateA.Rating, estimateB.Rating);
            Assert.Equal(estimateA.Morale, estimateB.Morale);
        }

        [Fact]
        public void Estimate_DifferentSeed_CanProduceDifferentValues()
        {
            var finances = CreateFinances();

            var estimateA = TeamAssessmentEstimator.Estimate(70, 80, finances, 40, new Random(HashCode.Combine(1, 2026, 8)));
            var estimateB = TeamAssessmentEstimator.Estimate(70, 80, finances, 40, new Random(HashCode.Combine(1, 2026, 9)));

            Assert.NotEqual(estimateA.Balance, estimateB.Balance);
        }
    }
}
