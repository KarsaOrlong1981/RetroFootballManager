using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    public record MerchandiseWeeklySalesSummary(int UnitsSold, int Revenue);

    // Merchandise department: buy the 10 catalog articles wholesale, set a sell price, sell
    // weekly at a rate driven by fanbase size, season performance and price. AI teams get no
    // shop UI of their own - RunAiWeeklyTick keeps them stocked with a small buffer so they
    // still generate merchandise income and aren't structurally disadvantaged (see
    // ClubManagementAiService/TacticAiService for the same "AI parity" reasoning elsewhere).
    public class MerchandiseService
    {
        private readonly MerchandiseRepository _inventories;

        // Difficulty-scaled restock buffer (Easy/Normal/Hard) - a Hard AI runs a noticeably
        // tighter, better-stocked shop than an Easy one, same "difficulty scales AI activity/
        // quality" convention as ClubManagementAiService's staff-role list. Easy additionally
        // skips restocking some weeks entirely (EasySkipRestockChance) to reflect genuinely
        // worse management, not just a smaller target.
        private static readonly int[] AiRestockTargetByDifficulty = [20, 50, 120];
        private const double EasySkipRestockChance = 0.3;

        public MerchandiseService(MerchandiseRepository inventories)
        {
            _inventories = inventories;
        }

        // Thin wrapper so ViewModels can persist their in-memory inventory (staged
        // buy/price changes) on "Bestätigen" without reaching into the repository directly -
        // same reasoning as every other confirm-and-navigate screen (Stadium/Training/...).
        public Task SaveInventoryAsync(MerchandiseInventory inventory) => _inventories.SaveAsync(inventory);

        public async Task<MerchandiseInventory> GetOrCreateInventoryAsync(int teamId)
        {
            var inventory = await _inventories.GetByTeamAsync(teamId);
            if (inventory is not null)
                return inventory;

            inventory = new MerchandiseInventory { TeamId = teamId };
            inventory.Articles = MerchandiseCatalog.Articles
                .Select(a => new MerchandiseArticleStock { Type = a.Type, Stock = 0, SellPrice = a.DefaultSellPrice })
                .ToList();
            return inventory;
        }

        // Wholesale purchase - deducts cost immediately from the team's balance. Reads and
        // writes MerchandiseInventory.Articles exactly once (single deserialize/reserialize
        // round trip), see the warning on MerchandiseInventory.Articles.
        public static bool TryBuyStock(Team team, MerchandiseInventory inventory, MerchandiseArticleType type, int quantity)
        {
            if (quantity <= 0 || team.Finances is null)
                return false;

            var articles = inventory.Articles;
            var article = articles.FirstOrDefault(a => a.Type == type);
            if (article is null)
                return false;

            int cost = quantity * MerchandiseCatalog.Get(type).WholesaleCost;
            if (team.Finances.CurrentBalance < cost)
                return false;

            article.Stock += quantity;
            team.Finances.CurrentBalance -= cost;
            team.Finances.MerchandiseArticleExpense += cost;
            inventory.Articles = articles;
            return true;
        }

        // Corrects an over-purchase mistake - refunds only half the wholesale cost (not a
        // risk-free buy/sell-back loop) and reduces MerchandiseArticleExpense rather than
        // booking it as "Income", so a bulk sell-back doesn't pollute the real weekly-sales
        // statistics (LastWeek/TotalUnitsSold) that reflect genuine customer demand.
        public const double SellBackRefundFraction = 0.5;

        public static bool TrySellBackStock(Team team, MerchandiseInventory inventory, MerchandiseArticleType type, int quantity)
        {
            if (quantity <= 0 || team.Finances is null)
                return false;

            var articles = inventory.Articles;
            var article = articles.FirstOrDefault(a => a.Type == type);
            if (article is null || article.Stock < quantity)
                return false;

            int refund = (int)Math.Round(quantity * MerchandiseCatalog.Get(type).WholesaleCost * SellBackRefundFraction);
            article.Stock -= quantity;
            team.Finances.CurrentBalance += refund;
            team.Finances.MerchandiseArticleExpense = Math.Max(0, team.Finances.MerchandiseArticleExpense - refund);
            inventory.Articles = articles;
            return true;
        }

        // Sell price floor is the wholesale cost - selling below cost is never allowed.
        public static void SetPrice(MerchandiseInventory inventory, MerchandiseArticleType type, int sellPrice)
        {
            var articles = inventory.Articles;
            var article = articles.FirstOrDefault(a => a.Type == type);
            if (article is null)
                return;

            article.SellPrice = Math.Max(MerchandiseCatalog.Get(type).WholesaleCost, sellPrice);
            inventory.Articles = articles;
        }

        // Weekly sales tick - called from MatchDayService alongside the other weekly hooks
        // (TrainingService.ApplyWeeklyTraining etc.), once per team that played this matchday.
        // AI teams additionally get a difficulty-scaled restock (RunAiWeeklyTick) and a chance
        // to launch their own membership campaign (ClubMembershipService.TryRunAiCampaignTick,
        // itself gated on having hired a Director of Football) - full parity with the human's
        // own Merchandise page, no special-casing.
        public async Task<MerchandiseWeeklySalesSummary> ApplyWeeklySalesAsync(
            Team team, bool isHuman, StandingRow? standingRow, int leagueSize, DateTime currentDate,
            Difficulty difficulty = Difficulty.Normal, Random? random = null, double aiCautionFactor = 1.0)
        {
            var inventory = await GetOrCreateInventoryAsync(team.Id);
            double performanceFactor = MerchandiseSalesCalculator.PerformanceFactor(standingRow, leagueSize);

            if (!isHuman)
            {
                var rng = random ?? Random.Shared;
                RunAiWeeklyTick(team, inventory, difficulty, rng);
                ClubMembershipService.TryRunAiCampaignTick(team, currentDate, performanceFactor, difficulty, aiCautionFactor, rng);
            }

            double? bestPlayerRating = team.Players.Count > 0 ? team.Players.Max(p => p.Rating) : null;
            int clubMembers = team.Finances?.ClubMembers ?? 0;

            var articles = inventory.Articles;
            int totalUnits = 0, totalRevenue = 0;
            foreach (var article in articles)
            {
                var sale = MerchandiseSalesCalculator.CalculateWeeklySale(article, clubMembers, performanceFactor, currentDate, bestPlayerRating);
                article.Stock -= sale.UnitsSold;
                article.LastWeekUnitsSold = sale.UnitsSold;
                article.LastWeekRevenue = sale.Revenue;
                article.TotalUnitsSold += sale.UnitsSold;
                article.TotalRevenue += sale.Revenue;
                totalUnits += sale.UnitsSold;
                totalRevenue += sale.Revenue;
            }
            inventory.Articles = articles;

            if (team.Finances is not null)
            {
                team.Finances.MerchandiseArticleIncome += totalRevenue;
                team.Finances.CurrentBalance += totalRevenue;
            }

            await _inventories.SaveAsync(inventory);
            return new MerchandiseWeeklySalesSummary(totalUnits, totalRevenue);
        }

        // Keeps every article stocked to a difficulty-scaled buffer, funds permitting - no
        // pricing decisions, AI teams just keep the default catalog markup. Easy additionally
        // skips the whole tick some weeks, reflecting genuinely worse (not just smaller-target)
        // management.
        private static void RunAiWeeklyTick(Team team, MerchandiseInventory inventory, Difficulty difficulty, Random random)
        {
            if (!FinanceService.HasSpendableBalance(team))
                return;

            if (difficulty == Difficulty.Easy && random.NextDouble() < EasySkipRestockChance)
                return;

            int target = AiRestockTargetByDifficulty[(int)difficulty];
            foreach (var def in MerchandiseCatalog.Articles)
            {
                var articles = inventory.Articles;
                int stock = articles.FirstOrDefault(a => a.Type == def.Type)?.Stock ?? 0;
                int need = target - stock;
                if (need <= 0)
                    continue;

                int affordable = team.Finances is null ? 0 : team.Finances.CurrentBalance / Math.Max(def.WholesaleCost, 1);
                int buy = Math.Min(need, affordable);
                if (buy > 0)
                    TryBuyStock(team, inventory, def.Type, buy);
            }
        }
    }
}
