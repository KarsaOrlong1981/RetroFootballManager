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
            var gender = rng.NextDouble() < 0.5 ? Gender.Male : Gender.Female;
            var (firstName, lastName) = NameBank.GetRandomName(nationality, gender, rng);

            int Around(double center) => Math.Clamp((int)Math.Round(center + rng.Next(-8, 9)), 1, 99);

            // Base spread for all coaching ratings.
            int off = Around(quality), def = Around(quality), gk = Around(quality * 0.7),
                fit = Around(quality), youth = Around(quality), scout = Around(quality),
                mot = Around(quality), analysis = Around(quality),
                sell = Around(quality), counter = Around(quality), firm = Around(quality),
                finance = Around(quality);

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
                case EmployeeType.DirectorOfFootball:
                    sell = Around(quality + 14); counter = Around(quality + 14);
                    firm = Around(quality + 14); finance = Around(quality + 14);
                    break;
            }

            double rating = new[] { off, def, gk, fit, youth, scout, mot, analysis, sell, counter, firm, finance }.Average();

            return new Employee
            {
                Name = $"{firstName} {lastName}",
                EmployeeType = type,
                Nationality = nationality,
                Age = rng.Next(25, 66),
                Gender = gender,
                Rating = Math.Round(rating, 1),
                OffensiveTraining = off,
                DefensiveTraining = def,
                GoalkeeperTraining = gk,
                FitnessTraining = fit,
                YouthDevelopment = youth,
                ScoutingAbility = scout,
                Motivation = mot,
                AnalysisAbility = analysis,
                SellingNegotiation = sell,
                CounterOfferNegotiation = counter,
                AcceptanceFirmness = firm,
                FinancialManagement = finance,
                MarketValue = Math.Round(rating * rating * 400),
                Salary = Math.Round(rating * 1200),
            };
        }

        // Legacy safety net for saves created before Age existed (new column defaults to 0,
        // which would otherwise pass FaceImageAssigner's "<= 25" check) - a no-op once Age is
        // set, safe to call on every load (same pattern as PlayerGenerator's Backfill*IfMissing).
        public static void BackfillAgeIfMissing(Employee employee, Random? random = null)
        {
            if (employee.Age > 0)
                return;

            var rng = random ?? new Random(employee.Id);
            employee.Age = rng.Next(25, 66);
        }

        // One-time self-heal for employees generated in the brief window where Gender was
        // rolled independently of the name pool, which could pair e.g. "Paul" with a female
        // portrait. Detects the mismatch via NameBank's female-name list and clears ImagePath
        // too, so FaceImageAssigner picks a matching-gender photo on the same load.
        public static void FixGenderNameMismatch(Employee employee)
        {
            string firstName = employee.Name.Split(' ', 2)[0];
            var correctGender = NameBank.IsFemaleFirstName(employee.Nationality, firstName)
                ? Gender.Female
                : Gender.Male;
            if (employee.Gender == correctGender)
                return;

            employee.Gender = correctGender;
            employee.ImagePath = null;
        }
    }
}
