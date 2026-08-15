using RetroFootballManager.Models;

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
        public async Task<int> SaveTeamAsync(Team team, bool includeYouth = true)
        {
            await UpsertAsync(team, team.Id);

            foreach (var player in team.Players)
            {
                player.TeamId = team.Id;
                player.IsYouthProspect = false;
                await UpsertAsync(player, player.Id);
            }

            if (includeYouth)
            {
                foreach (var youth in team.YouthPlayers)
                {
                    youth.TeamId = team.Id;
                    youth.IsYouthProspect = true;
                    await UpsertAsync(youth, youth.Id);
                }
            }

            foreach (var employee in team.Employees)
            {
                employee.TeamId = team.Id;
                await UpsertAsync(employee, employee.Id);
            }

            if (team.Stadium is not null)
            {
                team.Stadium.TeamId = team.Id;
                await UpsertAsync(team.Stadium, team.Stadium.Id);
            }

            if (team.Finances is not null)
            {
                team.Finances.TeamId = team.Id;
                await UpsertAsync(team.Finances, team.Finances.Id);
            }

            if (team.ActiveLoan is not null)
            {
                team.ActiveLoan.TeamId = team.Id;
                await UpsertAsync(team.ActiveLoan, team.ActiveLoan.Id);
            }

            if (team.Statistics is not null)
            {
                team.Statistics.TeamId = team.Id;
                await UpsertAsync(team.Statistics, team.Statistics.Id);
            }

            if (team.ManagerProfile is not null)
            {
                team.ManagerProfile.TeamId = team.Id;
                await UpsertAsync(team.ManagerProfile, team.ManagerProfile.Id);
            }

            return team.Id;
        }

        // Inserts a new row or updates it - regardless of whether the caller already
        // pre-assigned the Id (e.g. in tests or editors); as long as no matching row
        // exists in the DB, it still inserts.
        private async Task UpsertAsync<T>(T entity, int id) where T : new()
        {
            bool exists = id != 0 && await _db.Connection.FindAsync<T>(id) is not null;
            if (exists)
                await _db.Connection.UpdateAsync(entity);
            else
                await _db.Connection.InsertAsync(entity);
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
