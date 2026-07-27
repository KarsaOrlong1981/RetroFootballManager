using RetroFootballManager.Models;
using SQLite;

namespace RetroFootballManager.Data
{
    // Opens the SQLite database file and creates all tables on first start.
    public class AppDatabase
    {
        public SQLiteAsyncConnection Connection { get; }

        public AppDatabase(string databasePath)
        {
            Connection = new SQLiteAsyncConnection(databasePath);
        }

        public async Task InitializeAsync()
        {
            await Connection.CreateTableAsync<Team>();
            await Connection.CreateTableAsync<Player>();
            await Connection.CreateTableAsync<PlayerStats>();
            await Connection.CreateTableAsync<Stadium>();
            await Connection.CreateTableAsync<Finances>();
            await Connection.CreateTableAsync<ClubLoan>();
            await Connection.CreateTableAsync<TeamStats>();
            await Connection.CreateTableAsync<Employee>();
            await Connection.CreateTableAsync<GameState>();
            await Connection.CreateTableAsync<League>();
            await Connection.CreateTableAsync<Fixture>();
            await Connection.CreateTableAsync<Contract>();
            await Connection.CreateTableAsync<Sponsor>();
            await Connection.CreateTableAsync<Sponsorship>();
            await Connection.CreateTableAsync<CupTie>();
            await Connection.CreateTableAsync<TransferListing>();
            await Connection.CreateTableAsync<TransferOffer>();
            await Connection.CreateTableAsync<LoanAgreement>();
            await Connection.CreateTableAsync<Message>();
            await Connection.CreateTableAsync<TrainingCamp>();
            await Connection.CreateTableAsync<TrophyRecord>();
            await Connection.CreateTableAsync<ScoutingAssignment>();
            await Connection.CreateTableAsync<ScoutedPlayer>();
        }

        // Closes the underlying native SQLite connection (e.g. for tests that want to
        // delete the database file afterwards).
        public Task CloseAsync() => Connection.CloseAsync();

        // Deletes all save-game data (not the CareerProfile - that's deliberately kept
        // outside the DB). Called before creating a new game so teams/leagues/fixtures
        // from previous "new game" runs don't pile up.
        public async Task ClearGameDataAsync()
        {
            await Connection.DeleteAllAsync<Player>();
            await Connection.DeleteAllAsync<PlayerStats>();
            await Connection.DeleteAllAsync<Stadium>();
            await Connection.DeleteAllAsync<Finances>();
            await Connection.DeleteAllAsync<ClubLoan>();
            await Connection.DeleteAllAsync<TeamStats>();
            await Connection.DeleteAllAsync<Employee>();
            await Connection.DeleteAllAsync<Fixture>();
            await Connection.DeleteAllAsync<League>();
            await Connection.DeleteAllAsync<Contract>();
            await Connection.DeleteAllAsync<Sponsor>();
            await Connection.DeleteAllAsync<Sponsorship>();
            await Connection.DeleteAllAsync<CupTie>();
            await Connection.DeleteAllAsync<TransferListing>();
            await Connection.DeleteAllAsync<TransferOffer>();
            await Connection.DeleteAllAsync<LoanAgreement>();
            await Connection.DeleteAllAsync<Message>();
            await Connection.DeleteAllAsync<TrainingCamp>();
            await Connection.DeleteAllAsync<TrophyRecord>();
            await Connection.DeleteAllAsync<ScoutingAssignment>();
            await Connection.DeleteAllAsync<ScoutedPlayer>();
            await Connection.DeleteAllAsync<Team>();
            await Connection.DeleteAllAsync<GameState>();
        }
    }
}
