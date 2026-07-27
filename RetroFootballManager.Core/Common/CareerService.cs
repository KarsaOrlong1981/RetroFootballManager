using System.Text.Json;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    /// <summary>
    /// Manages the global, cross‑save meta‑progression (CareerProfile).
    ///Points are awarded for achievements and permanently unlock higher starting leagues.
    ///The profile is saved as a separate json file).
   /// </summary>
    public class CareerService
    {
        private static readonly ILog Log = LogManager.GetLogger<CareerService>();

        public const int Tier3Threshold = 100;
        public const int Tier2Threshold = 300;
        public const int Tier1Threshold = 700;

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly string _profilePath;
        private CareerProfile? _cached;

        public CareerService(string profilePath)
        {
            _profilePath = profilePath;
        }

        public CareerProfile Load()
        {
            if (_cached is not null)
                return _cached;

            try
            {
                if (File.Exists(_profilePath))
                {
                    var json = File.ReadAllText(_profilePath);
                    _cached = JsonSerializer.Deserialize<CareerProfile>(json) ?? new CareerProfile();
                }
                else
                {
                    _cached = new CareerProfile();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load career profile, starting fresh.", ex);
                _cached = new CareerProfile();
            }

            return _cached;
        }

        public void Save()
        {
            if (_cached is null)
                return;

            try
            {
                var dir = Path.GetDirectoryName(_profilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_profilePath, JsonSerializer.Serialize(_cached, JsonOptions));
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save career profile.", ex);
            }
        }

        public int Points => Load().Points;

        public int HighestUnlockedTier
        {
            get
            {
                int points = Points;
                if (points >= Tier1Threshold) return 1;
                if (points >= Tier2Threshold) return 2;
                if (points >= Tier3Threshold) return 3;
                return 4;
            }
        }

        public bool IsTierUnlocked(int tier) => tier >= HighestUnlockedTier;

        public int PointsToNextTier()
        {
            int points = Points;
            if (points < Tier3Threshold) return Tier3Threshold - points;
            if (points < Tier2Threshold) return Tier2Threshold - points;
            if (points < Tier1Threshold) return Tier1Threshold - points;
            return 0;
        }

        public void AwardPoints(int season, string reason, int points)
        {
            var profile = Load();
            profile.Points += points;
            profile.Awards.Add(new CareerAward { Season = season, Reason = reason, Points = points });
            Save();
            Log.Info($"Career: +{points} pts ({reason}), total {profile.Points}.");
        }

        public Task ResetAsync()
        {
            _cached = new CareerProfile();
            Save();
            Log.Info("Career profile reset to zero.");
            return Task.CompletedTask;
        }
    }
}
