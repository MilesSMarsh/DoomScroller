using System.Collections.Generic;
using DoomScroller.Feed;

namespace DoomScroller.Social
{
    // Plain C# so comment generation is testable without a running scene. Produces a
    // handful of canned NPC reactions to flesh out the stub board before the player
    // posts their own reply.
    public static class MessageBoardGenerator
    {
        private static readonly string[] Authors =
        {
            "xXShadowSlayerXx", "casual_gamer42", "LootGoblin", "npc_dave", "feed_lurker99", "TankMain"
        };

        private static readonly string[] VictoryComments =
        {
            "clean kill, gg", "that gear carried lol", "W", "grinding this feed today", "run it back harder next time"
        };

        private static readonly string[] DefeatComments =
        {
            "rip, gear check", "L take but respect the attempt", "this one's brutal ngl", "upgrade before you try that again"
        };

        // Generic chatter for TavernView — not tied to any specific encounter's outcome,
        // unlike Generate below. Minimal for now; the Tavern itself is expected to grow
        // beyond this stub later.
        private static readonly string[] TavernChatter =
        {
            "anyone else grinding the depths today?", "new gear drop rates feel decent ngl",
            "tips for the dungeon door? that third room wrecks me", "just vibing between runs",
            "gg on that last treasure room", "anyone got a spare talisman to trade",
            "which weapon ability hits hardest for you all"
        };

        public static IReadOnlyList<MessageBoardEntry> Generate(EncounterData data, bool victory, System.Random rng = null)
        {
            rng ??= new System.Random();
            var pool = victory ? VictoryComments : DefeatComments;
            var count = rng.Next(2, 4);

            var entries = new List<MessageBoardEntry>(count + 1)
            {
                new(Authors[rng.Next(Authors.Length)],
                    victory ? $"finally beat {data.title}" : $"{data.title} wrecked me")
            };

            for (var i = 0; i < count; i++)
            {
                entries.Add(new MessageBoardEntry(Authors[rng.Next(Authors.Length)], pool[rng.Next(pool.Length)]));
            }

            return entries;
        }

        public static IReadOnlyList<MessageBoardEntry> GenerateTavernChatter(System.Random rng = null)
        {
            rng ??= new System.Random();
            var count = rng.Next(3, 6);

            var entries = new List<MessageBoardEntry>(count);
            for (var i = 0; i < count; i++)
            {
                entries.Add(new MessageBoardEntry(Authors[rng.Next(Authors.Length)], TavernChatter[rng.Next(TavernChatter.Length)]));
            }

            return entries;
        }
    }
}
