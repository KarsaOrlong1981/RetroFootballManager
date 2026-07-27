using Microsoft.Extensions.Logging;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Services;
using RetroFootballManager.ViewModels;
using RetroFootballManager.Views;
using Serilog;

namespace RetroFootballManager
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            ConfigureSerilog();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            RegisterServices(builder.Services);
            RegisterViewModelsAndPages(builder.Services);

            return builder.Build();
        }

        private static void ConfigureSerilog()
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "logs", "rfm-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .CreateLogger();

            Log.Information("RetroFootballManager gestartet.");
        }

        private static void RegisterServices(IServiceCollection services)
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "retrofootball.db3");
            var careerPath = Path.Combine(FileSystem.AppDataDirectory, "career.json");

            services.AddSingleton(new AppDatabase(dbPath));
            services.AddSingleton<SaveGameService>();
            services.AddSingleton<TeamRepository>();
            services.AddSingleton<LeagueRepository>();
            services.AddSingleton<FixtureRepository>();
            services.AddSingleton<PlayerRepository>();
            services.AddSingleton<PlayerStatsService>();
            services.AddSingleton<CalendarService>();
            services.AddSingleton<ContractRepository>();
            services.AddSingleton<SponsorRepository>();
            services.AddSingleton<SponsorshipRepository>();
            services.AddSingleton<CupTieRepository>();
            services.AddSingleton<TransferListingRepository>();
            services.AddSingleton<TransferOfferRepository>();
            services.AddSingleton<LoanAgreementRepository>();
            services.AddSingleton<MessageRepository>();
            services.AddSingleton<MessageService>();
            services.AddSingleton<ExpiryWarningService>();
            services.AddSingleton<TrainingCampRepository>();
            services.AddSingleton<TrainingCampService>();
            services.AddSingleton<FinanceService>();
            services.AddSingleton<SponsorService>();
            services.AddSingleton<StaffMarketService>();
            services.AddSingleton<TransferMarketService>();
            services.AddSingleton<AiManagerService>();
            services.AddSingleton(sp => new MatchDayService(
                sp.GetRequiredService<FixtureRepository>(),
                sp.GetRequiredService<TeamRepository>(),
                sp.GetRequiredService<PlayerRepository>(),
                sp.GetRequiredService<FinanceService>(),
                sp.GetRequiredService<AiManagerService>(),
                sp.GetRequiredService<MessageService>()));
            services.AddSingleton<CupMatchDayService>();
            services.AddSingleton<CalendarAdvanceService>();
            services.AddSingleton<FriendlyService>();
            services.AddSingleton(new CareerService(careerPath));
            services.AddSingleton<GameSession>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<AppSettingsService>();

            // MAUI dispatcher for BaseViewModel (UI thread marshalling).
            services.AddSingleton<IDispatcher>(_ =>
                Application.Current?.Dispatcher ?? Dispatcher.GetForCurrentThread()!);
        }

        private static void RegisterViewModelsAndPages(IServiceCollection services)
        {
            services.AddTransient<StartViewModel>();
            services.AddTransient<TeamSelectionViewModel>();
            services.AddTransient<MainMenuViewModel>();
            services.AddTransient<FixturesTablesViewModel>();
            services.AddTransient<CupOverviewViewModel>();
            services.AddTransient<TrophyCaseViewModel>();
            services.AddTransient<ScoutingViewModel>();
            services.AddTransient<MatchDayViewModel>();
            services.AddTransient<CupMatchDayViewModel>();
            services.AddTransient<LineupViewModel>();
            services.AddTransient<TrainingViewModel>();
            services.AddTransient<TeamTrainingViewModel>();
            services.AddTransient<YouthViewModel>();
            services.AddTransient<StatisticsViewModel>();
            services.AddTransient<FinancesViewModel>();
            services.AddTransient<ClubViewModel>();
            services.AddTransient<StadiumViewModel>();
            services.AddTransient<ClubLoanViewModel>();
            services.AddTransient<SponsorsViewModel>();
            services.AddTransient<StaffViewModel>();
            services.AddTransient<TransferMarketViewModel>();
            services.AddTransient<InboxViewModel>();
            services.AddTransient<FriendlyMatchDayViewModel>();
            services.AddTransient<OptionsViewModel>();

            services.AddTransient<StartPage>();
            services.AddTransient<TeamSelectionPage>();
            services.AddTransient<MainMenuPage>();
            services.AddTransient<FixturesTablesPage>();
            services.AddTransient<CupOverviewPage>();
            services.AddTransient<TrophyCasePage>();
            services.AddTransient<ScoutingPage>();
            services.AddTransient<MatchDayPage>();
            services.AddTransient<CupMatchDayPage>();
            services.AddTransient<LineupPage>();
            services.AddTransient<TrainingPage>();
            services.AddTransient<TeamTrainingPage>();
            services.AddTransient<YouthPage>();
            services.AddTransient<StatisticsPage>();
            services.AddTransient<FinancesPage>();
            services.AddTransient<ClubPage>();
            services.AddTransient<StadiumPage>();
            services.AddTransient<ClubLoanPage>();
            services.AddTransient<SponsorsPage>();
            services.AddTransient<StaffPage>();
            services.AddTransient<TransferMarketPage>();
            services.AddTransient<InboxPage>();
            services.AddTransient<FriendlyMatchDayPage>();
            services.AddTransient<OptionsPage>();
        }
    }
}
