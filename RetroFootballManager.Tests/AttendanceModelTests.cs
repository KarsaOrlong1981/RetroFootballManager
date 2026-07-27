using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class AttendanceModelTests
    {
        private static Stadium CreateStadium(double seatPrice = 20, int comfort = 3, bool roof = false) => new()
        {
            SeatingCapacity = 30_000,
            StandingCapacity = 8_000,
            LogeCapacity = 500,
            SeatPrice = seatPrice,
            StandingPrice = seatPrice * 0.5,
            LogePrice = seatPrice * 4,
            ComfortLevel = comfort,
            HasRoof = roof,
        };

        [Fact]
        public void BetterForm_IncreasesAttendance()
        {
            var stadium = CreateStadium();
            var poorForm = AttendanceModel.Calculate(stadium, 0, 10, 18, 3, 20);
            var greatForm = AttendanceModel.Calculate(stadium, 15, 10, 18, 3, 20);

            Assert.True(greatForm.TotalAttendance > poorForm.TotalAttendance);
        }

        [Fact]
        public void HigherLeaguePosition_IncreasesAttendance()
        {
            var stadium = CreateStadium();
            var bottom = AttendanceModel.Calculate(stadium, 8, 18, 18, 3, 20);
            var top = AttendanceModel.Calculate(stadium, 8, 1, 18, 3, 20);

            Assert.True(top.TotalAttendance > bottom.TotalAttendance);
        }

        [Fact]
        public void StrongerOpponent_IncreasesAttendance()
        {
            var stadium = CreateStadium();
            var weakOpponent = AttendanceModel.Calculate(stadium, 8, 10, 18, 4, 20);
            var strongOpponent = AttendanceModel.Calculate(stadium, 8, 10, 18, 1, 20);

            Assert.True(strongOpponent.TotalAttendance > weakOpponent.TotalAttendance);
        }

        [Fact]
        public void HigherComfortLevel_IncreasesAttendance()
        {
            var lowComfort = CreateStadium(comfort: 1);
            var highComfort = CreateStadium(comfort: 5);

            var lowResult = AttendanceModel.Calculate(lowComfort, 8, 10, 18, 3, 20);
            var highResult = AttendanceModel.Calculate(highComfort, 8, 10, 18, 3, 20);

            Assert.True(highResult.TotalAttendance > lowResult.TotalAttendance);
        }

        [Fact]
        public void HigherPriceThanBaseline_DecreasesAttendance()
        {
            var cheap = CreateStadium(seatPrice: 15);
            var expensive = CreateStadium(seatPrice: 40);

            var cheapResult = AttendanceModel.Calculate(cheap, 8, 10, 18, 3, 20);
            var expensiveResult = AttendanceModel.Calculate(expensive, 8, 10, 18, 3, 20);

            Assert.True(cheapResult.TotalAttendance > expensiveResult.TotalAttendance);
        }

        [Fact]
        public void Demand_NeverDropsBelowMinimumFloor()
        {
            var stadium = CreateStadium(seatPrice: 500, comfort: 1);
            var result = AttendanceModel.Calculate(stadium, 0, 18, 18, 4, 20);

            Assert.True(result.AvgFillRate >= 0.10);
        }

        [Fact]
        public void Demand_NeverExceedsCapacity()
        {
            var stadium = CreateStadium(seatPrice: 1, comfort: 5, roof: true);
            var result = AttendanceModel.Calculate(stadium, 15, 1, 18, 1, 20);

            Assert.True(result.SeatingSold <= stadium.SeatingCapacity);
            Assert.True(result.StandingSold <= stadium.StandingCapacity);
            Assert.True(result.LogeSold <= stadium.LogeCapacity);
        }

        [Fact]
        public void PriceIncrease_HasConvexImpact_SmallIncreaseBarelyHurts_BigIncreaseHurtsALot()
        {
            var baseline = CreateStadium(seatPrice: 10);
            var slightlyPricier = CreateStadium(seatPrice: 12); // +20%
            var muchPricier = CreateStadium(seatPrice: 20); // +100%

            var baseResult = AttendanceModel.Calculate(baseline, 8, 10, 18, 3, baselinePrice: 10);
            var slightResult = AttendanceModel.Calculate(slightlyPricier, 8, 10, 18, 3, baselinePrice: 10);
            var bigResult = AttendanceModel.Calculate(muchPricier, 8, 10, 18, 3, baselinePrice: 10);

            int smallDrop = baseResult.TotalAttendance - slightResult.TotalAttendance;
            int bigDrop = baseResult.TotalAttendance - bigResult.TotalAttendance;

            // +20% Preis darf nur einen kleinen Bruchteil des Rückgangs von +100% Preis ausmachen
            // (konvexe Kurve, nicht proportional/linear).
            Assert.True(smallDrop < bigDrop / 3);
        }

        [Fact]
        public void StandingSellsBetterThanSeating_AtSameDemand()
        {
            var stadium = CreateStadium();
            var result = AttendanceModel.Calculate(stadium, 8, 10, 18, 3, 20);

            double seatingFill = (double)result.SeatingSold / stadium.SeatingCapacity;
            double standingFill = (double)result.StandingSold / stadium.StandingCapacity;
            Assert.True(standingFill >= seatingFill);
        }
    }
}
