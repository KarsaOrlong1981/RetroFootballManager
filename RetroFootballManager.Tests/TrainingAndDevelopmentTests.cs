using RetroFootballManager.Common;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TrainingAndDevelopmentTests
    {
        private static readonly DateTime DevDate = new(2027, 8, 1);

        // Moral defaults to 50 = neutral (matches PlayerMoraleFactor's convention), so existing
        // pace-calibration tests are unaffected unless a test deliberately varies it.
        private static Player MakePlayer(int age, int talent, int attr = 50, int id = 1,
            Position pos = Position.CentralMidfielder, int moral = 50)
        {
            return new Player
            {
                Id = id,
                Age = age,
                Talent = talent,
                DateOfBirth = DevDate.AddYears(-age),
                Position = pos,
                Moral = moral,
                OffensivePower = attr, DefensivePower = attr, GameIntelligence = attr,
                PressingIntensity = attr, CounterSpeed = attr, PassingAccuracy = attr,
                DuelHardness = attr, DuelEfficiency = attr, CrossingAccuracy = attr,
                GkReflexes = attr, GkHandling = attr, GkOneOnOne = attr,
                GkDistribution = attr, GkAerialControl = attr,
                HeaderStrength = attr, Jumping = attr, Dribbling = attr,
                LongShotAccuracy = attr, PenaltyKick = attr, FreeKick = attr,
                Finishing = attr, Positioning = attr,
                Rating = attr,
                Status = PlayerStatus.Available,
            };
        }

        private static Team MakeTeam(Employee? coach = null)
        {
            var team = new Team { Statistics = new TeamStats() };
            if (coach is not null)
                team.Employees.Add(coach);
            return team;
        }

        // A season is ~34 weekly ticks (one per matchday) - training is now gated to one
        // application per week (see TrainingService.ApplyWeeklyTraining), not repeatable
        // on-demand clicks, so tests simulate a season as 34 sequential Train() calls.
        private const int WeeksPerSeason = 34;

        [Fact]
        public void Training_YoungTalentedPlayer_ImprovesFasterThanOldOne_ButStaysBounded()
        {
            var coach = new Employee { EmployeeType = EmployeeType.AssistantCoach, OffensiveTraining = 90, DefensiveTraining = 90 };
            var team = MakeTeam(coach);

            var young = MakePlayer(age: 18, talent: 88, attr: 50, id: 1);
            var old = MakePlayer(age: 34, talent: 55, attr: 50, id: 2);

            var rng = new Random(1);
            for (int i = 0; i < WeeksPerSeason; i++)
            {
                TrainingService.Train(young, TrainableAttribute.Offensive, team, rng);
                TrainingService.Train(old, TrainableAttribute.Offensive, team, rng);
            }

            // Meaningful development for the young talent (expected ~+17 over a season)...
            Assert.True(young.OffensivePower > 58, $"young was {young.OffensivePower}");
            // ...but NOT overpowered within a single season, even under ideal conditions.
            Assert.True(young.OffensivePower < 85, $"young was {young.OffensivePower}");
            Assert.True(young.OffensivePower > old.OffensivePower);
        }

        [Fact]
        public void Training_CannotExceedTalentCeiling()
        {
            var team = MakeTeam();
            var player = MakePlayer(age: 20, talent: 60, attr: 68); // cap = 60 + 8 = 68
            var rng = new Random(2);

            // Several seasons' worth of weekly ticks - even at the slower weekly pace this
            // must still never cross the talent ceiling.
            for (int i = 0; i < WeeksPerSeason * 6; i++)
                TrainingService.Train(player, TrainableAttribute.Offensive, team, rng);

            Assert.True(player.OffensivePower <= 68);
        }

        [Fact]
        public void Training_GoodCoach_BeatsNoCoach()
        {
            var withCoach = MakeTeam(new Employee { OffensiveTraining = 95 });
            var noCoach = MakeTeam();

            var a = MakePlayer(age: 20, talent: 85, attr: 50, id: 1);
            var b = MakePlayer(age: 20, talent: 85, attr: 50, id: 2);

            for (int i = 0; i < WeeksPerSeason; i++)
            {
                TrainingService.Train(a, TrainableAttribute.Offensive, withCoach, new Random(100 + i));
                TrainingService.Train(b, TrainableAttribute.Offensive, noCoach, new Random(100 + i));
            }

            Assert.True(a.OffensivePower >= b.OffensivePower);
        }

        [Fact]
        public void ApplyWeeklyTraining_HumanTeam_IgnoresDifficultyScale()
        {
            var team = MakeTeam();
            var p = MakePlayer(age: 18, talent: 90, attr: 50, id: 1);
            p.CurrentTrainingFocus = TrainableAttribute.Offensive;
            team.Players.Add(p);

            var rng = new Random(11);
            for (int i = 0; i < WeeksPerSeason; i++)
                TrainingService.ApplyWeeklyTraining(team, isHuman: true, Difficulty.Easy, rng);

            // Human pace must match a plain Train() loop regardless of the (Easy) difficulty,
            // since difficulty only ever scales AI opponents, never the manager's own team.
            var reference = MakePlayer(age: 18, talent: 90, attr: 50, id: 2);
            var refRng = new Random(11);
            for (int i = 0; i < WeeksPerSeason; i++)
                TrainingService.Train(reference, TrainableAttribute.Offensive, team, refRng);

            Assert.Equal(reference.OffensivePower, p.OffensivePower);
        }

        [Fact]
        public void EnsureAiFocusAssigned_PicksWeakestAttribute_AndTeamFocusFromStyle()
        {
            var team = MakeTeam();
            team.PlayingStyle = PlayingStyle.Pressing;
            var p = MakePlayer(age: 20, talent: 70, attr: 60, id: 1);
            p.PressingIntensity = 30; // deliberately the weakest stat
            team.Players.Add(p);

            TrainingService.EnsureAiFocusAssigned(team, new Random(3));

            Assert.Equal(TrainableAttribute.Pressing, p.CurrentTrainingFocus);
            Assert.Equal(TeamTrainingFocus.Pressing, team.TeamTrainingFocus);
        }

        [Fact]
        public void ApplyWeeklyTraining_AiTeam_GrowsSlowerWithPoorMoraleAndFitness()
        {
            Team BuildAiTeam(int morale, int fitness)
            {
                var team = MakeTeam();
                team.Statistics = new TeamStats();
                while (team.Statistics.Morale < morale)
                    team.Statistics.BonusPayment();
                var p = MakePlayer(age: 19, talent: 90, attr: 50, id: 1, pos: Position.Forward);
                p.Fitness = fitness;
                p.CurrentTrainingFocus = TrainableAttribute.Offensive;
                team.Players.Add(p);
                return team;
            }

            var thriving = BuildAiTeam(morale: 90, fitness: 95);
            var struggling = BuildAiTeam(morale: 10, fitness: 40);

            for (int i = 0; i < WeeksPerSeason; i++)
            {
                TrainingService.ApplyWeeklyTraining(thriving, isHuman: false, Difficulty.Normal, new Random(200 + i));
                TrainingService.ApplyWeeklyTraining(struggling, isHuman: false, Difficulty.Normal, new Random(200 + i));
            }

            Assert.True(thriving.Players[0].OffensivePower >= struggling.Players[0].OffensivePower);
        }

        [Fact]
        public void ApplicableAttributes_GoalkeeperOnlyOffersGkAttributes_OutfieldNever()
        {
            var gkAttrs = TrainingService.ApplicableAttributes(Position.Goalkeeper);
            var outfieldAttrs = TrainingService.ApplicableAttributes(Position.Forward);

            Assert.Contains(TrainableAttribute.GkReflexes, gkAttrs);
            Assert.Contains(TrainableAttribute.GkHandling, gkAttrs);
            Assert.Contains(TrainableAttribute.GkOneOnOne, gkAttrs);
            Assert.Contains(TrainableAttribute.GkDistribution, gkAttrs);
            Assert.Contains(TrainableAttribute.GkAerialControl, gkAttrs);
            Assert.DoesNotContain(TrainableAttribute.Offensive, gkAttrs);
            Assert.DoesNotContain(TrainableAttribute.Crossing, gkAttrs);

            Assert.DoesNotContain(TrainableAttribute.GkReflexes, outfieldAttrs);
            Assert.Contains(TrainableAttribute.Offensive, outfieldAttrs);
        }

        [Fact]
        public void EnsureAiFocusAssigned_Goalkeeper_PicksWeakestGkAttribute_NeverOutfield()
        {
            var team = MakeTeam();
            var gk = MakePlayer(age: 22, talent: 70, attr: 60, id: 1, pos: Position.Goalkeeper);
            gk.GkHandling = 15; // deliberately the weakest GK stat
            team.Players.Add(gk);

            TrainingService.EnsureAiFocusAssigned(team, new Random(4));

            Assert.Equal(TrainableAttribute.GkHandling, gk.CurrentTrainingFocus);
        }

        [Fact]
        public void Train_GoalkeeperAttribute_ImprovesAndRecalculatesRatingFromGkAttributes()
        {
            var team = MakeTeam();
            var gk = MakePlayer(age: 19, talent: 88, attr: 50, id: 1, pos: Position.Goalkeeper);
            var rng = new Random(12);

            for (int i = 0; i < WeeksPerSeason; i++)
                TrainingService.Train(gk, TrainableAttribute.GkReflexes, team, rng);

            Assert.True(gk.GkReflexes > 50);
            // Rating for a goalkeeper must be derived from GK-specific attributes, not the
            // (untouched, still 50) outfield ones like OffensivePower/CrossingAccuracy.
            Assert.True(gk.Rating > 50);
        }

        [Fact]
        public void ApplyWeeklyTraining_TeamFocus_NeverTrainsGoalkeeperOutfieldAttributes()
        {
            var team = MakeTeam();
            team.TeamTrainingFocus = TeamTrainingFocus.Offensive;
            var gk = MakePlayer(age: 22, talent: 80, attr: 50, id: 1, pos: Position.Goalkeeper);
            team.Players.Add(gk);

            for (int i = 0; i < WeeksPerSeason; i++)
                TrainingService.ApplyWeeklyTraining(team, isHuman: true, Difficulty.Normal, new Random(30 + i));

            // Team-wide "Offensive" focus trains Offensive/Crossing for outfielders - the
            // goalkeeper must be excluded, so these stay untouched.
            Assert.Equal(50, gk.OffensivePower);
            Assert.Equal(50, gk.CrossingAccuracy);
        }

        [Fact]
        public void Train_LowMoralePlayer_ProgressesSlowerThanHighMorale()
        {
            var team = MakeTeam();
            var happy = MakePlayer(age: 20, talent: 85, attr: 50, id: 1, moral: 95);
            var demotivated = MakePlayer(age: 20, talent: 85, attr: 50, id: 2, moral: 5);

            for (int i = 0; i < WeeksPerSeason; i++)
            {
                TrainingService.Train(happy, TrainableAttribute.Offensive, team, new Random(300 + i));
                TrainingService.Train(demotivated, TrainableAttribute.Offensive, team, new Random(300 + i));
            }

            Assert.True(happy.OffensivePower > demotivated.OffensivePower);
        }

        [Fact]
        public void ApplicableAttributes_Outfield_IncludesNewHeaderDribblingSetPieceAttributes()
        {
            var outfieldAttrs = TrainingService.ApplicableAttributes(Position.Forward);
            var gkAttrs = TrainingService.ApplicableAttributes(Position.Goalkeeper);

            Assert.Contains(TrainableAttribute.HeaderStrength, outfieldAttrs);
            Assert.Contains(TrainableAttribute.Jumping, outfieldAttrs);
            Assert.Contains(TrainableAttribute.Dribbling, outfieldAttrs);
            Assert.Contains(TrainableAttribute.LongShot, outfieldAttrs);
            Assert.Contains(TrainableAttribute.PenaltyKick, outfieldAttrs);
            Assert.Contains(TrainableAttribute.FreeKick, outfieldAttrs);
            Assert.DoesNotContain(TrainableAttribute.HeaderStrength, gkAttrs);
        }

        [Fact]
        public void Train_HeaderStrength_ImprovesAndRecalculatesOutfieldRating()
        {
            var team = MakeTeam();
            var forward = MakePlayer(age: 19, talent: 88, attr: 50, id: 1, pos: Position.Forward);
            var rng = new Random(13);

            for (int i = 0; i < WeeksPerSeason; i++)
                TrainingService.Train(forward, TrainableAttribute.HeaderStrength, team, rng);

            Assert.True(forward.HeaderStrength > 50);
        }

        [Fact]
        public void Development_OldPlayer_DeclinesPhysically()
        {
            var team = MakeTeam();
            var old = MakePlayer(age: 34, talent: 60, attr: 70);
            team.Players.Add(old);

            int before = old.CounterSpeed + old.PressingIntensity + old.DuelHardness;
            DevelopmentService.DevelopSquad(team, DevDate, new Random(5));
            int after = old.CounterSpeed + old.PressingIntensity + old.DuelHardness;

            Assert.True(after < before);
        }

        [Fact]
        public void Development_YouthAgesAndGraduatesAtTwenty()
        {
            var team = MakeTeam();
            var youth = MakePlayer(age: 19, talent: 80, attr: 45);
            youth.IsYouthProspect = true;
            youth.DateOfBirth = DevDate.AddYears(-20); // will be 20 on DevDate
            team.YouthPlayers.Add(youth);

            DevelopmentService.DevelopSquad(team, DevDate, new Random(6));

            Assert.DoesNotContain(youth, team.YouthPlayers);
            Assert.Contains(youth, team.Players);
            Assert.False(youth.IsYouthProspect);
        }

        [Fact]
        public void Development_MentoredYouth_GrowsAtLeastAsMuch()
        {
            var mentor = MakePlayer(age: 30, talent: 85, attr: 82, id: 99);

            Team BuildYouthTeam(bool withMentor)
            {
                var team = MakeTeam();
                team.Players.Add(mentor);
                var youth = MakePlayer(age: 16, talent: 80, attr: 45, id: 1);
                youth.IsYouthProspect = true;
                youth.DateOfBirth = DevDate.AddYears(-16);
                if (withMentor) youth.MentorId = mentor.Id;
                team.YouthPlayers.Add(youth);
                return team;
            }

            var mentored = BuildYouthTeam(true);
            var plain = BuildYouthTeam(false);
            DevelopmentService.DevelopSquad(mentored, DevDate, new Random(7));
            DevelopmentService.DevelopSquad(plain, DevDate, new Random(7));

            Assert.True(mentored.YouthPlayers[0].Rating >= plain.YouthPlayers[0].Rating);
        }

        [Fact]
        public void Development_GoalkeeperWithGoodCoach_GrowsAtLeastAsMuch()
        {
            Team BuildTeam(bool withCoach)
            {
                var team = MakeTeam();
                var keeper = MakePlayer(age: 20, talent: 82, attr: 55, id: 1, pos: Position.Goalkeeper);
                team.Players.Add(keeper);
                if (withCoach)
                    team.Employees.Add(new Employee { EmployeeType = EmployeeType.GoalkeeperCoach, GoalkeeperTraining = 90 });
                return team;
            }

            var coached = BuildTeam(true);
            var plain = BuildTeam(false);
            DevelopmentService.DevelopSquad(coached, DevDate, new Random(11));
            DevelopmentService.DevelopSquad(plain, DevDate, new Random(11));

            Assert.True(coached.Players[0].Rating >= plain.Players[0].Rating);
        }

        [Fact]
        public void Development_YoungPlayerWithMinutes_GrowsMore_AndResetsSeasonMinutes()
        {
            Team BuildTeam(int minutes)
            {
                var team = MakeTeam();
                var p = MakePlayer(age: 20, talent: 82, attr: 55, id: 1);
                p.SeasonMinutes = minutes;
                team.Players.Add(p);
                return team;
            }

            var played = BuildTeam(1800);
            var benched = BuildTeam(0);
            DevelopmentService.DevelopSquad(played, DevDate, new Random(9));
            DevelopmentService.DevelopSquad(benched, DevDate, new Random(9));

            Assert.True(played.Players[0].Rating >= benched.Players[0].Rating);
            Assert.Equal(0, played.Players[0].SeasonMinutes);
        }

        [Fact]
        public void YouthGenerator_ProducesProspectsInExpectedRange()
        {
            var youth = YouthGenerator.GenerateYouthSquad(
                tier: 1, count: 20, Nationality.Germany, DevDate, new Random(3));

            Assert.Equal(20, youth.Count);
            Assert.All(youth, y =>
            {
                Assert.True(y.IsYouthProspect);
                Assert.InRange(y.Age, 15, 19);
                Assert.True(y.Talent >= (int)y.Rating);
            });
        }

        [Fact]
        public void Formation_HasElevenSlots_WithKeeperFirst_AndNormalisedCoords()
        {
            foreach (var formation in FormationCatalog.All)
            {
                Assert.Equal(11, formation.Slots.Count);
                Assert.Equal(Position.Goalkeeper, formation.Slots[0].Position);
                Assert.All(formation.Slots, s =>
                {
                    Assert.InRange(s.X, 0.0, 1.0);
                    Assert.InRange(s.Y, 0.0, 1.0);
                });
            }
        }
    }
}
