namespace RetroFootballManager.Services
{
    // Thin wrapper around Preferences.Default - keeps the raw MAUI API out of ViewModels.
    public class AppSettingsService
    {
        private const string DefaultMatchSpeedKey = "DefaultMatchSpeed";
        private const int DefaultMatchSpeedFallback = 2;

        public int DefaultMatchSpeed
        {
            get => Preferences.Default.Get(DefaultMatchSpeedKey, DefaultMatchSpeedFallback);
            set => Preferences.Default.Set(DefaultMatchSpeedKey, value);
        }
    }
}
