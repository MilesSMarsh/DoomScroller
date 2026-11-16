using System;
using System.Globalization;

namespace DoomScroller.Progression
{
    public enum DailyMissionKind
    {
        DefeatEnemies,
        ClearDungeon
    }

    // One daily goal. Plain C# so it's testable without a scene.
    public class DailyMission
    {
        public DailyMissionKind Kind { get; }
        public string Description { get; }
        public int Target { get; }
        public int Progress { get; internal set; }
        public int GoldReward { get; }
        public bool Claimed { get; internal set; }

        public bool IsComplete => Progress >= Target;

        public DailyMission(DailyMissionKind kind, string description, int target, int goldReward)
        {
            Kind = kind;
            Description = description;
            Target = Math.Max(1, target);
            GoldReward = Math.Max(0, goldReward);
        }
    }

    // Save-friendly snapshot of the board. Lives in SaveData later.
    [Serializable]
    public class DailyMissionState
    {
        public string date;
        public int[] progress;
        public bool[] claimed;
    }

    // Three daily missions, seeded from the UTC calendar date so every device
    // rolls the same set for the same day.
    public class DailyMissionBoard
    {
        public const int MissionCount = 3;
        private const string DateFormat = "yyyy-MM-dd";

        private readonly DailyMission[] _missions = new DailyMission[MissionCount];

        public DateTime Date { get; private set; }
        public int Count => _missions.Length;

        public event Action Changed;

        public DailyMissionBoard(DateTime utcDate)
        {
            Roll(utcDate.Date);
        }

        public DailyMission GetMission(int index)
        {
            if (index < 0 || index >= _missions.Length)
            {
                return null;
            }

            return _missions[index];
        }

        public void RecordEnemyDefeated()
        {
            Advance(DailyMissionKind.DefeatEnemies);
        }

        public void RecordDungeonCleared()
        {
            Advance(DailyMissionKind.ClearDungeon);
        }

        // Returns the gold reward exactly once for a completed mission; 0 otherwise.
        public int Claim(int index)
        {
            DailyMission mission = GetMission(index);
            if (mission == null || mission.Claimed || !mission.IsComplete)
            {
                return 0;
            }

            mission.Claimed = true;
            Changed?.Invoke();
            return mission.GoldReward;
        }

        // Re-rolls and resets progress if the UTC day changed. Returns true if it did.
        public bool RefreshForDate(DateTime utcDate)
        {
            DateTime day = utcDate.Date;
            if (day == Date)
            {
                return false;
            }

            Roll(day);
            Changed?.Invoke();
            return true;
        }

        public DailyMissionState CaptureState()
        {
            var state = new DailyMissionState
            {
                date = Date.ToString(DateFormat, CultureInfo.InvariantCulture),
                progress = new int[_missions.Length],
                claimed = new bool[_missions.Length]
            };

            for (int i = 0; i < _missions.Length; i++)
            {
                state.progress[i] = _missions[i].Progress;
                state.claimed[i] = _missions[i].Claimed;
            }

            return state;
        }

        // Restores progress only if the saved date matches the board's date;
        // a stale or malformed state leaves the fresh board untouched.
        // Doesn't fire Changed — called once, before anything subscribes.
        public void LoadState(DailyMissionState state)
        {
            if (state == null || state.progress == null || state.claimed == null)
            {
                return;
            }

            if (!DateTime.TryParseExact(state.date, DateFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime saved) || saved.Date != Date)
            {
                return;
            }

            for (int i = 0; i < _missions.Length; i++)
            {
                int progress = i < state.progress.Length ? state.progress[i] : 0;
                bool claimed = i < state.claimed.Length && state.claimed[i];
                _missions[i].Progress = Math.Max(0, Math.Min(progress, _missions[i].Target));
                _missions[i].Claimed = claimed && _missions[i].IsComplete;
            }
        }

        private void Advance(DailyMissionKind kind)
        {
            bool changed = false;
            for (int i = 0; i < _missions.Length; i++)
            {
                DailyMission mission = _missions[i];
                if (mission.Kind != kind || mission.Claimed || mission.IsComplete)
                {
                    continue;
                }

                mission.Progress++;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void Roll(DateTime day)
        {
            Date = day;
            int seed = day.Year * 10000 + day.Month * 100 + day.Day;
            var rng = new Random(seed);

            for (int i = 0; i < _missions.Length; i++)
            {
                DailyMissionKind kind;
                if (i == 0)
                {
                    kind = DailyMissionKind.DefeatEnemies;
                }
                else if (i == 1)
                {
                    kind = DailyMissionKind.ClearDungeon;
                }
                else
                {
                    kind = rng.Next(2) == 0 ? DailyMissionKind.DefeatEnemies : DailyMissionKind.ClearDungeon;
                }

                _missions[i] = CreateMission(kind, rng);
            }
        }

        private static DailyMission CreateMission(DailyMissionKind kind, Random rng)
        {
            if (kind == DailyMissionKind.ClearDungeon)
            {
                int target = rng.Next(1, 3);
                string text = target == 1 ? "Clear a dungeon" : "Clear " + target + " dungeons";
                return new DailyMission(kind, text, target, 75 * target);
            }

            int enemies = 5 * rng.Next(1, 5);
            return new DailyMission(kind, "Defeat " + enemies + " enemies", enemies, 8 * enemies);
        }
    }
}
