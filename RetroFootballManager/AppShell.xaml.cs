using RetroFootballManager.Views;

namespace RetroFootballManager
{
    public partial class AppShell : Shell
    {
        public static AppShell? Instance { get; private set; }

        public AppShell()
        {
            InitializeComponent();
            Instance = this;

            // Global gepushte Routen (Detail-/Zwischenseiten).
            Routing.RegisterRoute("teamselection", typeof(TeamSelectionPage));
            Routing.RegisterRoute("fixtures", typeof(FixturesTablesPage));
            Routing.RegisterRoute("cupoverview", typeof(CupOverviewPage));
            Routing.RegisterRoute("trophies", typeof(TrophyCasePage));
            Routing.RegisterRoute("scouting", typeof(ScoutingPage));
            Routing.RegisterRoute("matchday", typeof(MatchDayPage));
            Routing.RegisterRoute("cupmatchday", typeof(CupMatchDayPage));
            Routing.RegisterRoute("lineup", typeof(LineupPage));
            Routing.RegisterRoute("training", typeof(TrainingPage));
            Routing.RegisterRoute("teamtraining", typeof(TeamTrainingPage));
            Routing.RegisterRoute("youth", typeof(YouthPage));
            Routing.RegisterRoute("statistics", typeof(StatisticsPage));
            Routing.RegisterRoute("finances", typeof(FinancesPage));
            Routing.RegisterRoute("club", typeof(ClubPage));
            Routing.RegisterRoute("stadium", typeof(StadiumPage));
            Routing.RegisterRoute("clubloan", typeof(ClubLoanPage));
            Routing.RegisterRoute("sponsors", typeof(SponsorsPage));
            Routing.RegisterRoute("staff", typeof(StaffPage));
            Routing.RegisterRoute("transfermarket", typeof(TransferMarketPage));
            Routing.RegisterRoute("inbox", typeof(InboxPage));
            Routing.RegisterRoute("friendlymatchday", typeof(FriendlyMatchDayPage));
            Routing.RegisterRoute("options", typeof(OptionsPage));
            Routing.RegisterRoute("gameover", typeof(GameOverPage));
            Routing.RegisterRoute("talktoplayer", typeof(TalkToPlayerPage));
        }
    }
}
