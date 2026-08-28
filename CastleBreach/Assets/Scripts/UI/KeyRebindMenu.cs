using System;
using UnityEngine;

/// <summary>
/// Builds the whole rebinding list in the Settings menu: one KeyRebindRow
/// per control, spawned from a single prefab into a container.
///
/// Generated rather than hand-placed because there are ~19 rows once
/// weapon and building slots are counted, and because that count changes
/// whenever a weapon or structure is added — hand-placed rows would need
/// maintaining every time, which is exactly the kind of manual upkeep this
/// project avoids elsewhere (MonsterDefinition assets, Build Options list).
///
/// Slot counts are Inspector fields rather than read from WeaponSwitcher /
/// BuildModeController, because those live in the Game scene and this menu
/// lives in Settings — there's no instance to ask. Bump them when you add
/// a weapon or structure.
/// </summary>
public class KeyRebindMenu : MonoBehaviour
{
    [Tooltip("Prefab with a KeyRebindRow on it (label + key text + button).")]
    [SerializeField] private KeyRebindRow rowPrefab;

    [Tooltip("Parent the spawned rows go under — give it a Vertical Layout Group.")]
    [SerializeField] private Transform rowContainer;

    [Header("How many slot rows to show")]
    [Tooltip("Number of weapons the player can carry — currently 4 (Sword, Bow, Hammer, Fire Staff).")]
    [SerializeField, Range(0, KeyBindings.MaxSlots)] private int weaponSlotCount = 4;

    [Tooltip("Number of entries in BuildModeController's Build Options list.")]
    [SerializeField, Range(0, KeyBindings.MaxSlots)] private int buildSlotCount = 6;

    private void Start() => Rebuild();

    private void Rebuild()
    {
        if (rowPrefab == null || rowContainer == null)
        {
            Debug.LogError("KeyRebindMenu: assign Row Prefab and Row Container in the Inspector.", this);
            return;
        }

        for (int i = rowContainer.childCount - 1; i >= 0; i--)
            Destroy(rowContainer.GetChild(i).gameObject);

        foreach (KeyBindings.Action action in Enum.GetValues(typeof(KeyBindings.Action)))
            SpawnRow(KeyRebindRow.BindingKind.Action, action, 0);

        for (int i = 0; i < weaponSlotCount; i++)
            SpawnRow(KeyRebindRow.BindingKind.WeaponSlot, default, i);

        for (int i = 0; i < buildSlotCount; i++)
            SpawnRow(KeyRebindRow.BindingKind.BuildSlot, default, i);
    }

    private void SpawnRow(KeyRebindRow.BindingKind kind, KeyBindings.Action action, int slotIndex)
    {
        KeyRebindRow row = Instantiate(rowPrefab, rowContainer);
        row.Configure(kind, action, slotIndex);
    }

    /// <summary>Wire a "Reset Controls" Button's On Click to this.</summary>
    public void OnResetToDefaultsPressed()
    {
        KeyBindings.ResetAllToDefaults();
        foreach (var row in FindObjectsByType<KeyRebindRow>(FindObjectsSortMode.None))
            row.Refresh();
    }
}
