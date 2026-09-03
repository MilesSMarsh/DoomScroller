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
    // Tavern in TavernView (no combat, never locks), everything else in the plain
    // single-fight CombatResolutionView.
    public class EncounterFlowController : MonoBehaviour
    {
        [SerializeField] private FeedController feedController;
        [SerializeField] private ProfileController profileController;

        private CombatResolutionView _resolutionView;
        private DungeonView _dungeonView;
        private TavernView _tavernView;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            _resolutionView = CombatResolutionView.Create(rect);
            _dungeonView = DungeonView.Create(rect);
            _tavernView = TavernView.Create(rect);

            feedController.EncounterSelected += OnEncounterSelected;

            _resolutionView.ReturnToFeedRequested += OnBackToFeed;
            _resolutionView.EncounterWon += feedController.MarkWon;
            _resolutionView.EncounterLost += feedController.MarkLost;

            _dungeonView.ReturnToFeedRequested += OnBackToFeed;
            _dungeonView.EncounterWon += feedController.MarkWon;
            _dungeonView.EncounterLost += feedController.MarkLost;
            _dungeonView.EncounterAbandoned += feedController.MarkAbandoned;

            // Tavern never fires Won/Lost/Abandoned — it's never locked, and there's
            // nothing to mark it with.
            _tavernView.ReturnToFeedRequested += OnBackToFeed;
        }

        private void OnEncounterSelected(EncounterData data)
        {
            feedController.gameObject.SetActive(false);

            if (data.type == EncounterType.Dungeon)
            {
                _dungeonView.Show(data, profileController.Loadout, profileController.Progression,
                    profileController.Health, profileController.Stats, profileController.Settings);
            }
            else if (data.type == EncounterType.Tavern)
            {
                _tavernView.Show(data);
            }
            else
            {
                _resolutionView.Show(data, profileController.Loadout, profileController.Progression,
                    profileController.Health, profileController.Stats, profileController.Settings);
            }
        }

        private void OnBackToFeed()
        {
            _resolutionView.Hide();
            _dungeonView.Hide();
            _tavernView.Hide();
            feedController.gameObject.SetActive(true);
        }
    }
}
