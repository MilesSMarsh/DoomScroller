using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Coordinates the feed <-> encounter screens. A tap on a feed card (or its own
    // action button — see FeedCardView) commits straight into the encounter; there's no
    // separate preview/confirm step any more (that was EncounterDetailView, removed —
    // the card itself, plus its per-type action button, is the entire "are you sure").
    // Dungeon-type cards resolve in DungeonView (a multi-step, horizontally-paged run),
    // Tavern in VillageView (a swipeable hub of establishments — no combat, never
    // locks), everything else in the plain single-fight CombatResolutionView.
    public class EncounterFlowController : MonoBehaviour
    {
        [SerializeField] private FeedController feedController;
        [SerializeField] private ProfileController profileController;

        private CombatResolutionView _resolutionView;
        private DungeonView _dungeonView;
        private VillageView _villageView;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            _resolutionView = CombatResolutionView.Create(rect);
            _dungeonView = DungeonView.Create(rect);
            _villageView = VillageView.Create(rect);

            feedController.EncounterSelected += OnEncounterSelected;

            _resolutionView.ReturnToFeedRequested += OnBackToFeed;
            _resolutionView.EncounterWon += feedController.MarkWon;
            _resolutionView.EncounterLost += feedController.MarkLost;

            _dungeonView.ReturnToFeedRequested += OnBackToFeed;
            _dungeonView.EncounterWon += feedController.MarkWon;
            _dungeonView.EncounterLost += feedController.MarkLost;
            _dungeonView.EncounterAbandoned += feedController.MarkAbandoned;

            // The Village never fires Won/Lost/Abandoned — it's never locked, and
            // there's nothing to mark it with.
            _villageView.ReturnToFeedRequested += OnBackToFeed;
        }

        private void OnEncounterSelected(EncounterData data)
        {
            feedController.gameObject.SetActive(false);

            if (data.type == EncounterType.Dungeon)
            {
                _dungeonView.Show(data, profileController.Loadout, profileController.Progression,
                    profileController.Health, profileController.Stats, profileController.Settings, profileController.Wallet);
            }
            else if (data.type == EncounterType.Tavern)
            {
                _villageView.Show(data, profileController.Loadout, profileController.Health, profileController.Wallet);
            }
            else
            {
                _resolutionView.Show(data, profileController.Loadout, profileController.Progression,
                    profileController.Health, profileController.Stats, profileController.Settings, profileController.Wallet);
            }
        }

        private void OnBackToFeed()
        {
            _resolutionView.Hide();
            _dungeonView.Hide();
            _villageView.Hide();
            feedController.gameObject.SetActive(true);
        }
    }
}
