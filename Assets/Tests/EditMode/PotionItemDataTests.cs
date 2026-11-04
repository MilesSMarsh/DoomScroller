using System.IO;
using System.Linq;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DoomScroller.Tests.EditMode
{
    // Task 005: the two potion ItemData assets under Resources/Items and the starting
    // potion loadout built from them.
    public class PotionItemDataTests
    {
        private const int ExpectedReviveAmount = 30;

        private static ItemData[] LoadItems() => Resources.LoadAll<ItemData>("Items");

        private static ItemData LoadRevive() =>
            LoadItems().FirstOrDefault(i => i.potionEffect == PotionEffect.Revive);

        private static ItemData LoadRestoration() =>
            LoadItems().FirstOrDefault(i => i.potionEffect == PotionEffect.MaxHealthRestore);

        // Mirrors the 3 Revive + 3 Restoration starting inventory set on ProfileController
        // in SampleScene.
        private static PlayerLoadout MakeStartingLoadout()
        {
            var revive = LoadRevive();
            var restoration = LoadRestoration();
            var starting = new[] { revive, revive, revive, restoration, restoration, restoration };
            return new PlayerLoadout(starting);
        }

        private static bool IsRevive(ItemData i) => i.potionEffect == PotionEffect.Revive;
        private static bool IsRestoration(ItemData i) => i.potionEffect == PotionEffect.MaxHealthRestore;

        [Test]
        public void ItemsFolder_ContainsExactlyOneRevive_AndOneRestoration()
        {
            var items = LoadItems();

            Assert.AreEqual(2, items.Length, "Resources/Items should hold exactly the two potion assets.");
            Assert.AreEqual(1, items.Count(i => i.potionEffect == PotionEffect.Revive));
            Assert.AreEqual(1, items.Count(i => i.potionEffect == PotionEffect.MaxHealthRestore));
            Assert.IsTrue(items.All(i => i.category == ItemCategory.Potion));
        }

        [Test]
        public void PotionItemIds_AreNonEmptyAndDistinct()
        {
            var revive = LoadRevive();
            var restoration = LoadRestoration();

            Assert.IsFalse(string.IsNullOrEmpty(revive.itemId));
            Assert.IsFalse(string.IsNullOrEmpty(restoration.itemId));
            Assert.AreNotEqual(revive.itemId, restoration.itemId);
        }

        [Test]
        public void ReviveAsset_MatchesSpec()
        {
            var revive = LoadRevive();

            Assert.AreEqual("potion_revive", revive.itemId);
            Assert.AreEqual("Revive Draught", revive.title);
            Assert.AreEqual(ItemCategory.Potion, revive.category);
            Assert.AreEqual(PotionEffect.Revive, revive.potionEffect);
            Assert.AreEqual(ItemRarity.Common, revive.rarity);
            Assert.IsNull(revive.icon, "Potions ship as placeholders with no icon.");
            Assert.GreaterOrEqual(revive.goldValue, 0);
        }

        [Test]
        public void ReviveAsset_PotionAmountIs30()
        {
            var revive = LoadRevive();

            Assert.AreEqual(ExpectedReviveAmount, revive.potionAmount);
            Assert.AreEqual(ExpectedReviveAmount, new PlayerLoadout(new[] { revive }).GetRevivePotionAmount(revive),
                "No quality upgrades owned, so the effective amount is the authored 30%.");
        }

        [Test]
        public void RestorationAsset_MatchesSpec()
        {
            var restoration = LoadRestoration();

            Assert.AreEqual("potion_restoration", restoration.itemId);
            Assert.AreEqual("Restoration Potion", restoration.title);
            Assert.AreEqual(ItemCategory.Potion, restoration.category);
            Assert.AreEqual(PotionEffect.MaxHealthRestore, restoration.potionEffect);
            Assert.AreEqual(ItemRarity.Common, restoration.rarity);
            Assert.IsNull(restoration.icon, "Potions ship as placeholders with no icon.");
            Assert.Greater(restoration.potionAmount, 0, "Restoration must restore some HP.");
            Assert.GreaterOrEqual(restoration.goldValue, 0);
        }

        [Test]
        public void PotionAssets_HaveCommittedMetaFiles()
        {
            var folder = Path.Combine(Application.dataPath, "Resources", "Items");

            foreach (var name in new[] { "Potion_Revive", "Potion_Restoration" })
            {
                var assetPath = Path.Combine(folder, name + ".asset");
                var metaPath = assetPath + ".meta";

                Assert.IsTrue(File.Exists(assetPath), $"Missing asset {name}.asset");
                Assert.IsTrue(File.Exists(metaPath), $"Missing {name}.asset.meta");
                StringAssert.Contains("guid: ", File.ReadAllText(metaPath));
            }
        }

        [Test]
        public void StartingLoadout_KnowsReviveType_AndRefillTopsUpToCapacity()
        {
            var loadout = new PlayerLoadout(new[] { LoadRevive() });
            var wallet = new PlayerWallet(1000);

            Assert.IsNotNull(loadout.GetKnownRevivePotionType());

            var bought = loadout.RefillPotions(wallet, _ => 10, IsRevive);

            var revive = LoadRevive();
            Assert.AreEqual(2, bought, "Refill should buy up to capacity from one owned.");
            Assert.AreEqual(loadout.GetRefillCapacity(revive), loadout.GetPotions().Count(IsRevive));
            Assert.AreEqual(3, loadout.GetPotions().Count(IsRevive));
            Assert.AreEqual(1000 - 20, wallet.Gold);
        }

        [Test]
        public void StartingLoadout_ReviveTypeStaysKnown_AfterAllPotionsAreSpent()
        {
            var loadout = MakeStartingLoadout();
            var revive = LoadRevive();

            Assert.AreEqual(3, loadout.GetPotions().Count(IsRevive));
            for (var i = 0; i < 3; i++)
            {
                loadout.ConsumeRevivePotion(loadout.GetBestRevivePotion());
            }

            Assert.AreEqual(0, loadout.GetPotions().Count(IsRevive));
            Assert.AreSame(revive, loadout.GetKnownRevivePotionType(),
                "A consumed Revive type must still be known so the Tavern can refill it.");
        }

        [Test]
        public void StartingLoadout_RestorationTypeIsKnown_ViaRefillCostCheck()
        {
            var loadout = MakeStartingLoadout();

            Assert.AreEqual(3, loadout.GetPotions().Count(IsRestoration));
            Assert.IsTrue(loadout.ArePotionsFullyStocked(IsRestoration));

            // Spend all three. A type that isn't known would contribute nothing to the
            // refill cost; a known one owes its full capacity (3 * 10 at price 10).
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(loadout.RemoveFromInventory(LoadRestoration()));
            }

            Assert.AreEqual(0, loadout.GetPotions().Count(IsRestoration));
            Assert.AreEqual(30, loadout.ComputeRefillCost(_ => 10, IsRestoration));
        }

        [Test]
        public void StartingLoadout_FullyStocked_CostsNothingToRefill()
        {
            var loadout = MakeStartingLoadout();

            Assert.IsTrue(loadout.ArePotionsFullyStocked(IsRevive));
            Assert.AreEqual(0, loadout.ComputeRefillCost(_ => 10, IsRevive));
            Assert.AreEqual(0, loadout.RefillPotions(new PlayerWallet(500), _ => 10, IsRevive));
        }

        [Test]
        public void Refill_WithTooLittleGold_BuysOnlyWhatIsAffordable()
        {
            var loadout = new PlayerLoadout(new[] { LoadRevive() });
            var wallet = new PlayerWallet(25);

            var bought = loadout.RefillPotions(wallet, _ => 10, IsRevive);

            Assert.AreEqual(2, bought, "25 gold at 10 each buys two of the two missing.");
            Assert.AreEqual(5, wallet.Gold);
            Assert.AreEqual(3, loadout.GetPotions().Count(IsRevive));
        }

        [Test]
        public void ReviveAndRestoration_RoundTripThroughSnapshot_RelinkToSameAsset()
        {
            var revive = LoadRevive();
            var restoration = LoadRestoration();

            Assert.AreSame(revive, ItemSnapshot.Capture(revive).Restore());
            Assert.AreSame(restoration, ItemSnapshot.Capture(restoration).Restore());
        }
    }
}
