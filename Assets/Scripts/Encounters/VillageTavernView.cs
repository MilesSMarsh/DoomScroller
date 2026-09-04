using DoomScroller.Inventory;
using DoomScroller.Progression;
using DoomScroller.Social;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Encounters
{
    // One establishment in the Village (see VillageView): the old standalone Tavern's
    // chat board, plus a potion refill that now costs gold — tops every potion type the
    // player started with back up to its refill capacity, one at a time, as far as the
    // wallet can afford (see PlayerLoadout.RefillPotions). A Revive potion the Alchemist
    // has upgraded costs more per unit to refill (PricePerPotion) — the button always
    // states one flat total, not a per-unit price.
    public class VillageTavernView : MonoBehaviour
    {
        private const int BaseRefillPrice = 8;
        private const int PricePerQualityLevel = 4;

        private MessageBoardView _messageBoard;
        private Text _refillButtonLabel;
        private Text _refillResultText;
        private PlayerLoadout _loadout;
        private PlayerWallet _wallet;

        public static VillageTavernView Create(Transform parent)
        {
            var go = new GameObject("Tavern", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var view = go.AddComponent<VillageTavernView>();

            var subtitle = CreateLabel(go.transform, "Subtitle", "Trade tips, talk gear, no combat here.", 28, FontStyle.Italic);
            subtitle.color = new Color(1f, 1f, 1f, 0.6f);
            SetFixedHeight(subtitle.GetComponent<LayoutElement>(), 48);

            var refillButtonGo = CreateButton(go.transform, "RefillButton", "Refill Potions", () => view.OnRefillClicked());
            SetFixedHeight(refillButtonGo.GetComponent<LayoutElement>(), 100);
            view._refillButtonLabel = refillButtonGo.GetComponentInChildren<Text>();

            view._refillResultText = CreateLabel(go.transform, "RefillResult", "", 24, FontStyle.Italic);
            view._refillResultText.color = new Color(0.6f, 0.9f, 1f, 0.95f);
            SetFixedHeight(view._refillResultText.GetComponent<LayoutElement>(), 40);

            view._messageBoard = MessageBoardView.Create(go.transform);

            return view;
        }

        // Called once per Village visit (see VillageView.Show) — same granularity the
        // standalone Tavern used to refresh its chatter at. loadout is one long-lived
        // instance for the whole session, so subscribing to its Changed event exactly
        // once (guarded below) keeps the button's total correct even if the player
        // upgrades a flask at the Alchemist earlier in the same visit, without needing
        // to leave and re-enter this page.
        public void Show(PlayerLoadout loadout, PlayerWallet wallet)
        {
            if (_loadout == null)
            {
                loadout.Changed += RefreshRefillButton;
            }

            _loadout = loadout;
            _wallet = wallet;
            _refillResultText.text = "";
            _messageBoard.PopulateWithEntries(MessageBoardGenerator.GenerateTavernChatter());
            RefreshRefillButton();
        }

        // A Revive potion the Alchemist has upgraded costs more per unit to refill;
        // every other potion type stays at the flat base price.
        private int PricePerPotion(ItemData potionType)
        {
            return potionType.potionEffect == PotionEffect.Revive
                ? BaseRefillPrice + _loadout.GetReviveQualityLevel(potionType) * PricePerQualityLevel
                : BaseRefillPrice;
        }

        private void RefreshRefillButton()
        {
            var cost = _loadout.ComputeRefillCost(PricePerPotion);
            _refillButtonLabel.text = cost > 0 ? $"Refill Potions ({cost}g)" : "Fully Stocked";
        }

        private void OnRefillClicked()
        {
            if (_loadout.ArePotionsFullyStocked())
            {
                _refillResultText.text = "Already fully stocked.";
                return;
            }

            var added = _loadout.RefillPotions(_wallet, PricePerPotion);
            _refillResultText.text = added > 0
                ? $"Refilled {added} potion{(added == 1 ? "" : "s")}."
                : "Not enough gold to refill any potions.";
            RefreshRefillButton();
        }

        private static void SetFixedHeight(LayoutElement layoutElement, float height)
        {
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
        }

        private static Text CreateLabel(Transform parent, string name, string content, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            go.AddComponent<LayoutElement>();

            return text;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.15f);

            go.AddComponent<LayoutElement>();

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 32;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;

            return go;
        }
    }
}
