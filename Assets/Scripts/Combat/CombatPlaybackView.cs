using System;
using System.Collections;
using DoomScroller.Inventory;
using DoomScroller.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace DoomScroller.Combat
{
    // Shared visual fight player, hosted by both CombatResolutionView and
    // DungeonStepPageView (which previously each hand-rolled their own battle row).
    // Walks a CombatResult's beats and animates them: the attacker wiggles/lunges, a
    // projectile flies for ranged beats (every ability cast, or a ranged weapon's basic
    // attack), the defender flashes and a damage number (or "MISS") pops up. No
    // text-log narration — Phase 8.
    public class CombatPlaybackView : MonoBehaviour
    {
        private const float PortraitSize = 280f;
        private const float GearSquareSize = 52f;
        private const float BeatPause = 0.35f;
        private const float LungeDuration = 0.22f;
        private const float LungeDistance = 50f;
        private const float ProjectileDuration = 0.2f;
        private const float ProjectileSize = 34f;
        private const float FlashDuration = 0.15f;
        private const float DamageNumberDuration = 0.6f;
        private const float DamageNumberRise = 90f;
        private const float HealthBarHeight = 40f;
        private const float HealthBarOutlineWidth = 3f;
        private const float HealthBarTweenDuration = 0.3f;

        private RectTransform _effectsLayer;
        private RectTransform _gearRow;
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

        public static CombatPlaybackView Create(Transform parent)
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
            // TopRow when it grew a HorizontalLayoutGroup of its own.
            var totalHeight = PortraitSize + 100f + HealthBarHeight + 12f;
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

            return view;
        }

        // health is the player's real, persistent pool — damage is applied to it live,
        // beat by beat, as each hit lands (not a lump sum at the end), so the player
        // watches their own HP actually drop during the fight. enemyMaxHealth seeds a
        // purely local running counter for the enemy's bar (enemies don't persist
        // between fights, so there's nothing to store it on) — both counters are driven
        // by the same per-beat DisplayDamage values CombatResolver already computed, so
        // they can't drift from the precomputed outcome.
        public IEnumerator Play(CombatResult result, PlayerLoadout loadout, PlayerHealth health, int enemyMaxHealth,
            Sprite enemySprite, Color enemyColor)
        {
            _playerPortrait.sprite = loadout.PlayerSprite;
            _playerBaseColor = loadout.PlayerSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            _playerPortrait.color = _playerBaseColor;

            _enemyPortrait.sprite = enemySprite;
            _enemyBaseColor = enemySprite != null ? Color.white : enemyColor;
            _enemyPortrait.color = _enemyBaseColor;

            BuildGearRow(loadout);

            var enemyHp = enemyMaxHealth;
            UpdateHealthBar(_playerHealthFill, _playerHealthLabel, health.CurrentHealth, health.MaxHealth);
            UpdateHealthBar(_enemyHealthFill, _enemyHealthLabel, enemyHp, enemyMaxHealth);

            foreach (var beat in result.Beats)
            {
                var isPlayer = beat.Actor == CombatActor.Player;
                var attackerAnchor = isPlayer ? _playerAnchor : _enemyAnchor;
                var attackerImage = isPlayer ? _playerPortrait : _enemyPortrait;
                var defenderAnchor = isPlayer ? _enemyAnchor : _playerAnchor;
                var defenderImage = isPlayer ? _enemyPortrait : _playerPortrait;

                yield return StartCoroutine(Lunge(attackerImage.rectTransform, isPlayer ? 1f : -1f));

                if (beat.IsRanged)
                {
                    yield return StartCoroutine(FireProjectile(attackerAnchor, defenderAnchor, isPlayer));
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
                        _enemyHealthTween = StartCoroutine(AnimateHealthBar(_enemyHealthFill, _enemyHealthLabel, enemyHp, enemyMaxHealth));
                    }
                    else
                    {
                        health.ApplyDamage(beat.DisplayDamage);
                        if (_playerHealthTween != null)
                        {
                            StopCoroutine(_playerHealthTween);
                        }
                        _playerHealthTween = StartCoroutine(AnimateHealthBar(_playerHealthFill, _playerHealthLabel, health.CurrentHealth, health.MaxHealth));
                    }
                }

                ResolveImpact(defenderAnchor, defenderImage, beat, isPlayer);

                yield return new WaitForSeconds(BeatPause);
            }
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

        private IEnumerator FireProjectile(RectTransform from, RectTransform to, bool isPlayerCast)
        {
            var projectileGo = new GameObject("Projectile", typeof(RectTransform));
            var projectileRect = (RectTransform)projectileGo.transform;
            projectileRect.SetParent(_effectsLayer, false);
            projectileRect.anchorMin = new Vector2(0.5f, 0.5f);
            projectileRect.anchorMax = new Vector2(0.5f, 0.5f);
            projectileRect.pivot = new Vector2(0.5f, 0.5f);
            projectileRect.sizeDelta = new Vector2(ProjectileSize, ProjectileSize);

            var image = projectileGo.AddComponent<Image>();
            image.color = isPlayerCast ? new Color(0.4f, 0.8f, 1f, 1f) : new Color(1f, 0.4f, 0.3f, 1f);

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

            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
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
    }
}
