using System.Runtime.CompilerServices;

// Lets DoomScroller.Tests.EditMode exercise internal-but-not-private helpers
// (WeaponGenerator.RollAbilities/RollPassives/SkillCountByRarity) directly, the same
// way WeaponUpgrader already does in production code.
[assembly: InternalsVisibleTo("DoomScroller.Tests.EditMode")]
