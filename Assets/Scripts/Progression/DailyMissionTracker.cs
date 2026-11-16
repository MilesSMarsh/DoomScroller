using System;

namespace DoomScroller.Progression
{
    // Manual wiring for the agent-generated DailyMissionBoard (tools/gdd-agent): one board
    // for the current UTC day, shared by the fight/dungeon hooks and the Quest Board.
    // Session-only for now: progress isn't in SaveData yet (the board already has
    // CaptureState/LoadState for that), and the day comes from the device clock until the
    // server clock is wired in.
    public static class DailyMissionTracker
    {
        private static DailyMissionBoard _board;

        public static DailyMissionBoard Board
        {
            get
            {
                if (_board == null)
                {
                    _board = new DailyMissionBoard(DateTime.UtcNow);
                }
                else
                {
                    _board.RefreshForDate(DateTime.UtcNow);
                }

                return _board;
            }
        }
    }
}
