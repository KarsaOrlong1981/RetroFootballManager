using System.Text.Json;
using SQLite;

namespace RetroFootballManager.Models
{
    // One team's current stock/price per article. Not part of the Team object graph
    // (like ScoutingFocus/TrainingCamp) - fetched directly via MerchandiseRepository.
    public class MerchandiseInventory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int TeamId { get; set; }

        // Persisted as JSON since sqlite-net can't store complex arrays directly (same
        // approach as Sponsor.BonusTypesRaw). IMPORTANT: the getter deserializes a FRESH
        // list every time - always read it into a local variable once, mutate that, then
        // assign it back via the setter exactly once per operation. Reading/mutating/writing
        // across multiple separate ".Articles" accesses silently loses the mutation.
        public string ArticlesRaw { get; set; } = "[]";

        [Ignore]
        public List<MerchandiseArticleStock> Articles
        {
            get => JsonSerializer.Deserialize<List<MerchandiseArticleStock>>(ArticlesRaw) ?? [];
            set => ArticlesRaw = JsonSerializer.Serialize(value);
        }
    }

    public class MerchandiseArticleStock
    {
        public MerchandiseArticleType Type { get; set; }
        public int Stock { get; set; }
        public int SellPrice { get; set; }

        // Season-to-date running totals, reset by MerchandiseService.RolloverSeason - lets
        // the Merchandise page show real sales numbers per article, not just current stock.
        public int TotalUnitsSold { get; set; }
        public int TotalRevenue { get; set; }

        // Last weekly tick's result only (overwritten every ApplyWeeklySalesAsync) - shown
        // on the page as "letzte Woche verkauft" alongside the season totals above.
        public int LastWeekUnitsSold { get; set; }
        public int LastWeekRevenue { get; set; }
    }
}
