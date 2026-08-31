using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    public record EmployeeAttributeSummary(IReadOnlyList<AttributeChip> Chips)
    {
        public static EmployeeAttributeSummary From(Employee e) => new(e.EmployeeType switch
        {
            EmployeeType.Scout => [new("Scouting", e.ScoutingAbility)],
            EmployeeType.AssistantCoach =>
            [
                new("Offensiv Training", e.OffensiveTraining),
                new("Defensiv Training", e.DefensiveTraining),
            ],
            EmployeeType.FitnessCoach => [new("Fitness Training", e.FitnessTraining)],
            EmployeeType.Psychologist => [new("Motivation", e.Motivation)],
            EmployeeType.GoalkeeperCoach => [new("Torwart Training", e.GoalkeeperTraining)],
            EmployeeType.Analyst => [new("Analyse", e.AnalysisAbility)],
            EmployeeType.YouthCoach => [new("Jugendentwicklung", e.YouthDevelopment)],
            EmployeeType.MedicalStaff => [new("Fitness Training", e.FitnessTraining)],
            EmployeeType.Physiotherapist => [new("Fitness Training", e.FitnessTraining)],
            EmployeeType.DirectorOfFootball =>
            [
                new("Verkaufsverhandlung", e.SellingNegotiation),
                new("Gegenangebote", e.CounterOfferNegotiation),
                new("Verhandlungshärte", e.AcceptanceFirmness),
                new("Finanzmanagement", e.FinancialManagement),
            ],
            _ => [],
        });
    }
}
