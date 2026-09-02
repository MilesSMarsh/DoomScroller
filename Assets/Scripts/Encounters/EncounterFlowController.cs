using DoomScroller.Feed;
using DoomScroller.Inventory;
using UnityEngine;

namespace DoomScroller.Encounters
{
    // Coordinates the feed <-> encounter-detail <-> combat-resolution screens.
    // Tapping a card enters it; sideways swipe there picks a difficulty; committing
    // locks it in and resolves combat against the player's current loadout (Phase 5).
    public class EncounterFlowController : MonoBehaviour
    {
        [SerializeField] private FeedController feedController;
        [SerializeField] private ProfileController profileController;

        private EncounterDetailView _detailView;
        private CombatResolutionView _resolutionView;

        private void Awake()
        {
            var rect = (RectTransform)transform;
            _detailView = EncounterDetailView.Create(rect);
            _resolutionView = CombatResolutionView.Create(rect);

            feedController.EncounterSelected += OnEncounterSelected;
            _detailView.BackRequested += OnBackToFeed;
            _detailView.Committed += OnCommitted;
            _resolutionView.ReturnToFeedRequested += OnBackToFeed;
            _resolutionView.EncounterWon += feedController.MarkWon;
            _resolutionView.EncounterLost += feedController.MarkLost;
        }

        private void OnEncounterSelected(EncounterData data)
        {
            feedController.gameObject.SetActive(false);
            _detailView.Show(data);
        }

        private void OnCommitted(EncounterData data, EncounterVariant variant)
        {
            _detailView.Hide();
            _resolutionView.Show(data, variant, profileController.Loadout, profileController.Progression, profileController.Health);
        }

        private void OnBackToFeed()
        {
            _detailView.Hide();
            _resolutionView.Hide();
            feedController.gameObject.SetActive(true);
        }
    }
}
