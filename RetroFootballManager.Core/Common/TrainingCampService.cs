using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;

namespace RetroFootballManager.Common
{
    // Training camps (M7 refinement): booking is instant, but the effect (morale + optional
    // attribute boost) only applies once EndDate is reached (see ApplyDueCampsAsync, called from
    // CalendarAdvanceService's daily step).
    public class TrainingCampService
    {
        private const int AttributeCap = 99;

        private readonly TrainingCampRepository _camps;
        private readonly FixtureRepository _fixtures;
        private readonly MessageService _messages;
        private readonly Random _random;

        public TrainingCampService(TrainingCampRepository camps, FixtureRepository fixtures, MessageService messages, Random? random = null)
        {
            _camps = camps;
            _fixtures = fixtures;
            _messages = messages;
            _random = random ?? Random.Shared;
        }

        // PreSeason ends at SeasonStart, WinterBreak at the first matchday of the second half -
        // null outside these phases (camps then can't be booked).
        public static DateTime? GetWindowEndDate(GameState state, SeasonPhaseInfo phase, IReadOnlyList<Fixture> seasonFixtures)
        {
            if (phase.Phase == SeasonPhase.PreSeason)
                return state.SeasonStart;

            if (phase.Phase == SeasonPhase.WinterBreak && seasonFixtures.Count > 0)
            {
                int firstHalfCount = seasonFixtures.Max(f => f.Matchday) / 2;
                return seasonFixtures.Where(f => f.Matchday == firstHalfCount + 1).Min(f => f.Date);
            }

            return null;
        }

        // For an overview of "is a camp currently running, which tier, how much time left" -
        // per CanBookAsync a team can never have more than one at a time anyway.
        public async Task<TrainingCamp?> GetActiveCampAsync(int teamId, DateTime currentDate) =>
            (await _camps.GetOverlappingAsync(teamId, currentDate)).FirstOrDefault();

        public async Task<(bool Allowed, string? Reason)> CanBookAsync(
            int teamId, int durationWeeks, DateTime currentDate, DateTime? windowEnd)
        {
            if (windowEnd is null)
                return (false, "Trainingslager sind nur in Vorbereitung oder Winterpause möglich.");

            var existing = await _camps.GetUnappliedByTeamAsync(teamId);
            if (existing.Count > 0)
                return (false, "Es läuft bereits ein Trainingslager.");

            var campEnd = currentDate.AddDays(durationWeeks * 7);
            if (campEnd > windowEnd.Value)
            {
                int daysLeft = (windowEnd.Value.Date - currentDate.Date).Days;
                return (false, daysLeft <= 0
                    ? "Keine Vorbereitungszeit mehr übrig."
                    : $"Nur noch {daysLeft} Tag(e) Zeit - dieses Lager passt nicht mehr hinein.");
            }

            // Symmetric to FriendlyService.CanScheduleAsync's camp conflict check - a friendly
            // (or other fixture) already scheduled within the planned camp period blocks the
            // booking instead of allowing both at once.
            if (await _fixtures.HasFixtureInRangeAsync(teamId, currentDate, campEnd))
                return (false, "In diesem Zeitraum steht bereits ein Freundschaftsspiel an.");

            return (true, null);
        }

        public async Task<TrainingCamp> BookAsync(Team team, TrainingCampTier tier, int durationWeeks, DateTime currentDate)
        {
            var option = TrainingCampCatalog.Get(tier, durationWeeks);
            var camp = new TrainingCamp
            {
                TeamId = team.Id,
                Tier = tier,
                DurationWeeks = durationWeeks,
                StartDate = currentDate,
                EndDate = currentDate.AddDays(durationWeeks * 7),
                Cost = option.Cost,
                MoraleBoost = option.MoraleBoost,
                GrantsAttributeBoost = option.GrantsAttributeBoost,
                Applied = false,
            };
            await _camps.SaveAsync(camp);

            if (team.Finances is not null)
            {
                team.Finances.CurrentBalance -= (int)option.Cost;
                team.Finances.OtherExpenses += (int)option.Cost;
            }

            return camp;
        }

        // sendMessage=false for AI teams - the inbox is exclusively for the user's team,
        // otherwise other teams' camp completions would show up there. Return value: whether a
        // camp was actually applied - CalendarAdvanceService uses this to only save teams that
        // really changed instead of the whole league every day.
        public async Task<bool> ApplyDueCampsAsync(Team team, DateTime currentDate, bool sendMessage = true)
        {
            var camps = await _camps.GetUnappliedByTeamAsync(team.Id);
            var dueCamps = camps.Where(c => c.EndDate <= currentDate).ToList();
            foreach (var camp in dueCamps)
            {
                if (team.Statistics is not null)
                    team.Statistics.MoraleBoost += camp.MoraleBoost;

                if (camp.GrantsAttributeBoost)
                    foreach (var player in team.Players)
                        BoostKeyAttributes(player);

                camp.Applied = true;
                await _camps.SaveAsync(camp);

                if (sendMessage)
                {
                    await _messages.SendAsync(MessageType.TrainingCampFinished, "Trainingslager beendet",
                        camp.GrantsAttributeBoost
                            ? $"Das {camp.Tier}-Trainingslager ist beendet - Moral gestiegen, einige Spieler haben sich verbessert."
                            : $"Das {camp.Tier}-Trainingslager ist beendet - Moral gestiegen.",
                        currentDate, team.Id);
                }
            }

            return dueCamps.Count > 0;
        }

