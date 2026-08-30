using System.Collections.ObjectModel;
using RetroFootballManager.Common;
using RetroFootballManager.Helper;
using RetroFootballManager.Models;

namespace RetroFootballManager.ViewModels
{
    // Shared ticker-feed logic for the three live-match ViewModels (league/cup/friendly),
    // which otherwise each duplicated this verbatim. Besides the plain event-to-TickerEntry
    // mapping, this stages a multi-step dramatic reveal (with real-time delays) for penalties
    // and direct free kicks instead of dumping the award and outcome into the ticker in the
    // same instant - the award/outcome events are already fully resolved by Match's
    // synchronous simulation, this only paces *when* they become visible.
    public static class LiveMatchTicker
    {
        public static string IconFor(GameEventType type) => type switch
        {
            GameEventType.Goal => "ball.png",
            GameEventType.YellowCard => "yellowcard.png",
            GameEventType.RedCard => "redcard.png",
            GameEventType.Substitution => "substitution.png",
            _ => "",
        };

        // Drains newly-simulated events (from `cursor` onward) into `ticker`. `onEvent` lets
        // the caller apply its own side effects (cards list, scorer tracking, red-card/injury
        // pause flags) for every event, including ones consumed inside a staged sequence below.
        public static async Task<int> ProcessNewEventsAsync(
            IReadOnlyList<MatchEvent> events, int cursor, ObservableCollection<TickerEntry> ticker,
            Action<MatchEvent> onEvent)
        {
            int i = cursor;
            while (i < events.Count)
            {
                var e = events[i];
                i++;
                onEvent(e);

                if (e.Type is GameEventType.Shot or GameEventType.DangerousAttack)
                    continue; // keep the ticker focused on notable moments

                bool isTeamEvent = e.Type is not (GameEventType.KickOff or GameEventType.HalfTime or GameEventType.FullTime);
                Insert(ticker, e, isTeamEvent);

                if (e.Type == GameEventType.Penalty)
                    i = await RevealPenaltySequenceAsync(events, i, e, ticker, onEvent);
                else if (e.Type == GameEventType.FreeKick)
                    i = await RevealFreeKickSequenceAsync(events, i, e, ticker, onEvent);
            }

            return i;
        }

        private static async Task<int> RevealPenaltySequenceAsync(
            IReadOnlyList<MatchEvent> events, int i, MatchEvent award, ObservableCollection<TickerEntry> ticker,
            Action<MatchEvent> onEvent)
        {
            await Task.Delay(1100);
            InsertSynthetic(ticker, award.Minute, EventTextHelper.PenaltyReadyText(Random.Shared), award.IsHomeTeam);
            await Task.Delay(1300);
            InsertSynthetic(ticker, award.Minute, EventTextHelper.PenaltyRunUpText(Random.Shared), award.IsHomeTeam);
            await Task.Delay(900);

            // The taker's outcome (Goal/Save/missed-Shot) always follows immediately, possibly
            // preceded by a card for the fouler - skip through that, then reveal the payoff.
            while (i < events.Count && events[i].Minute == award.Minute
                && events[i].Type is GameEventType.YellowCard or GameEventType.RedCard
                    or GameEventType.Goal or GameEventType.Save or GameEventType.Shot)
            {
                var outcome = events[i];
                i++;
                onEvent(outcome);

                if (outcome.Type is GameEventType.Goal or GameEventType.Save or GameEventType.Shot)
                {
                    Insert(ticker, outcome, isTeamEvent: true);
                    break;
                }
            }

            return i;
        }

        private static async Task<int> RevealFreeKickSequenceAsync(
            IReadOnlyList<MatchEvent> events, int i, MatchEvent award, ObservableCollection<TickerEntry> ticker,
            Action<MatchEvent> onEvent)
        {
            await Task.Delay(1100);
            InsertSynthetic(ticker, award.Minute, EventTextHelper.FreeKickRunUpText(Random.Shared), award.IsHomeTeam);
            await Task.Delay(900);

            // A successful free kick emits ShotOnTarget then Goal; a failed one emits only
            // Save - skip the intermediate ShotOnTarget silently and reveal the final outcome.
            while (i < events.Count && events[i].Minute == award.Minute
                && events[i].Type is GameEventType.Goal or GameEventType.Save or GameEventType.ShotOnTarget)
            {
                var outcome = events[i];
                i++;
                onEvent(outcome);

                if (outcome.Type is GameEventType.Goal or GameEventType.Save)
                {
                    Insert(ticker, outcome, isTeamEvent: true);
                    break;
                }
            }

            return i;
        }

        private static void Insert(ObservableCollection<TickerEntry> ticker, MatchEvent e, bool isTeamEvent) =>
            ticker.Insert(0, new TickerEntry(e.Minute, IconFor(e.Type), e.Player?.Name, e.Description, e.IsHomeTeam, isTeamEvent));

        private static void InsertSynthetic(ObservableCollection<TickerEntry> ticker, int minute, string text, bool isHomeTeam) =>
            ticker.Insert(0, new TickerEntry(minute, "", null, text, isHomeTeam, true));
    }
}
