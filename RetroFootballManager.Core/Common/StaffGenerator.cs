using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Creates staff members with specialised coaching ratings. The rating relevant to the
    // employee's role is strong; the rest scatter around a general quality level.
    public static class StaffGenerator
    {
        public static Employee GenerateStaff(
            EmployeeType type,
            double quality,
            Nationality nationality = Nationality.Germany,
            Random? random = null)
        {
            var rng = random ?? Random.Shared;
            var (firstName, lastName) = NameBank.GetRandomName(nationality, rng);

            int Around(double center) => Math.Clamp((int)Math.Round(center + rng.Next(-8, 9)), 1, 99);

            // Base spread for all coaching ratings.
            int off = Around(quality), def = Around(quality), gk = Around(quality * 0.7),
                fit = Around(quality), youth = Around(quality), scout = Around(quality),
                mot = Around(quality), analysis = Around(quality);

            // Emphasise the rating matching the role.
            switch (type)
            {
                case EmployeeType.AssistantCoach: off = Around(quality + 6); def = Around(quality + 6); break;
                case EmployeeType.GoalkeeperCoach: gk = Around(quality + 14); break;
                case EmployeeType.FitnessCoach: fit = Around(quality + 14); break;
                case EmployeeType.YouthCoach: youth = Around(quality + 14); break;
                case EmployeeType.Scout: scout = Around(quality + 14); break;
                case EmployeeType.Psychologist: mot = Around(quality + 14); break;
                case EmployeeType.Analyst: analysis = Around(quality + 14); break;
                // No dedicated "medical" field exists on Employee - FitnessTraining doubles as
                // the skill both Physiotherapist/MedicalStaff bonuses key on (see Match.
                // ApplyMedicalStaffReduction), same as FitnessCoach's own emphasis.
                case EmployeeType.Physiotherapist: fit = Around(quality + 14); break;
                case EmployeeType.MedicalStaff: fit = Around(quality + 14); break;
            }

            double rating = new[] { off, def, gk, fit, youth, scout, mot, analysis }.Average();

            return new Employee
            {
                Name = $"{firstName} {lastName}",
                EmployeeType = type,
                Rating = Math.Round(rating, 1),
                OffensiveTraining = off,
                DefensiveTraining = def,
                GoalkeeperTraining = gk,
                FitnessTraining = fit,
                YouthDevelopment = youth,
                ScoutingAbility = scout,
                Motivation = mot,
                AnalysisAbility = analysis,
                MarketValue = Math.Round(rating * rating * 400),
                Salary = Math.Round(rating * 1200),
            };
        }
    }
}
