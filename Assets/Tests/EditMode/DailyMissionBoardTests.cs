using System;
using DoomScroller.Progression;
using NUnit.Framework;

namespace DoomScroller.Tests.EditMode
{
    public class DailyMissionBoardTests
    {
        private static readonly DateTime Day = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);

        private static void Complete(DailyMissionBoard board, int index)
        {
            DailyMission mission = board.GetMission(index);
            for (int i = 0; i < mission.Target; i++)
            {
                if (mission.Kind == DailyMissionKind.DefeatEnemies)
                {
                    board.RecordEnemyDefeated();
                }
                else
                {
                    board.RecordDungeonCleared();
                }
            }
        }

        [Test]
        public void SameDate_ProducesIdenticalMissions()
        {
            var a = new DailyMissionBoard(Day);
            var b = new DailyMissionBoard(Day.AddHours(13));

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.GetMission(i).Kind, b.GetMission(i).Kind);
                Assert.AreEqual(a.GetMission(i).Description, b.GetMission(i).Description);
                Assert.AreEqual(a.GetMission(i).Target, b.GetMission(i).Target);
                Assert.AreEqual(a.GetMission(i).GoldReward, b.GetMission(i).GoldReward);
            }
        }

        [Test]
        public void DifferentDates_CanProduceDifferentMissions()
        {
            var baseline = new DailyMissionBoard(Day);
            bool anyDifferent = false;

            for (int d = 1; d <= 30 && !anyDifferent; d++)
            {
                var other = new DailyMissionBoard(Day.AddDays(d));
                for (int i = 0; i < other.Count; i++)
                {
                    if (other.GetMission(i).Description != baseline.GetMission(i).Description
                        || other.GetMission(i).Kind != baseline.GetMission(i).Kind)
                    {
                        anyDifferent = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Board_HasExactlyThreeMissions()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(3, DailyMissionBoard.MissionCount);
            Assert.AreEqual(3, board.Count);
            Assert.IsNotNull(board.GetMission(0));
            Assert.IsNotNull(board.GetMission(2));
            Assert.IsNull(board.GetMission(3));
            Assert.IsNull(board.GetMission(-1));
        }

        [Test]
        public void RecordDungeonCleared_AdvancesOnlyDungeonMissions()
        {
            var board = new DailyMissionBoard(Day);

            board.RecordDungeonCleared();

            for (int i = 0; i < board.Count; i++)
            {
                int expected = board.GetMission(i).Kind == DailyMissionKind.ClearDungeon ? 1 : 0;
                Assert.AreEqual(expected, board.GetMission(i).Progress);
            }
        }

        [Test]
        public void RecordEnemyDefeated_AdvancesOnlyEnemyMissions()
        {
            var board = new DailyMissionBoard(Day);

            board.RecordEnemyDefeated();

            for (int i = 0; i < board.Count; i++)
            {
                int expected = board.GetMission(i).Kind == DailyMissionKind.DefeatEnemies ? 1 : 0;
                Assert.AreEqual(expected, board.GetMission(i).Progress);
            }
        }

        [Test]
        public void Progress_CapsAtTarget()
        {
            var board = new DailyMissionBoard(Day);
            DailyMission mission = board.GetMission(0);

            for (int i = 0; i < mission.Target + 10; i++)
            {
                board.RecordEnemyDefeated();
            }

            Assert.AreEqual(mission.Target, mission.Progress);
            Assert.IsTrue(mission.IsComplete);
        }

        [Test]
        public void Claim_BeforeComplete_ReturnsZero()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(0, board.Claim(0));
            Assert.IsFalse(board.GetMission(0).Claimed);
        }

        [Test]
        public void Claim_PaysOnce_WhenComplete()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);

            int first = board.Claim(0);
            int second = board.Claim(0);

            Assert.AreEqual(board.GetMission(0).GoldReward, first);
            Assert.Greater(first, 0);
            Assert.AreEqual(0, second);
            Assert.IsTrue(board.GetMission(0).Claimed);
        }

        [Test]
        public void Claim_InvalidIndex_ReturnsZero()
        {
            var board = new DailyMissionBoard(Day);

            Assert.AreEqual(0, board.Claim(99));
        }

        [Test]
        public void RefreshForDate_SameDay_DoesNothing()
        {
            var board = new DailyMissionBoard(Day);
            board.RecordEnemyDefeated();

            Assert.IsFalse(board.RefreshForDate(Day.AddHours(5)));
            Assert.AreEqual(1, board.GetMission(0).Progress);
        }

        [Test]
        public void RefreshForDate_NewDay_ResetsProgressAndClaims()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);
            board.Claim(0);
            int changedCount = 0;
            board.Changed += () => changedCount++;

            bool refreshed = board.RefreshForDate(Day.AddDays(1));

            Assert.IsTrue(refreshed);
            Assert.AreEqual(Day.AddDays(1).Date, board.Date);
            Assert.AreEqual(1, changedCount);
            for (int i = 0; i < board.Count; i++)
            {
                Assert.AreEqual(0, board.GetMission(i).Progress);
                Assert.IsFalse(board.GetMission(i).Claimed);
            }
        }

        [Test]
        public void CaptureAndLoadState_RoundTrips()
        {
            var board = new DailyMissionBoard(Day);
            Complete(board, 0);
            board.Claim(0);
            board.RecordDungeonCleared();

            DailyMissionState state = board.CaptureState();
            var restored = new DailyMissionBoard(Day);
            restored.LoadState(state);

            Assert.AreEqual("2026-03-14", state.date);
            for (int i = 0; i < board.Count; i++)
            {
                Assert.AreEqual(board.GetMission(i).Progress, restored.GetMission(i).Progress);
                Assert.AreEqual(board.GetMission(i).Claimed, restored.GetMission(i).Claimed);
            }
        }

        [Test]
        public void LoadState_StaleDate_IsIgnored()
        {
            var yesterday = new DailyMissionBoard(Day.AddDays(-1));
            Complete(yesterday, 0);
            DailyMissionState state = yesterday.CaptureState();

            var today = new DailyMissionBoard(Day);
            today.LoadState(state);

            for (int i = 0; i < today.Count; i++)
            {
                Assert.AreEqual(0, today.GetMission(i).Progress);
                Assert.IsFalse(today.GetMission(i).Claimed);
            }
        }

        [Test]
        public void LoadState_NullOrMalformed_LeavesBoardUntouched()
        {
            var board = new DailyMissionBoard(Day);
            board.RecordEnemyDefeated();

            board.LoadState(null);
            board.LoadState(new DailyMissionState { date = "2026-03-14" });

            Assert.AreEqual(1, board.GetMission(0).Progress);
        }
    }
}
