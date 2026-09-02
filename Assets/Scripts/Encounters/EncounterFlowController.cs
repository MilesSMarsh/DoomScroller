using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Coordinates the feed <-> encounter-detail <-> resolution screens. Tapping a card
    // enters it; committing resolves it. Dungeon-type cards resolve in DungeonView (a
    // multi-step, horizontally-paged run) instead of the plain single-fight
    // CombatResolutionView every other encounter type uses.
    public class EncounterFlowController : MonoBehaviour
    {
        [SerializeField] private FeedController feedController;
        [SerializeField] private ProfileController profileController;

        private EncounterDetailView _detailView;
        private CombatResolutionView _resolutionView;
        private DungeonView _dungeonView;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            _detailView = EncounterDetailView.Create(rect);
            _resolutionView = CombatResolutionView.Create(rect);
            _dungeonView = DungeonView.Create(rect);

            feedController.EncounterSelected += OnEncounterSelected;
            _detailView.BackRequested += OnBackToFeed;
            _detailView.Committed += OnCommitted;

            _resolutionView.ReturnToFeedRequested += OnBackToFeed;
            _resolutionView.EncounterWon += feedController.MarkWon;
            _resolutionView.EncounterLost += feedController.MarkLost;

            _dungeonView.ReturnToFeedRequested += OnBackToFeed;
            _dungeonView.EncounterWon += feedController.MarkWon;
            _dungeonView.EncounterLost += feedController.MarkLost;
            _dungeonView.EncounterAbandoned += feedController.MarkAbandoned;
        }

        private void OnEncounterSelected(EncounterData data)
        {
            feedController.gameObject.SetActive(false);
            _detailView.Show(data);
        }

        private void OnCommitted(EncounterData data)
        {
            _detailView.Hide();

            if (data.type == EncounterType.Dungeon)
            {
                _dungeonView.Show(data, profileController.Loadout, profileController.Progression, profileController.Health);
            }
            else
            {
                _resolutionView.Show(data, profileController.Loadout, profileController.Progression, profileController.Health);
            }
        }

        private void OnBackToFeed()
        {
            _detailView.Hide();
            _resolutionView.Hide();
            _dungeonView.Hide();
            feedController.gameObject.SetActive(true);
        }
    }
}
