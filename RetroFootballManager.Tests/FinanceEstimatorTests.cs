using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FinanceEstimatorTests
    {
        private static Finances CreateFinances() => new()
        {
            CurrentBalance = 1_000_000,
            TransferBudget = 200_000,
            FinancialHealth = 70,
        };

        [Fact]
        public void Estimate_NoAnalyst_OnlyFinancialHealthIsVisible()
        {
            var finances = CreateFinances();

            var estimate = FinanceEstimator.Estimate(finances, analysisAbility: null, new Random(1));

            Assert.Null(estimate.EstimatedBalance);
            Assert.Null(estimate.EstimatedTransferBudget);
            Assert.Equal(70, estimate.FinancialHealth);
            Assert.False(estimate.IsExact);
        }

        [Fact]
        public void Estimate_TopAnalyst_ReturnsExactValues()
        {
            var finances = CreateFinances();

            var estimate = FinanceEstimator.Estimate(finances, analysisAbility: 90, new Random(1));

            Assert.Equal(finances.CurrentBalance, estimate.EstimatedBalance);
            Assert.Equal(finances.TransferBudget, estimate.EstimatedTransferBudget);
            Assert.True(estimate.IsExact);
        }

        [Fact]
        public void Estimate_HighAnalysisAbility_StaysCloseToRealValue()
        {
            var finances = CreateFinances();

            var estimate = FinanceEstimator.Estimate(finances, analysisAbility: 85, new Random(2));

            Assert.NotNull(estimate.EstimatedBalance);
            double deviation = Math.Abs(estimate.EstimatedBalance!.Value - finances.CurrentBalance) / finances.CurrentBalance;
            Assert.True(deviation < 0.10, $"deviation={deviation}");
            Assert.False(estimate.IsExact);
        }

        [Fact]
        public void Estimate_LowAnalysisAbility_CanDeviateWidely()
        {
            var finances = CreateFinances();
            double maxDeviation = 0;

            for (int seed = 0; seed < 30; seed++)
            {
                var estimate = FinanceEstimator.Estimate(finances, analysisAbility: 5, new Random(seed));
                double deviation = Math.Abs(estimate.EstimatedBalance!.Value - finances.CurrentBalance) / finances.CurrentBalance;
                maxDeviation = Math.Max(maxDeviation, deviation);
            }

            Assert.True(maxDeviation > 0.10, $"maxDeviation={maxDeviation}");
        }

        [Fact]
        public void Estimate_SameSeed_IsDeterministic()
        {
            var finances = CreateFinances();

            var estimateA = FinanceEstimator.Estimate(finances, analysisAbility: 40, new Random(HashCode.Combine(1, 2026, 8)));
            var estimateB = FinanceEstimator.Estimate(finances, analysisAbility: 40, new Random(HashCode.Combine(1, 2026, 8)));

            Assert.Equal(estimateA.EstimatedBalance, estimateB.EstimatedBalance);
            Assert.Equal(estimateA.EstimatedTransferBudget, estimateB.EstimatedTransferBudget);
        }

        [Fact]
        public void Estimate_DifferentSeed_CanProduceDifferentValues()
        {
            var finances = CreateFinances();

            var estimateA = FinanceEstimator.Estimate(finances, analysisAbility: 40, new Random(HashCode.Combine(1, 2026, 8)));
            var estimateB = FinanceEstimator.Estimate(finances, analysisAbility: 40, new Random(HashCode.Combine(1, 2026, 9)));

            Assert.NotEqual(estimateA.EstimatedBalance, estimateB.EstimatedBalance);
        }
    }
}
