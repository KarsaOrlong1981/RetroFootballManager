using RetroFootballManager.Common;
using RetroFootballManager.Models;

namespace RetroFootballManager.Tests
{
    public class FaceImageAssignerTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly string _originalFacesRoot;

        public FaceImageAssignerTests()
        {
            _originalFacesRoot = FaceImageAssigner.FacesRootPath;
            _tempRoot = Path.Combine(Path.GetTempPath(), "RfmFaceTests_" + Guid.NewGuid());
            FaceImageAssigner.FacesRootPath = _tempRoot;

            SeedBucket("Players", "CentralEurope", "centraleurope");
            SeedBucket("Players", "Anglosphere", "anglosphere");
            SeedBucket("Staff", Path.Combine("NorthCentralEurope", "male"), "staffmale");
            SeedBucket("Staff", Path.Combine("NorthCentralEurope", "female"), "stafffemale");
        }

        public void Dispose()
        {
            FaceImageAssigner.FacesRootPath = _originalFacesRoot;
            Directory.Delete(_tempRoot, recursive: true);
        }

        private void SeedBucket(string kind, string bucketRelativePath, string prefix, int count = 5)
        {
            string dir = Path.Combine(_tempRoot, kind, bucketRelativePath);
            Directory.CreateDirectory(dir);
            for (int i = 0; i < count; i++)
                File.WriteAllText(Path.Combine(dir, $"{prefix}{i}.png"), "");
        }

        private static Player MakePlayer(int age, Nationality nationality) => new()
        {
            Name = "Test Player",
            Age = age,
            Nationality = nationality,
            Position = Position.Forward,
        };

        [Fact]
        public void AssignPlayerFaces_SkipsPlayersOlderThanMaxFaceAge()
        {
            var old = MakePlayer(FaceImageAssigner.MaxFaceAge + 1, Nationality.Germany);
            FaceImageAssigner.AssignPlayerFaces([old], new Random(1));

            Assert.Null(old.ImagePath);
        }

        [Fact]
        public void AssignPlayerFaces_AssignsYoungPlayerFromMatchingBucket()
        {
            var young = MakePlayer(FaceImageAssigner.MaxFaceAge, Nationality.Germany);
            FaceImageAssigner.AssignPlayerFaces([young], new Random(1));

            Assert.NotNull(young.ImagePath);
            Assert.Contains(Path.Combine("Players", "CentralEurope"), young.ImagePath);
        }

        [Fact]
        public void AssignPlayerFaces_DoesNotOverwriteExistingImagePath()
        {
            var player = MakePlayer(20, Nationality.Germany);
            player.ImagePath = "already-set.png";

            FaceImageAssigner.AssignPlayerFaces([player], new Random(1));

            Assert.Equal("already-set.png", player.ImagePath);
        }

        [Fact]
        public void AssignPlayerFaces_NeverAssignsSamePhotoTwiceWithinOneSquad()
        {
            var squad = Enumerable.Range(0, 5).Select(_ => MakePlayer(20, Nationality.England)).ToList();
            FaceImageAssigner.AssignPlayerFaces(squad, new Random(42));

            var paths = squad.Select(p => p.ImagePath).ToList();
            Assert.All(paths, Assert.NotNull);
            Assert.Equal(paths.Count, paths.Distinct().Count());
        }

        [Fact]
        public void AssignStaffFaces_PicksGenderFolderMatchingRolledGender()
        {
            var employee = new Employee { Name = "Test Coach", Age = 24, Nationality = Nationality.Germany };
            FaceImageAssigner.AssignStaffFaces([employee], new Random(7));

            Assert.NotNull(employee.ImagePath);
            string expectedFolder = employee.Gender == Gender.Female ? "female" : "male";
            Assert.Contains(Path.Combine("NorthCentralEurope", expectedFolder), employee.ImagePath);
        }

        [Fact]
        public void AssignStaffFaces_AssignsImageRegardlessOfAge()
        {
            // Unlike players, staff always get a (youthful-looking, for now) photo -
            // there's no older-looking pack yet to gate on MaxFaceAge.
            var employee = new Employee { Name = "Veteran Coach", Age = 45, Nationality = Nationality.Germany };
            FaceImageAssigner.AssignStaffFaces([employee], new Random(7));

            Assert.NotNull(employee.ImagePath);
        }
    }
}