        // 70% chance +1, 30% chance +2 on the two key attributes for the player's position.
        private void BoostKeyAttributes(Player player)
        {
            int amount = _random.NextDouble() < 0.7 ? 1 : 2;
            foreach (var attribute in KeyAttributesFor(player.Position))
                SetAttribute(player, attribute, Math.Min(AttributeCap, GetAttribute(player, attribute) + amount));
        }

        private static IEnumerable<TrainableAttribute> KeyAttributesFor(Position position) => position switch
        {
            Position.Goalkeeper => [TrainableAttribute.GkReflexes, TrainableAttribute.GkOneOnOne],
            Position.CentralDefender or Position.LeftDefender or Position.RightDefender =>
                [TrainableAttribute.Defensive, TrainableAttribute.DuelHardness],
            Position.LeftWingBack or Position.RightWingBack =>
                [TrainableAttribute.DuelHardness, TrainableAttribute.Crossing],
            Position.DefensiveMidfielder => [TrainableAttribute.DuelHardness, TrainableAttribute.Positioning],
            Position.CentralMidfielder => [TrainableAttribute.Passing, TrainableAttribute.GameIntelligence],
            Position.LeftMidfielder or Position.RightMidfielder =>
                [TrainableAttribute.Crossing, TrainableAttribute.Dribbling],
            Position.CentralOffenseMidfielder or Position.LeftOffenseMidfielder or Position.RightOffenseMidfielder =>
                [TrainableAttribute.Passing, TrainableAttribute.Dribbling],
            Position.Forward => [TrainableAttribute.Offensive, TrainableAttribute.HeaderStrength],
            _ => [],
        };

        private static int GetAttribute(Player player, TrainableAttribute attribute) => attribute switch
        {
            TrainableAttribute.Offensive => player.OffensivePower,
            TrainableAttribute.Defensive => player.DefensivePower,
            TrainableAttribute.GameIntelligence => player.GameIntelligence,
            TrainableAttribute.Pressing => player.PressingIntensity,
            TrainableAttribute.CounterSpeed => player.CounterSpeed,
            TrainableAttribute.Passing => player.PassingAccuracy,
            TrainableAttribute.DuelHardness => player.DuelHardness,
            TrainableAttribute.DuelEfficiency => player.DuelEfficiency,
            TrainableAttribute.Crossing => player.CrossingAccuracy,
            TrainableAttribute.GkReflexes => player.GkReflexes,
            TrainableAttribute.GkHandling => player.GkHandling,
            TrainableAttribute.GkOneOnOne => player.GkOneOnOne,
            TrainableAttribute.GkDistribution => player.GkDistribution,
            TrainableAttribute.GkAerialControl => player.GkAerialControl,
            TrainableAttribute.HeaderStrength => player.HeaderStrength,
            TrainableAttribute.Jumping => player.Jumping,
            TrainableAttribute.Dribbling => player.Dribbling,
            TrainableAttribute.LongShot => player.LongShotAccuracy,
            TrainableAttribute.Finishing => player.Finishing,
            TrainableAttribute.Positioning => player.Positioning,
            _ => 0,
        };

        private static void SetAttribute(Player player, TrainableAttribute attribute, int value)
        {
            switch (attribute)
            {
                case TrainableAttribute.Offensive: player.OffensivePower = value; break;
                case TrainableAttribute.Defensive: player.DefensivePower = value; break;
                case TrainableAttribute.GameIntelligence: player.GameIntelligence = value; break;
                case TrainableAttribute.Pressing: player.PressingIntensity = value; break;
                case TrainableAttribute.CounterSpeed: player.CounterSpeed = value; break;
                case TrainableAttribute.Passing: player.PassingAccuracy = value; break;
                case TrainableAttribute.DuelHardness: player.DuelHardness = value; break;
                case TrainableAttribute.DuelEfficiency: player.DuelEfficiency = value; break;
                case TrainableAttribute.Crossing: player.CrossingAccuracy = value; break;
                case TrainableAttribute.GkReflexes: player.GkReflexes = value; break;
                case TrainableAttribute.GkHandling: player.GkHandling = value; break;
                case TrainableAttribute.GkOneOnOne: player.GkOneOnOne = value; break;
                case TrainableAttribute.GkDistribution: player.GkDistribution = value; break;
                case TrainableAttribute.GkAerialControl: player.GkAerialControl = value; break;
                case TrainableAttribute.HeaderStrength: player.HeaderStrength = value; break;
                case TrainableAttribute.Jumping: player.Jumping = value; break;
                case TrainableAttribute.Dribbling: player.Dribbling = value; break;
                case TrainableAttribute.LongShot: player.LongShotAccuracy = value; break;
                case TrainableAttribute.Finishing: player.Finishing = value; break;
                case TrainableAttribute.Positioning: player.Positioning = value; break;
            }
        }
    }
}
