using System.Collections;
using System.Collections.Generic;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Combat
{
    // Shared visual fight player, hosted by both CombatResolutionView and
    // DungeonStepPageView. Drives a CombatSimulator batch-by-batch (RunUntilDeathOrVictory
    // at a time) and animates each beat: the attacker wiggles/lunges, a projectile flies
    // for ranged beats (every ability cast, or a ranged weapon's basic attack), the
    // defender flashes and a damage number (or "MISS") pops up. If the player dies and
    // has a revive potion carried, the sequence pauses for a Yes/No prompt (or consumes
    // it automatically, per GameSettings) and the same fight continues. Active abilities
    // get their own icon-and-cooldown-bar widget near the player: in auto mode the bar
    // just visualizes the sim's own timer, in manual mode (GameSettings.
    // ManualAbilityCasting) it's a real button that fires the ability on tap. No text-log
    // narration, and PlayerHealth is never touched here — see CombatSimulator.
    public class CombatPlaybackView : MonoBehaviour
    {
        private const float PortraitSize = 280f;
        private const float GearSquareSize = 52f;
        private const float AbilityIconSize = 64f;
        private const float AbilityCooldownBarHeight = 10f;
        private const float AbilityWidgetSpacing = 4f;
        private const float AbilityWidgetHeight = AbilityIconSize + AbilityWidgetSpacing + AbilityCooldownBarHeight;
        private const float BeatPause = 0.35f;
        private const float LungeDuration = 0.22f;
        private const float LungeDistance = 50f;
        private const float ProjectileDuration = 0.2f;
        private const float ProjectileSize = 34f;
        private const float AbilityProjectileSize = 60f;
        private const float DotProjectileSize = 20f;

        // Sickly green — the common "poison/bleed" read — so a DoT tick's projectile
        // and damage number both visually say "ongoing effect chipping away," distinct
        // from either a real swing or a fresh ability cast.
        private static readonly Color DotTickColor = new(0.55f, 0.85f, 0.35f, 1f);
        private const float FlashDuration = 0.15f;
        private const float DamageNumberDuration = 0.6f;
        private const float DamageNumberRise = 90f;
        private const float HealthBarHeight = 40f;
        private const float HealthBarOutlineWidth = 3f;
        private const float HealthBarTweenDuration = 0.3f;

        // A win that ended below this fraction of this fight's own starting HP reads as
        // a "close call" — flavor only now, no persistent cost either way.
        private const float CloseCallHealthFraction = 0.25f;

        private RectTransform _effectsLayer;
        private RectTransform _gearRow;
        private RectTransform _abilityRow;
        private RectTransform _playerAnchor;
        private RectTransform _enemyAnchor;
        private Image _playerPortrait;
        private Image _enemyPortrait;
        private Color _playerBaseColor;
        private Color _enemyBaseColor;
        private Image _playerHealthFill;
        private Text _playerHealthLabel;
        private Image _enemyHealthFill;
        private Text _enemyHealthLabel;
        private Coroutine _playerHealthTween;
        private Coroutine _enemyHealthTween;

        private readonly Dictionary<string, AbilityWidget> _abilityWidgets = new();
        private CombatSimulator _sim;
        private bool _activeManualMode;
        private string _pendingManualCastName;

        private GameObject _reviveDialogGo;
        private Text _reviveDialogMessage;
        private bool _reviveDialogAnswerYes;
        private bool _reviveDialogAnswered;

        public bool LastOutcomeWasVictory { get; private set; }
        public bool WasCloseCall { get; private set; }
        // The player's HP when the fight ended — 0 by default, meaningless for a
        // Treasure card's instant win (see the null-sim branch below). Read by
        // DungeonStepPageView to carry HP forward into the next step of a run.
        public int FinalPlayerHp { get; private set; }

        // Set once PlayTimedWindow's coroutine finishes — the resource minigame's score.
        public int TotalDamageDealt { get; private set; }

        // screenRoot is a full-screen ancestor (the host view's own root) the revive
        // dialog anchors to — this view's own rect is only the bounded combat-sim area,
        // too small for a proper full-screen modal.
        public static CombatPlaybackView Create(Transform parent, Transform screenRoot)
        {
            var go = new GameObject("CombatPlayback", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var outerLayout = go.AddComponent<LayoutElement>();
            // All three explicit: the VerticalLayoutGroup below lives on this same
            // GameObject and is itself an ILayoutElement tying with this one on
            // layoutPriority — an unset minHeight/flexibleHeight here lets the group's
            // own computed value win the tie (Unity picks the larger of the two) and
            // silently overrides this preferredHeight. Same bug/fix as FeedCardView's
            // TopRow when it grew a HorizontalLayoutGroup of its own. The value itself is
            // a deliberate overestimate (real content is a bit less) — safe, since any
            // slack just becomes blank space rather than clipping.
            var totalHeight = PortraitSize + AbilityWidgetHeight + 100f + HealthBarHeight + 12f;
            outerLayout.minHeight = totalHeight;
            outerLayout.preferredHeight = totalHeight;
            outerLayout.flexibleHeight = 0;

            var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 12;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            var view = go.AddComponent<CombatPlaybackView>();

            var battleRowGo = new GameObject("BattleRow", typeof(RectTransform));
            battleRowGo.transform.SetParent(rect, false);
            battleRowGo.AddComponent<LayoutElement>().preferredHeight = PortraitSize;
            var battleRowLayout = battleRowGo.AddComponent<HorizontalLayoutGroup>();
            battleRowLayout.spacing = 24;
            battleRowLayout.childAlignment = TextAnchor.MiddleCenter;
            battleRowLayout.childForceExpandWidth = false;
            battleRowLayout.childForceExpandHeight = false;
            battleRowLayout.childControlWidth = true;
            battleRowLayout.childControlHeight = true;

            view._playerPortrait = CreatePortrait(battleRowGo.transform, out view._playerAnchor);

            var vsGo = new GameObject("VsLabel", typeof(RectTransform));
            vsGo.transform.SetParent(battleRowGo.transform, false);
            vsGo.AddComponent<LayoutElement>().preferredWidth = 90;
            var vsText = vsGo.AddComponent<Text>();
            vsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            vsText.fontSize = 40;
            vsText.fontStyle = FontStyle.Bold;
            vsText.alignment = TextAnchor.MiddleCenter;
            vsText.color = new Color(1f, 1f, 1f, 0.6f);
            vsText.text = "VS";

            view._enemyPortrait = CreatePortrait(battleRowGo.transform, out view._enemyAnchor);

            // Left-aligned, directly below the player's side of BattleRow — reads as
            // "belongs to the player" without needing to anchor onto the portrait itself.
            var abilityRowGo = new GameObject("AbilityRow", typeof(RectTransform));
            abilityRowGo.transform.SetParent(rect, false);
            abilityRowGo.AddComponent<LayoutElement>().preferredHeight = AbilityWidgetHeight;
            var abilityRowLayout = abilityRowGo.AddComponent<HorizontalLayoutGroup>();
            abilityRowLayout.spacing = 10;
            abilityRowLayout.childAlignment = TextAnchor.MiddleLeft;
            abilityRowLayout.childForceExpandWidth = false;
            abilityRowLayout.childForceExpandHeight = false;
            abilityRowLayout.childControlWidth = true;
            abilityRowLayout.childControlHeight = true;
            view._abilityRow = (RectTransform)abilityRowGo.transform;

            var healthBarRowGo = new GameObject("HealthBarRow", typeof(RectTransform));
            healthBarRowGo.transform.SetParent(rect, false);
            healthBarRowGo.AddComponent<LayoutElement>().preferredHeight = HealthBarHeight;
            var healthBarRowLayout = healthBarRowGo.AddComponent<HorizontalLayoutGroup>();
            healthBarRowLayout.spacing = 24;
            healthBarRowLayout.childAlignment = TextAnchor.MiddleCenter;
            healthBarRowLayout.childForceExpandWidth = false;
            healthBarRowLayout.childForceExpandHeight = false;
            healthBarRowLayout.childControlWidth = true;
            healthBarRowLayout.childControlHeight = true;

            CreateHealthBar(healthBarRowGo.transform, PortraitSize, out view._playerHealthFill, out view._playerHealthLabel,
                new Color(0.3f, 0.75f, 0.35f, 1f));

            var spacerGo = new GameObject("Spacer", typeof(RectTransform));
            spacerGo.transform.SetParent(healthBarRowGo.transform, false);
            spacerGo.AddComponent<LayoutElement>().preferredWidth = 70;

            CreateHealthBar(healthBarRowGo.transform, PortraitSize, out view._enemyHealthFill, out view._enemyHealthLabel,
                new Color(0.8f, 0.3f, 0.25f, 1f));

            var gearRowGo = new GameObject("GearRow", typeof(RectTransform));
            gearRowGo.transform.SetParent(rect, false);
            gearRowGo.AddComponent<LayoutElement>().preferredHeight = GearSquareSize;
            var gearRowLayout = gearRowGo.AddComponent<HorizontalLayoutGroup>();
            gearRowLayout.spacing = 8;
            gearRowLayout.childAlignment = TextAnchor.MiddleCenter;
            gearRowLayout.childForceExpandWidth = false;
            gearRowLayout.childForceExpandHeight = false;
            gearRowLayout.childControlWidth = true;
            gearRowLayout.childControlHeight = true;
            view._gearRow = (RectTransform)gearRowGo.transform;

            var effectsGo = new GameObject("EffectsLayer", typeof(RectTransform));
            var effectsRect = (RectTransform)effectsGo.transform;
            effectsRect.SetParent(rect, false);
            effectsRect.anchorMin = Vector2.zero;
            effectsRect.anchorMax = Vector2.one;
            effectsRect.offsetMin = Vector2.zero;
            effectsRect.offsetMax = Vector2.zero;
            // Overlay, not a layout cell — same ignoreLayout fix as EquipSlotDetailView's
            // confirm dialog: the parent VerticalLayoutGroup would otherwise seize and
            // collapse this on activation.
            effectsGo.AddComponent<LayoutElement>().ignoreLayout = true;
            view._effectsLayer = effectsRect;

            view._reviveDialogGo = CreateReviveDialog(screenRoot, view);

            return view;
        }

        public IEnumerator Play(CombatSimulator sim, PlayerLoadout loadout, GameSettings settings, Sprite enemySprite, Color enemyColor)
        {
            _playerPortrait.sprite = loadout.PlayerSprite;
            _playerBaseColor = loadout.PlayerSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            _playerPortrait.color = _playerBaseColor;

            _enemyPortrait.sprite = enemySprite;
            _enemyBaseColor = enemySprite != null ? Color.white : enemyColor;
            _enemyPortrait.color = _enemyBaseColor;

            BuildGearRow(loadout);

            _sim = sim;
            _activeManualMode = settings != null && settings.ManualAbilityCasting;
            _pendingManualCastName = null;
            BuildAbilityRow(loadout, _activeManualMode);

            if (sim == null)
            {
                // Treasure — no fight, guaranteed win, nothing to animate.
                UpdateHealthBar(_playerHealthFill, _playerHealthLabel, 1, 1);
                UpdateHealthBar(_enemyHealthFill, _enemyHealthLabel, 0, 0);
                LastOutcomeWasVictory = true;
                WasCloseCall = false;
                _sim = null;
                yield break;
            }

            UpdateHealthBar(_playerHealthFill, _playerHealthLabel, sim.PlayerHp, sim.PlayerStartHp);
            UpdateHealthBar(_enemyHealthFill, _enemyHealthLabel, sim.EnemyHp, sim.EnemyMaxHp);

            // Every Revive potion is always "available" (see PlayerLoadout.GetBestRevivePotion)
            // but a fight still only gets to use one — this used to fall out for free from
            // only one potion ever being "carried" at a time; now it's tracked explicitly.
            var revivedThisFight = false;

            while (true)
            {
                var playerHpBefore = sim.PlayerHp;
                var enemyHpBefore = sim.EnemyHp;
                var beats = sim.RunUntilDeathOrVictory();

                // Seed each auto ability's cooldown bar toward its first cast in this
                // fresh batch (also covers the very start of the fight, where every
                // active ability starts on cooldown with nothing having cast yet).
                if (!_activeManualMode)
                {
                    foreach (var name in _abilityWidgets.Keys)
                    {
                        var firstCast = FindNextCastIndex(beats, 0, name);
                        if (firstCast >= 0)
                        {
                            StartAbilityCooldownLerp(name, ComputeGapSeconds(beats, 0, firstCast));
                        }
                    }
                }

                yield return StartCoroutine(PlayBeats(beats, playerHpBefore, sim.PlayerStartHp, enemyHpBefore, sim.EnemyMaxHp));

                if (_pendingManualCastName != null)
                {
                    var castName = _pendingManualCastName;
                    _pendingManualCastName = null;

                    var playerBeforeCast = sim.PlayerHp;
                    var enemyBeforeCast = sim.EnemyHp;
                    var castBeat = sim.CastManualAbility(castName);
                    if (castBeat.HasValue)
                    {
                        yield return StartCoroutine(PlayBeats(new[] { castBeat.Value }, playerBeforeCast,
                            sim.PlayerStartHp, enemyBeforeCast, sim.EnemyMaxHp));
                    }

                    // A manual cast may have just ended the fight, or simply changed the
                    // HP the next batch needs to be computed from — either way, loop back
                    // around rather than trusting the (now possibly stale) beats above.
                    if (!sim.IsEnemyDead && !sim.IsPlayerDead)
                    {
                        continue;
                    }
                }

                if (sim.IsEnemyDead)
                {
                    LastOutcomeWasVictory = true;
                    break;
                }

                if (sim.IsPlayerDead)
                {
                    var potion = revivedThisFight ? null : loadout.GetBestRevivePotion();
                    var revive = false;

                    if (potion != null)
                    {
                        if (settings.AutoConsumeRevivePotions)
                        {
                            revive = true;
                        }
                        else
                        {
                            yield return StartCoroutine(ShowReviveDialog(potion));
                            revive = _reviveDialogAnswerYes;
                        }
                    }

                    if (revive)
                    {
                        loadout.ConsumeRevivePotion(potion);
                        revivedThisFight = true;
                        sim.Revive(loadout.GetRevivePotionAmount(potion));
                        UpdateHealthBar(_playerHealthFill, _playerHealthLabel, sim.PlayerHp, sim.PlayerStartHp);
                        SpawnPopup(_playerAnchor, "REVIVED!", new Color(0.4f, 0.9f, 1f), 44);
                        yield return new WaitForSeconds(BeatPause);
                        continue;
                    }

                    LastOutcomeWasVictory = false;
                    break;
                }
            }

            WasCloseCall = LastOutcomeWasVictory && sim.PlayerStartHp > 0 &&
                (float)sim.PlayerHp / sim.PlayerStartHp < CloseCallHealthFraction;
            FinalPlayerHp = sim.PlayerHp;
            _sim = null;
        }

        // The resource-node minigame's entry point — sibling to Play, but structurally
        // simpler: the whole run resolves synchronously up front (sim.RunForDuration),
        // every beat is guaranteed CombatActor.Player (the node never attacks — see
        // CombatSimulator's enemyCanAttack), and there's no win/loss/revive branching to
        // handle. Manual ability casting is deliberately not supported here even if
        // GameSettings.ManualAbilityCasting is on — every ability just auto-fires as
        // part of the timed window, same as this mode's own timer. Reuses the enemy-side
        // health bar as a real-time countdown (fill drains 1→0 over durationSeconds) and
        // its label as a running damage total — an actual depleting enemy HP bar would
        // misrepresent a target that structurally can't die within the window.
        public IEnumerator PlayTimedWindow(CombatSimulator sim, PlayerLoadout loadout, GameSettings settings,
            Sprite nodeSprite, Color nodeColor, float durationSeconds)
        {
            _playerPortrait.sprite = loadout.PlayerSprite;
            _playerBaseColor = loadout.PlayerSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            _playerPortrait.color = _playerBaseColor;

            _enemyPortrait.sprite = nodeSprite;
            _enemyBaseColor = nodeSprite != null ? Color.white : nodeColor;
            _enemyPortrait.color = _enemyBaseColor;

            BuildGearRow(loadout);
            _sim = sim;
            _activeManualMode = false;
            _pendingManualCastName = null;
            BuildAbilityRow(loadout, false);

            UpdateHealthBar(_playerHealthFill, _playerHealthLabel, 1, 1);
            _enemyHealthFill.fillAmount = 1f;
            _enemyHealthLabel.text = "Damage: 0";

            var beats = sim.RunForDuration(durationSeconds);
            var countdown = StartCoroutine(CountdownRoutine(durationSeconds));

            // Spread across the window rather than the fixed combat BeatPause — a fast
            // weapon landing 30 beats in 10s would otherwise strobe by unreadably, and a
            // slow one landing 3 would leave long dead gaps; clamped to a sane range
            // either way.
            var spacing = beats.Count > 0 ? Mathf.Clamp(durationSeconds / beats.Count, 0.08f, 0.6f) : 0f;
            var runningDamage = 0;

            foreach (var beat in beats)
            {
                var isAbility = !string.IsNullOrEmpty(beat.AbilityName);
                yield return StartCoroutine(Lunge(_playerPortrait.rectTransform, 1f));

                if (beat.IsRanged)
                {
                    var abilityColor = isAbility && _abilityWidgets.TryGetValue(beat.AbilityName, out var castWidget)
                        ? castWidget.Icon.color
                        : Color.white;
                    yield return StartCoroutine(FireProjectile(_playerAnchor, _enemyAnchor, true, isAbility, beat.IsDotTick, abilityColor));
                }

                if (!beat.WasMiss)
                {
                    runningDamage += beat.DisplayDamage;
                    _enemyHealthLabel.text = $"Damage: {runningDamage}";
                }

                ResolveImpact(_enemyAnchor, _enemyPortrait, beat, true);
                yield return new WaitForSeconds(spacing);
            }

            // Let the countdown finish draining even if every beat already animated
            // faster than the real window (a slow weapon might only land 2-3 beats).
            yield return countdown;

            TotalDamageDealt = sim.DamageDealt;
            _sim = null;
        }

        private IEnumerator CountdownRoutine(float seconds)
        {
            if (seconds <= 0f)
            {
                _enemyHealthFill.fillAmount = 0f;
                yield break;
            }

            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                _enemyHealthFill.fillAmount = 1f - Mathf.Clamp01(t / seconds);
                yield return null;
            }

            _enemyHealthFill.fillAmount = 0f;
        }

        private void Update()
        {
            // Manual-ability readiness is the one thing in this view driven by real
            // (unpaused-fight) time rather than the beat sequence — see
            // CombatSimulator.AdvanceManualCooldowns.
            if (_sim == null || !_activeManualMode || _sim.IsPlayerDead || _sim.IsEnemyDead)
            {
                return;
            }

            _sim.AdvanceManualCooldowns(Time.deltaTime);

            foreach (var state in _sim.GetManualAbilityStates())
            {
                if (_abilityWidgets.TryGetValue(state.Name, out var widget))
                {
                    widget.CooldownFill.fillAmount = state.RemainingFraction;
                    widget.Button.interactable = state.IsReady;
                }
            }
        }

        // Animates one already-resolved batch of beats, tracking its own running HP
        // totals (seeded from the values before the batch ran) so the bars visibly
        // deplete beat-by-beat — CombatSimulator itself only exposes the batch's final
        // state, not each intermediate step. In auto mode, also drives each ability's
        // cooldown-bar lerp forward whenever its own cast beat plays. Bails out early if
        // a manual-ability tap came in mid-batch — the remaining beats were computed
        // against HP the tap has since changed, so they're stale (see Play()).
        private IEnumerator PlayBeats(IReadOnlyList<CombatBeat> beats, int playerHp, int playerMaxHp, int enemyHp, int enemyMaxHp)
        {
            for (var i = 0; i < beats.Count; i++)
            {
                if (_pendingManualCastName != null)
                {
                    yield break;
                }

                var beat = beats[i];
                var isPlayer = beat.Actor == CombatActor.Player;
                var attackerAnchor = isPlayer ? _playerAnchor : _enemyAnchor;
                var attackerImage = isPlayer ? _playerPortrait : _enemyPortrait;
                var defenderAnchor = isPlayer ? _enemyAnchor : _playerAnchor;
                var defenderImage = isPlayer ? _enemyPortrait : _playerPortrait;
                var isAbility = !string.IsNullOrEmpty(beat.AbilityName);

                yield return StartCoroutine(Lunge(attackerImage.rectTransform, isPlayer ? 1f : -1f));

                if (beat.IsRanged)
                {
                    var abilityColor = isAbility && _abilityWidgets.TryGetValue(beat.AbilityName, out var castWidget)
                        ? castWidget.Icon.color
                        : Color.white;
                    yield return StartCoroutine(FireProjectile(attackerAnchor, defenderAnchor, isPlayer, isAbility,
                        beat.IsDotTick, abilityColor));
                }

                if (!beat.WasMiss)
                {
                    if (isPlayer)
                    {
                        enemyHp = Mathf.Max(0, enemyHp - beat.DisplayDamage);
                        if (_enemyHealthTween != null)
                        {
                            StopCoroutine(_enemyHealthTween);
                        }
                        _enemyHealthTween = StartCoroutine(AnimateHealthBar(_enemyHealthFill, _enemyHealthLabel, enemyHp, enemyMaxHp));
                    }
                    else
                    {
                        playerHp = Mathf.Max(0, playerHp - beat.DisplayDamage);
                        if (_playerHealthTween != null)
                        {
                            StopCoroutine(_playerHealthTween);
                        }
                        _playerHealthTween = StartCoroutine(AnimateHealthBar(_playerHealthFill, _playerHealthLabel, playerHp, playerMaxHp));
                    }
                }

                ResolveImpact(defenderAnchor, defenderImage, beat, isPlayer);

                if (!_activeManualMode && isAbility)
                {
                    var nextCast = FindNextCastIndex(beats, i + 1, beat.AbilityName);
                    if (nextCast >= 0)
                    {
                        StartAbilityCooldownLerp(beat.AbilityName, ComputeGapSeconds(beats, i + 1, nextCast));
                    }
                }

                yield return new WaitForSeconds(BeatPause);
            }
        }

        private IEnumerator ShowReviveDialog(ItemData potion)
        {
            _reviveDialogAnswered = false;
            _reviveDialogMessage.text = $"You've fallen! Use your {potion.title} to keep fighting?";
            _reviveDialogGo.SetActive(true);
            yield return new WaitUntil(() => _reviveDialogAnswered);
            _reviveDialogGo.SetActive(false);
        }

        private IEnumerator Lunge(RectTransform portrait, float direction)
        {
            var start = Vector2.zero;
            var peak = new Vector2(LungeDistance * direction, 0f);
            var half = LungeDuration * 0.5f;

            var t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                portrait.anchoredPosition = Vector2.Lerp(start, peak, t / half);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                portrait.anchoredPosition = Vector2.Lerp(peak, start, t / half);
                yield return null;
            }

            portrait.anchoredPosition = start;
        }

        private IEnumerator FireProjectile(RectTransform from, RectTransform to, bool isPlayerCast, bool isAbility,
            bool isDotTick, Color abilityColor)
        {
            var projectileGo = new GameObject("Projectile", typeof(RectTransform));
            var projectileRect = (RectTransform)projectileGo.transform;
            projectileRect.SetParent(_effectsLayer, false);
            projectileRect.anchorMin = new Vector2(0.5f, 0.5f);
            projectileRect.anchorMax = new Vector2(0.5f, 0.5f);
            projectileRect.pivot = new Vector2(0.5f, 0.5f);
            var size = isAbility ? AbilityProjectileSize : (isDotTick ? DotProjectileSize : ProjectileSize);
            projectileRect.sizeDelta = new Vector2(size, size);

            var image = projectileGo.AddComponent<Image>();
            // A skill's projectile takes on its own icon color so the payoff visually
            // ties back to which ability fired; a DoT tick gets its own small
            // sickly-green bolt so it doesn't read as a fresh attack landing; a plain
            // ranged basic attack keeps the generic side-colored bolt.
            image.color = isAbility ? abilityColor
                : isDotTick ? DotTickColor
                : isPlayerCast ? new Color(0.4f, 0.8f, 1f, 1f)
                : new Color(1f, 0.4f, 0.3f, 1f);

            var fromPoint = LocalPointIn(_effectsLayer, from);
            var toPoint = LocalPointIn(_effectsLayer, to);
            projectileRect.anchoredPosition = fromPoint;

            var t = 0f;
            while (t < ProjectileDuration)
            {
                t += Time.deltaTime;
                projectileRect.anchoredPosition = Vector2.Lerp(fromPoint, toPoint, t / ProjectileDuration);
                yield return null;
            }

            Destroy(projectileGo);
        }

        private void ResolveImpact(RectTransform defenderAnchor, Image defenderImage, CombatBeat beat, bool isPlayerAttacker)
        {
            if (beat.WasMiss)
            {
                SpawnPopup(defenderAnchor, "MISS", new Color(1f, 1f, 1f, 0.7f), 36);
                return;
            }

            StartCoroutine(Flash(defenderImage));

            if (beat.IsDotTick)
            {
                // Smaller and tinted green — reads as chip damage from an ongoing
                // effect rather than a real hit landing, same distinction the
                // projectile above already makes.
                SpawnPopup(defenderAnchor, beat.DisplayDamage.ToString(), DotTickColor, 32);
                return;
            }

            var color = isPlayerAttacker ? Color.white : new Color(1f, 0.35f, 0.3f);
            SpawnPopup(defenderAnchor, beat.DisplayDamage.ToString(), color, 50);
        }

        private IEnumerator Flash(Image image)
        {
            var baseColor = image == _playerPortrait ? _playerBaseColor : _enemyBaseColor;
            image.color = Color.white;

            var t = 0f;
            while (t < FlashDuration)
            {
                t += Time.deltaTime;
                image.color = Color.Lerp(Color.white, baseColor, t / FlashDuration);
                yield return null;
            }

            image.color = baseColor;
        }

        private void SpawnPopup(RectTransform anchor, string text, Color color, int fontSize)
        {
            StartCoroutine(PopupRoutine(anchor, text, color, fontSize));
        }

        private IEnumerator PopupRoutine(RectTransform anchor, string text, Color color, int fontSize)
        {
            var go = new GameObject("DamagePopup", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_effectsLayer, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220, 70);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;

            var startPoint = LocalPointIn(_effectsLayer, anchor);
            rect.anchoredPosition = startPoint;

            var t = 0f;
            while (t < DamageNumberDuration)
            {
                t += Time.deltaTime;
                var progress = t / DamageNumberDuration;
                rect.anchoredPosition = startPoint + new Vector2(0f, DamageNumberRise * progress);
                var c = label.color;
                c.a = Mathf.Lerp(1f, 0f, progress);
                label.color = c;
                yield return null;
            }

            Destroy(go);
        }

        // Rebuilt on every Play — this view is only ever visible while a fight is being
        // shown, so a per-fight rebuild is enough to stay in sync with equip/unequip
        // changes made between fights.
        private void BuildGearRow(PlayerLoadout loadout)
        {
            for (var i = _gearRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_gearRow.GetChild(i).gameObject);
            }

            foreach (EquipmentSlotType slot in System.Enum.GetValues(typeof(EquipmentSlotType)))
            {
                var item = loadout.GetEquipped(slot);
                if (item == null)
                {
                    continue;
                }

                var squareGo = new GameObject($"Gear_{slot}", typeof(RectTransform));
                squareGo.transform.SetParent(_gearRow, false);
                var layoutElement = squareGo.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = GearSquareSize;
                layoutElement.preferredHeight = GearSquareSize;
                layoutElement.flexibleWidth = 0;
                layoutElement.flexibleHeight = 0;

                var image = squareGo.AddComponent<Image>();
                image.color = item.itemColor;
            }
        }

        // Rebuilt on every Play, same convention as BuildGearRow — one widget per
        // currently-active weapon ability (icon + shrinking cooldown overlay). Whether
        // each widget is driven by the auto beat sequence or by a live real-time tap is
        // decided globally by manualMode, not per-ability (matches CombatSimulator).
        private void BuildAbilityRow(PlayerLoadout loadout, bool manualMode)
        {
            foreach (var widget in _abilityWidgets.Values)
            {
                if (widget.CooldownLerp != null)
                {
                    StopCoroutine(widget.CooldownLerp);
                }
            }
            _abilityWidgets.Clear();

            for (var i = _abilityRow.childCount - 1; i >= 0; i--)
            {
                Destroy(_abilityRow.GetChild(i).gameObject);
            }

            foreach (var (_, ability) in loadout.GetEquippedWeaponAbilityPairs())
            {
                // Every ability on an equipped weapon gets a widget — no toggle, no cap.
                if (_abilityWidgets.ContainsKey(ability.abilityName))
                {
                    continue;
                }

                _abilityWidgets[ability.abilityName] = CreateAbilityWidget(_abilityRow, ability, manualMode);
            }
        }

        // Icon on top, a separate shrinking bar directly underneath it — not an overlay
        // painted on the icon itself. The bar reads as "cooldown remaining": full while
        // just cast, empty (and the icon tappable, in manual mode) once ready.
        private AbilityWidget CreateAbilityWidget(Transform parent, WeaponAbilityData ability, bool manualMode)
        {
            var go = new GameObject($"Ability_{ability.abilityName}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var containerLayout = go.AddComponent<LayoutElement>();
            containerLayout.preferredWidth = AbilityIconSize;
            containerLayout.preferredHeight = AbilityWidgetHeight;
            containerLayout.flexibleWidth = 0;
            containerLayout.flexibleHeight = 0;

            var containerGroup = go.AddComponent<VerticalLayoutGroup>();
            containerGroup.spacing = AbilityWidgetSpacing;
            containerGroup.childAlignment = TextAnchor.UpperCenter;
            containerGroup.childForceExpandWidth = true;
            containerGroup.childForceExpandHeight = false;
            containerGroup.childControlWidth = true;
            containerGroup.childControlHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = AbilityIconSize;
            iconLayout.preferredHeight = AbilityIconSize;
            iconLayout.flexibleWidth = 0;
            iconLayout.flexibleHeight = 0;
            var icon = iconGo.AddComponent<Image>();
            GearRowFactory.SetIcon(icon, ability.icon, ability.iconColor);

            // Always present, but only ever interactable (and only ever tapped
            // meaningfully) in manual mode — CombatSimulator.CastManualAbility is a
            // no-op for an auto-tracked ability anyway, so this is a defensive no-op
            // rather than something that needs its own manualMode branch here.
            var button = iconGo.AddComponent<Button>();
            button.interactable = false;
            var capturedName = ability.abilityName;
            button.onClick.AddListener(() =>
            {
                if (_pendingManualCastName == null)
                {
                    _pendingManualCastName = capturedName;
                }
            });

            var barGo = new GameObject("CooldownBar", typeof(RectTransform));
            barGo.transform.SetParent(go.transform, false);
            var barLayout = barGo.AddComponent<LayoutElement>();
            barLayout.preferredWidth = AbilityIconSize;
            barLayout.preferredHeight = AbilityCooldownBarHeight;
            barLayout.flexibleWidth = 0;
            barLayout.flexibleHeight = 0;
            barGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(barGo.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1, 1);
            fillRect.offsetMax = new Vector2(-1, -1);
            var fill = fillGo.AddComponent<Image>();
            fill.color = ability.iconColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            return new AbilityWidget(icon, fill, button);
        }

        private void StartAbilityCooldownLerp(string abilityName, float seconds)
        {
            if (!_abilityWidgets.TryGetValue(abilityName, out var widget))
            {
                return;
            }

            if (widget.CooldownLerp != null)
            {
                StopCoroutine(widget.CooldownLerp);
            }

            widget.CooldownLerp = StartCoroutine(AbilityCooldownLerpRoutine(widget.CooldownFill, seconds));
        }

        private static IEnumerator AbilityCooldownLerpRoutine(Image fill, float seconds)
        {
            fill.fillAmount = 1f;

            if (seconds <= 0f)
            {
                fill.fillAmount = 0f;
                yield break;
            }

            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                fill.fillAmount = 1f - Mathf.Clamp01(t / seconds);
                yield return null;
            }

            fill.fillAmount = 0f;
        }

        // Index of the next beat at/after fromIndex that's a cast of abilityName, or -1
        // if this batch has no further cast of it.
        private static int FindNextCastIndex(IReadOnlyList<CombatBeat> beats, int fromIndex, string abilityName)
        {
            for (var i = fromIndex; i < beats.Count; i++)
            {
                if (beats[i].AbilityName == abilityName)
                {
                    return i;
                }
            }

            return -1;
        }

        // Real-seconds cost of animating beats[fromIndexInclusive..toIndexExclusive) —
        // the same per-beat costs PlayBeats itself pays (lunge, optional projectile,
        // the pause after) — used to time an ability's cooldown-bar lerp so it empties
        // right as that beat is reached.
        private static float ComputeGapSeconds(IReadOnlyList<CombatBeat> beats, int fromIndexInclusive, int toIndexExclusive)
        {
            var total = 0f;
            for (var i = fromIndexInclusive; i < toIndexExclusive; i++)
            {
                total += LungeDuration + (beats[i].IsRanged ? ProjectileDuration : 0f) + BeatPause;
            }

            return total;
        }

        // Converts target's world position into a point in reference's local rect
        // space, directly usable as anchoredPosition for a (0.5, 0.5)-anchored child of
        // reference — the standard technique for following an arbitrary UI element's
        // position across sibling hierarchies (used here since the portraits live under
        // BattleRow while projectiles/damage numbers live under EffectsLayer).
        private static Vector2 LocalPointIn(RectTransform reference, RectTransform target)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(reference, screenPoint, null, out var localPoint);
            return localPoint;
        }

        // Three stacked layers: an outline frame (the cell's own Image), a dark track
        // inset inside it, and the colored fill inset again inside the track — the
        // outline reads as a clean border around the bar rather than just a color change.
        private static void CreateHealthBar(Transform parent, float width, out Image fill, out Text label, Color fillColor)
        {
            var cellGo = new GameObject("HealthBarCell", typeof(RectTransform));
            cellGo.transform.SetParent(parent, false);
            var layoutElement = cellGo.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = HealthBarHeight;
            layoutElement.flexibleWidth = 0;

            var outline = cellGo.AddComponent<Image>();
            outline.color = new Color(1f, 1f, 1f, 0.85f);

            var trackGo = new GameObject("Track", typeof(RectTransform));
            var trackRect = (RectTransform)trackGo.transform;
            trackRect.SetParent(cellGo.transform, false);
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(HealthBarOutlineWidth, HealthBarOutlineWidth);
            trackRect.offsetMax = new Vector2(-HealthBarOutlineWidth, -HealthBarOutlineWidth);
            var track = trackGo.AddComponent<Image>();
            track.color = new Color(0f, 0f, 0f, 0.6f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(trackGo.transform, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2, 2);
            fillRect.offsetMax = new Vector2(-2, -2);

            fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            // Parented to the cell itself (not the track) so it renders above both the
            // outline and the track/fill regardless of how full the bar is.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(cellGo.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        private static void UpdateHealthBar(Image fill, Text label, int current, int max)
        {
            fill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            label.text = $"{Mathf.Max(0, current)}/{max}";
        }

        // Smoothly drains (or refills) the bar to the new percentage instead of snapping
        // instantly, so a hit visibly depletes it rather than just changing a number.
        private static IEnumerator AnimateHealthBar(Image fill, Text label, int current, int max)
        {
            var startFill = fill.fillAmount;
            var targetFill = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            label.text = $"{Mathf.Max(0, current)}/{max}";

            var t = 0f;
            while (t < HealthBarTweenDuration)
            {
                t += Time.deltaTime;
                fill.fillAmount = Mathf.Lerp(startFill, targetFill, t / HealthBarTweenDuration);
                yield return null;
            }

            fill.fillAmount = targetFill;
        }

        private static Image CreatePortrait(Transform parent, out RectTransform anchor)
        {
            var anchorGo = new GameObject("PortraitAnchor", typeof(RectTransform));
            anchorGo.transform.SetParent(parent, false);
            var anchorLayout = anchorGo.AddComponent<LayoutElement>();
            anchorLayout.preferredWidth = PortraitSize;
            anchorLayout.preferredHeight = PortraitSize;
            anchor = (RectTransform)anchorGo.transform;

            // The Image lives one level below the LayoutGroup-controlled Anchor cell, so
            // the lunge tween below can freely move its own anchoredPosition without the
            // HorizontalLayoutGroup fighting it back on the next layout pass (same
            // "layout owns direct children only" reasoning as everywhere else in this
            // project that shakes/offsets something inside a managed row).
            var portraitGo = new GameObject("Portrait", typeof(RectTransform));
            var portraitRect = (RectTransform)portraitGo.transform;
            portraitRect.SetParent(anchor, false);
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(PortraitSize, PortraitSize);

            var image = portraitGo.AddComponent<Image>();
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.12f);
            return image;
        }

        // Full-screen Yes/No modal for the "you died, use a revive potion?" pause —
        // anchored to screenRoot (not this view's own small rect) and built the same
        // way as DungeonView's leave-confirm dialog, including the ignoreLayout fix.
        private static GameObject CreateReviveDialog(Transform screenRoot, CombatPlaybackView view)
        {
            var overlayGo = new GameObject("ReviveDialog", typeof(RectTransform));
            var overlayRect = (RectTransform)overlayGo.transform;
            overlayRect.SetParent(screenRoot, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayGo.AddComponent<LayoutElement>().ignoreLayout = true;
            overlayGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

            var boxGo = new GameObject("Box", typeof(RectTransform));
            var boxRect = (RectTransform)boxGo.transform;
            boxRect.SetParent(overlayRect, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(800, 380);
            boxGo.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var boxLayout = boxGo.AddComponent<VerticalLayoutGroup>();
            boxLayout.padding = new RectOffset(32, 32, 28, 28);
            boxLayout.spacing = 20;
            boxLayout.childAlignment = TextAnchor.MiddleCenter;
            boxLayout.childForceExpandWidth = true;
            boxLayout.childForceExpandHeight = false;
            boxLayout.childControlWidth = true;
            boxLayout.childControlHeight = true;

            view._reviveDialogMessage = CreateDialogLabel(boxRect, "Message", "", 32, FontStyle.Bold);
            SetFixedHeight(view._reviveDialogMessage.GetComponent<LayoutElement>(), 160);

            var buttonRowGo = new GameObject("ButtonRow", typeof(RectTransform));
            buttonRowGo.transform.SetParent(boxRect, false);
            SetFixedHeight(buttonRowGo.AddComponent<LayoutElement>(), 100);
            var buttonRowLayout = buttonRowGo.AddComponent<HorizontalLayoutGroup>();
            buttonRowLayout.spacing = 16;
            buttonRowLayout.childForceExpandWidth = false;
            buttonRowLayout.childForceExpandHeight = false;
            buttonRowLayout.childControlWidth = true;
            buttonRowLayout.childControlHeight = true;

            CreateDialogButton(buttonRowGo.transform, "NoButton", "No", 360, 100,
                new Color(1f, 1f, 1f, 0.15f), () =>
                {
                    view._reviveDialogAnswerYes = false;
                    view._reviveDialogAnswered = true;
                });
            CreateDialogButton(buttonRowGo.transform, "YesButton", "Yes, revive!", 360, 100,
                new Color(0.2f, 0.6f, 0.35f, 0.85f), () =>
                {
                    view._reviveDialogAnswerYes = true;
                    view._reviveDialogAnswered = true;
                });

            overlayGo.SetActive(false);
            return overlayGo;
        }

        private static void SetFixedHeight(LayoutElement layoutElement, float height)
        {
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
        }

        private static Text CreateDialogLabel(Transform parent, string name, string content, int fontSize, FontStyle style)
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

            go.AddComponent<LayoutElement>().preferredHeight = fontSize + 24;

            return text;
        }

        private static void CreateDialogButton(Transform parent, string name, string label,
            float width, float height, Color backgroundColor, System.Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            SetFixedHeight(go.AddComponent<LayoutElement>(), height);
            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minWidth = width;
            layoutElement.preferredWidth = width;
            layoutElement.flexibleWidth = 0;

            var image = go.AddComponent<Image>();
            image.color = backgroundColor;

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(() => onClick());

            var labelRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            labelRect.SetParent(go.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = labelRect.gameObject.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 30;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.text = label;
            labelText.raycastTarget = false;
        }

        private sealed class AbilityWidget
        {
            public readonly Image Icon;
            public readonly Image CooldownFill;
            public readonly Button Button;
            public Coroutine CooldownLerp;

            public AbilityWidget(Image icon, Image cooldownFill, Button button)
            {
                Icon = icon;
                CooldownFill = cooldownFill;
                Button = button;
            }
        }
    }
}
