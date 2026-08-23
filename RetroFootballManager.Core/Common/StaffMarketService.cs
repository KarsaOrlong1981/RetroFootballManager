using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // A real staff market: generates a candidate list to browse (not
    // persisted until hired) and handles hiring/firing including the contract.
    public class StaffMarketService
    {
        private static readonly int[] TierTargetRating = [77, 67, 57, 47];
        private static readonly EmployeeType[] CandidateTypes =
        [
            EmployeeType.AssistantCoach, EmployeeType.FitnessCoach, EmployeeType.GoalkeeperCoach,
            EmployeeType.YouthCoach, EmployeeType.Scout, EmployeeType.Psychologist,
            EmployeeType.Analyst, EmployeeType.MedicalStaff, EmployeeType.Physiotherapist,
            EmployeeType.DirectorOfFootball,
        ];

        private readonly AppDatabase _db;
        private readonly TeamRepository _teams;
        private readonly ContractRepository _contracts;
        private readonly Random _random;

        public StaffMarketService(AppDatabase db, TeamRepository teams, ContractRepository contracts, Random? random = null)
        {
            _db = db;
            _teams = teams;
            _contracts = contracts;
            _random = random ?? Random.Shared;
        }

        // Per-league hiring caps: DirectorOfFootball is always unique (max 1) regardless of
        // league; every other type scales with league tier (Tier4=3, Tier3=4, Tier2=5, Tier1=6).
        public static int MaxEmployeesPerType(int leagueTier, EmployeeType type) =>
            type == EmployeeType.DirectorOfFootball ? 1 : Math.Clamp(7 - leagueTier, 3, 6);

        public static bool CanHire(Team team, EmployeeType type, out string? error)
        {
            if (!FinanceService.HasSpendableBalance(team))
            {
                error = "Der Verein ist im Minus - keine Neuverpflichtungen möglich, bis die Bilanz wieder positiv ist.";
                return false;
            }

            int max = MaxEmployeesPerType(team.LeagueTier, type);
            int current = team.Employees.Count(e => e.EmployeeType == type);
            if (current >= max)
            {
                error = type == EmployeeType.DirectorOfFootball
                    ? "Es kann immer nur ein Sportdirektor gleichzeitig angestellt sein."
                    : $"Es können maximal {max} Mitarbeiter vom Typ {type} gleichzeitig angestellt sein (Liga {team.LeagueTier}).";
                return false;
            }
            error = null;
            return true;
        }

        public List<Employee> GenerateCandidates(int teamTier, int count = 8)
        {
            double quality = TierTargetRating[Math.Clamp(teamTier, 1, TierTargetRating.Length) - 1] - 10;
            var candidates = new List<Employee>(count);
            for (int i = 0; i < count; i++)
            {
                var type = CandidateTypes[_random.Next(CandidateTypes.Length)];
                candidates.Add(StaffGenerator.GenerateStaff(type, quality, Nationality.Germany, _random));
            }

            FaceImageAssigner.AssignStaffFaces(candidates, _random);
            return candidates;
        }

        public async Task<Contract> HireAsync(Team team, Employee candidate, DateTime hireDate, int durationSeasons = 3)
        {
            candidate.TeamId = team.Id;
            team.Employees.Add(candidate);

            // Persist the team first so sqlite-net assigns the candidate's real Id -
            // the Contract row below needs that Id as its HolderId.
            await _teams.SaveTeamAsync(team, includeYouth: false);

            var contract = new Contract
            {
                HolderId = candidate.Id,
                HolderType = ContractHolderType.Employee,
                TeamId = team.Id,
                StartDate = hireDate,
                EndDate = hireDate.AddYears(durationSeasons),
                AnnualSalary = candidate.Salary,
                MarketValue = candidate.MarketValue,
            };

            await _contracts.SaveAsync(contract);
            return contract;
        }

        public async Task FireAsync(Team team, Employee employee)
        {
            team.Employees.RemoveAll(e => e.Id == employee.Id);
            await _db.Connection.DeleteAsync<Employee>(employee.Id);

            var contracts = await _contracts.GetByHolderAsync(employee.Id, ContractHolderType.Employee);
            foreach (var contract in contracts.Where(c => c.TeamId == team.Id))
                await _contracts.DeleteAsync(contract.Id);
        }
    }
}
