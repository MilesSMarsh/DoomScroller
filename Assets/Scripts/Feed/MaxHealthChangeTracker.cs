namespace DoomScroller.Feed
{
    // Decides whether feed odds need re-estimating: only when the HP a fight would start
    // with (PlayerHealth.CurrentMaxHealth) differs from the value at the last refresh.
    public class MaxHealthChangeTracker
    {
        private int _lastValue;
        private bool _hasValue;

        // True if value differs from the last one accepted (the first value always counts).
        public bool ShouldRefresh(int currentMaxHealth)
        {
            if (_hasValue && currentMaxHealth == _lastValue)
            {
                return false;
            }

            _lastValue = currentMaxHealth;
            _hasValue = true;
            return true;
        }
    }
}
