using RetroFootballManager.Models;
using SQLite;

namespace RetroFootballManager.Data.Repositories
{
    // Saves and loads a complete team including players, stadium, finances,
    // statistics, and employees (sqlite-net has no automatic relations).
    public class TeamRepository
    {
        private readonly AppDatabase _db;

        public TeamRepository(AppDatabase db)
        {
            _db = db;
        }

        // includeYouth = false skips persisting youth prospects (they don't change during a
        // matchday, so the matchday loop avoids re-saving hundreds of unchanged youth rows).
        //
        // Everything below runs as ONE transaction (was: one Find + Insert/Update roundtrip pair
        // PER entity, no shared transaction - the difference between a handful of disk syncs and
        // 50+ of them for a single team save, see CalendarAdvanceService which calls this once per
        // touched team, per day). team.Id must be resolved (via the first UpsertSync call, which
        // assigns the auto-increment Id synchronously) BEFORE it's copied onto player/employee/etc.
        // TeamId - both happen inside the same synchronous callback so that ordering is guaranteed.
        public async Task<int> SaveTeamAsync(Team team, bool includeYouth = true)
        {
            await _db.Connection.RunInTransactionAsync(conn =>
            {
                UpsertSync(conn, team, team.Id);

                foreach (var player in team.Players)
                {
                    player.TeamId = team.Id;
                    player.IsYouthProspect = false;
                    UpsertSync(conn, player, player.Id);
                }

                if (includeYouth)
                {
                    foreach (var youth in team.YouthPlayers)
                    {
                        youth.TeamId = team.Id;
                        youth.IsYouthProspect = true;
                        UpsertSync(conn, youth, youth.Id);
                    }
                }

                foreach (var employee in team.Employees)
                {
                    employee.TeamId = team.Id;
                    UpsertSync(conn, employee, employee.Id);
                }

                if (team.Stadium is not null)
                {
                    team.Stadium.TeamId = team.Id;
                    UpsertSync(conn, team.Stadium, team.Stadium.Id);
                }
                if (team.Finances is not null)
                {
                    team.Finances.TeamId = team.Id;
                    UpsertSync(conn, team.Finances, team.Finances.Id);
                }
                if (team.ActiveLoan is not null)
                {
                    team.ActiveLoan.TeamId = team.Id;
                    UpsertSync(conn, team.ActiveLoan, team.ActiveLoan.Id);
                }
                if (team.Statistics is not null)
                {
                    team.Statistics.TeamId = team.Id;
                    UpsertSync(conn, team.Statistics, team.Statistics.Id);
                }
                if (team.ManagerProfile is not null)
                {
                    team.ManagerProfile.TeamId = team.Id;
                    UpsertSync(conn, team.ManagerProfile, team.ManagerProfile.Id);
                }
            });

            return team.Id;
        }

        // Id == 0 means "never saved" - a plain Insert leaves the AutoIncrement PK column out of
        // the statement so SQLite assigns a fresh one (and sqlite-net writes it back onto entity).
        // InsertOrReplace always includes the PK column, so for Id == 0 it would insert the
        // literal value 0 instead of auto-assigning - fine for an already-known Id (replaces the
        // existing row in place), wrong for a brand-new one.
        private static void UpsertSync<T>(SQLiteConnection conn, T entity, int id) where T : new()
        {
            if (id == 0)
                conn.Insert(entity);
            else
                conn.InsertOrReplace(entity);
        }

        public async Task<Team?> GetTeamAsync(int teamId)
        {
            var team = await _db.Connection.FindAsync<Team>(teamId);
            if (team is null)
                return null;

            await HydrateAsync(team);
            return team;
        }

        public async Task<List<Team>> GetAllTeamsAsync()
        {
            var teams = await _db.Connection.Table<Team>().ToListAsync();
            foreach (var team in teams)
                await HydrateAsync(team);

            return teams;
        }

        private async Task HydrateAsync(Team team)
        {
            var allPlayers = await _db.Connection.Table<Player>()
                .Where(p => p.TeamId == team.Id).ToListAsync();
            team.Players = allPlayers.Where(p => !p.IsYouthProspect).ToList();
            team.YouthPlayers = allPlayers.Where(p => p.IsYouthProspect).ToList();
            team.Employees = await _db.Connection.Table<Employee>()
                .Where(e => e.TeamId == team.Id).ToListAsync();
            team.Stadium = await _db.Connection.Table<Stadium>()
                .Where(s => s.TeamId == team.Id).FirstOrDefaultAsync();
            team.Finances = await _db.Connection.Table<Finances>()
                .Where(f => f.TeamId == team.Id).FirstOrDefaultAsync();
            team.ActiveLoan = await _db.Connection.Table<ClubLoan>()
                .Where(l => l.TeamId == team.Id && l.Status == ClubLoanStatus.Active).FirstOrDefaultAsync();
            team.Statistics = await _db.Connection.Table<TeamStats>()
                .Where(t => t.TeamId == team.Id).FirstOrDefaultAsync();
            team.ManagerProfile = await _db.Connection.Table<ManagerProfile>()
                .Where(m => m.TeamId == team.Id).FirstOrDefaultAsync();
        }

        public async Task DeleteTeamAsync(int teamId)
        {
            await _db.Connection.Table<Player>().Where(p => p.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<Employee>().Where(e => e.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<Stadium>().Where(s => s.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<Finances>().Where(f => f.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<ClubLoan>().Where(l => l.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<TeamStats>().Where(t => t.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<ManagerProfile>().Where(m => m.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<Contract>().Where(c => c.TeamId == teamId).DeleteAsync();
            await _db.Connection.Table<Sponsorship>().Where(s => s.TeamId == teamId).DeleteAsync();
            await _db.Connection.DeleteAsync<Team>(teamId);
        }
    }
}
