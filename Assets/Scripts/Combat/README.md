# Combat Visuals System - Into the Void

This system provides a data-driven, scalable architecture for character visuals in "Into the Void". It bridges combat logic (triggered via `EncounterDirector`) with high-fidelity HDRP VFX and Lighting.

## Core Components

### 1. AbilityVisualData (ScriptableObject)
Located in `Assets/Scripts/Combat/Data/`. These assets define the visual sequence for a specific ability.
- **Animation Trigger**: The trigger name in the Animator controller.
- **VFX Slot Name**: The named slot on the character prefab to trigger the effect.
- **VFX Events**: String events sent to the VFX Graph (e.g., `OnChargeStart`, `OnImpactBurst`).
- **Parameter Mappings**: Allows overriding VFX Graph parameters (floats, colors, etc.) per ability.
- **Timing**: Use `Delay Before Completion` for timed effects, or set to `0` to wait for an Animation Event.

### 2. CharacterVisualController (MonoBehaviour)
Located in `Assets/Scripts/Combat/Visuals/`. This component lives on the character prefab.
- **Slotted System**: Instead of hardcoded references, use the `vfxSlots` and `lightSlots` lists to map strings (e.g., "RightHand") to specific components.
- **Dictionary Lookup**: Automatically builds a lookup for abilities assigned in the `characterAbilities` list.

## Workflow: Adding a New Ability

1. **Create Data Asset**: Create a new `AbilityVisualData` asset in `Assets/Data/Abilities/`.
2. **Configure VFX Graph**: Ensure your VFX Graph has the necessary events and exposed parameters.
3. **Set Up Slots**: On the character prefab, add the necessary `VisualEffect` or `Light` components to the `vfxSlots` or `lightSlots` lists.
4. **Assign Ability**: Add the new `AbilityVisualData` asset to the `Character Visual Controller`'s `characterAbilities` list.
5. **Animation Events**: Ensure the animation clip calls `AnimEvent_OnAbilityImpact` at the frame where the effect should "land".

## Mandatory Animation Hooks

All character animations must use the following hook for synchronization:
- `AnimEvent_OnAbilityImpact()`: Triggers impact VFX, lighting flashes, and signals the combat system that the visual sequence is complete.

*Legacy Support*: `AnimEvent_OnJusticesEdgeImpact()` is maintained for Zaia's original animations but is marked as Obsolete.

## Technical Standards

- **Zero Frame-Hitch**: We use a slotted system instead of runtime asset swapping to avoid re-initializing VFX Graph buffers.
- **IX-Node Stabilization**: Visuals should feel crisp and "stabilized". Use the parameter mappings to fine-tune intensities and timings to match the character's lore.
- **Production-Ready**: No hardcoded string literals are used for logic; all triggers and event names are defined in the Data Assets.
