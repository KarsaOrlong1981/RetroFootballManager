using RetroFootballManager.Common;
using RetroFootballManager.Models;

namespace RetroFootballManager.Tests
{
    public class StaffGeneratorGenderTests
    {
        [Fact]
        public void GenerateStaff_FirstNameAlwaysMatchesRolledGender()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var employee = StaffGenerator.GenerateStaff(
                    EmployeeType.Scout, quality: 60, Nationality.Germany, new Random(seed));

                string firstName = employee.Name.Split(' ', 2)[0];
                bool isFemaleName = NameBank.IsFemaleFirstName(Nationality.Germany, firstName);

                Assert.Equal(employee.Gender == Gender.Female, isFemaleName);
            }
        }

        [Fact]
        public void FixGenderNameMismatch_CorrectsGenderAndClearsImagePath()
        {
            var employee = new Employee
            {
                Name = "Paul Neumann",
                Nationality = Nationality.Germany,
                Gender = Gender.Female,
                ImagePath = "some/female/photo.png",
            };

            StaffGenerator.FixGenderNameMismatch(employee);

            Assert.Equal(Gender.Male, employee.Gender);
            Assert.Null(employee.ImagePath);
        }

        [Fact]
        public void FixGenderNameMismatch_LeavesCorrectlyMatchedEmployeeUntouched()
        {
            var employee = new Employee
            {
                Name = "Anna Müller",
                Nationality = Nationality.Germany,
                Gender = Gender.Female,
                ImagePath = "some/female/photo.png",
            };

            StaffGenerator.FixGenderNameMismatch(employee);

            Assert.Equal(Gender.Female, employee.Gender);
            Assert.Equal("some/female/photo.png", employee.ImagePath);
        }
    }
}
