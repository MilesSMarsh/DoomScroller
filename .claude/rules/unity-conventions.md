# Unity Conventions

- Namespace scripts `DoomScroller.<System>` matching their folder under
  `Assets/Scripts/` (Combat, Encounters, Feed, Input, Inventory, Progression, Social, UI).
- Keep MonoBehaviours thin. Logic lives in plain C# classes/services so it can be tested
  in EditMode without a scene. Randomness takes an injected `System.Random` so tests
  are deterministic.
- Authored content (encounters, weapons, abilities, materials, recipes) is
  ScriptableObject data under `Assets/Resources/`, loaded by data — never hardcode a
  catalog in code. Add content without new UI code.
- Weapons: authored `WeaponDefinition`s hold a *pool* of possible abilities; generation
  rolls a rarity-sized subset each time. Shops/vendors must generate weapons via
  `WeaponGenerator.GenerateFromDefinition`/`GenerateFromRandomDefinition`, never the
  archetype `WeaponGenerator.Generate`.
- UI is a single Screen Space - Overlay canvas (UGUI). No world-space rendering;
  animation/VFX are UGUI tweens, not ParticleSystem/Animator.
- New visuals start as greybox/placeholder primitives. No final art before a mechanic
  is proven.
- Every asset and script has a committed `.meta` file. Binary assets go through Git LFS
  — check `.gitattributes` before adding a new binary type.
- Changing a serialized field or save-data shape: keep old saves loading, or call out
  the break explicitly.
