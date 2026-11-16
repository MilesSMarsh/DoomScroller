using System;
using System.Threading.Tasks;
using DoomScroller.Network;

namespace DoomScroller.Inventory
{
    public enum ProfileLoadOutcome { Failed, New, Existing }

    // Plain C# load/save protocol between the game and the server profile. Debounced
    // saves: RequestSave marks dirty, Pump (called each frame) sends once the debounce
    // elapsed and nothing is in flight; changes during a save queue exactly one more.
    // Any failure halts syncing and raises ConnectionLost — it never starts a fresh
    // profile over the server's.
    public sealed class ProfileSync
    {
        private readonly IServerClient _client;
        private readonly Func<string> _captureJson;
        private readonly Func<double> _clock;
        private readonly double _debounceSeconds;

        private bool _dirty;
        private bool _inFlight;
        private bool _flushPending;
        private bool _halted;
        private double _dirtySince;
        private int _revision;

        public bool IsLoaded { get; private set; }
        public ProfileResponse LoadedProfile { get; private set; }
        public int SaveCount { get; private set; }

        public event Action ConnectionLost;

        // Raised after a successful save (isReload false: only server-owned fields such
        // as current max HP need applying) or after a 409 forced a reload (isReload
        // true: the whole profile changed server-side and must be applied).
        public event Action<ProfileResponse, bool> ServerProfileApplied;

        public ProfileSync(IServerClient client, Func<string> captureJson, Func<double> clock, double debounceSeconds = 1.0)
        {
            _client = client;
            _captureJson = captureJson;
            _clock = clock;
            _debounceSeconds = debounceSeconds;
        }

        public async Task<ProfileLoadOutcome> LoadAsync()
        {
            try
            {
                if (!_client.IsSignedIn && !await _client.SignInAsync())
                {
                    return Fail();
                }

                var response = await _client.LoadProfileAsync();
                if (!response.Ok || (!response.IsNew && string.IsNullOrEmpty(response.Json)))
                {
                    return Fail();
                }

                _revision = response.Revision;
                LoadedProfile = response;
                IsLoaded = true;
                _halted = false;
                return response.IsNew ? ProfileLoadOutcome.New : ProfileLoadOutcome.Existing;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[ProfileSync] Load failed: " + e);
                return Fail();
            }
        }

        public void RequestSave()
        {
            if (!IsLoaded || _halted)
            {
                return;
            }

            if (!_dirty)
            {
                _dirty = true;
                _dirtySince = _clock();
            }
        }

        public void Pump()
        {
            if (_dirty && !_inFlight && IsLoaded && !_halted && _clock() - _dirtySince >= _debounceSeconds)
            {
                _ = SaveAsync();
            }
        }

        // Skips the debounce (app pause/quit, first save of a new profile).
        public void Flush()
        {
            if (!_dirty || !IsLoaded || _halted)
            {
                return;
            }

            if (_inFlight)
            {
                _flushPending = true;
                return;
            }

            _ = SaveAsync();
        }

        private async Task SaveAsync()
        {
            _inFlight = true;
            _dirty = false;
            try
            {
                var json = _captureJson();
                var response = await _client.SaveProfileAsync(json, _revision);
                SaveCount++;
                if (response.Ok)
                {
                    _revision = response.Revision;
                    ServerProfileApplied?.Invoke(response, false);
                }
                else if (response.Conflict)
                {
                    // The server changed the profile (a fight): take its version, never overwrite.
                    var reloaded = await _client.LoadProfileAsync();
                    if (!reloaded.Ok || string.IsNullOrEmpty(reloaded.Json))
                    {
                        _dirty = true;
                        Fail();
                        return;
                    }

                    _revision = reloaded.Revision;
                    _dirty = false;
                    ServerProfileApplied?.Invoke(reloaded, true);
                }
                else
                {
                    _dirty = true;
                    Fail();
                    return;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[ProfileSync] Save failed: " + e);
                _dirty = true;
                Fail();
                return;
            }
            finally
            {
                _inFlight = false;
            }

            if (_flushPending)
            {
                _flushPending = false;
                Flush();
            }
        }

        private ProfileLoadOutcome Fail()
        {
            _halted = true;
            UnityEngine.Debug.LogWarning("[ProfileSync] Connection lost.");
            ConnectionLost?.Invoke();
            return ProfileLoadOutcome.Failed;
        }

        // For the connection-lost screen (014): try the whole startup again.
        public void Reset()
        {
            _halted = false;
            IsLoaded = false;
            _dirty = false;
            _flushPending = false;
        }
    }
}
