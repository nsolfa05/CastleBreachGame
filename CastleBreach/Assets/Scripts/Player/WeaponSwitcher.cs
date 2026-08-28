using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns which of the player's weapons (Sword, Bow, Hammer, Fire Staff — Guide
/// 11b) is currently active, exactly one at a time. Mirrors
/// BuildModeController's B-key convention: press V to open the weapon-select
/// menu, then any number key (1-4) equips that weapon WITHOUT closing the
/// menu — press another number to switch again, as many times as you like.
/// Only Esc (or pressing V again) closes it.
///
/// Number keys are otherwise BuildModeController's — the menu only reads them
/// while actually open (selecting), and each side ignores its own open-key
/// while the other's menu is open (see SelectingWeapon / BuildModeController.
/// BuildingActive) so a stray number press can never fire both at once.
/// </summary>
// Forced to run AFTER BuildModeController every frame — Unity's default order
// between two unrelated scripts is unspecified, and the two menus racing was
// a real bug: on the exact frame a shared number key both equips a weapon
// HERE and closes this menu (clearing SelectingWeapon within this same
// Update call), BuildModeController's own guard — if it happened to run
// AFTER this script that frame — would read the already-cleared flag and
// process that same key too, briefly also popping a build ghost (e.g. "V
// then 2"). Running strictly after means BuildModeController always reads
// SelectingWeapon as it stood at the end of the PREVIOUS frame, which is
// stable and race-free for its check.
[DefaultExecutionOrder(100)]
public class WeaponSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class WeaponSlot
    {
        [Tooltip("Shown in a future weapon-select UI; pick something readable, e.g. \"Bow\".")]
        public string displayName = "Weapon";

        [Tooltip("The weapon's own component on this object (PlayerAttack, BowWeapon, HammerWeapon, FireStaffWeapon) — WeaponSwitcher enables exactly one at a time.")]
        public Behaviour component;
    }

    /// <summary>True while the weapon-select menu is open — mirrors BuildModeController.BuildingActive.</summary>
    public static bool SelectingWeapon { get; private set; }

    [Header("Weapons (1-4, in this order — press V to select)")]
    [SerializeField] private List<WeaponSlot> weapons = new List<WeaponSlot>();

    private bool selecting;
    private int equippedIndex = -1;
    private bool combatEnabled = true;

    private void Awake()
    {
        // Statics survive a scene reload — make sure a restart never starts
        // with a stale "still selecting" flag (same reasoning as BuildModeController).
        SelectingWeapon = false;
    }

    private void Start()
    {
        if (weapons.Count > 0) Equip(0);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !combatEnabled) return;

        if (KeyBindings.WasPressed(KeyBindings.Action.WeaponMenu) && !BuildModeController.BuildingActive)
        {
            if (selecting) StopSelecting();
            else StartSelecting();
        }

        if (!selecting) return;

        if (KeyBindings.WasPressed(KeyBindings.Action.Cancel))
        {
            StopSelecting();
            return;
        }

        for (int i = 0; i < weapons.Count && i < KeyBindings.MaxSlots; i++)
        {
            if (KeyBindings.WeaponSlotPressed(i))
            {
                Equip(i); // stays in the menu — press another number to switch again, Esc/V to close
                break;
            }
        }
    }

    private void StartSelecting()
    {
        selecting = true;
        SelectingWeapon = true;
    }

    private void StopSelecting()
    {
        selecting = false;
        SelectingWeapon = false;
    }

    private void Equip(int index)
    {
        if (index < 0 || index >= weapons.Count || weapons[index].component == null) return;
        for (int i = 0; i < weapons.Count; i++)
            if (weapons[i].component != null) weapons[i].component.enabled = i == index;
        equippedIndex = index;
    }

    /// <summary>
    /// Called by PlayerRespawn instead of touching any one weapon directly.
    /// false disables every weapon (and force-closes the select menu) for
    /// death; true re-equips whichever weapon was active before.
    /// </summary>
    public void SetCombatEnabled(bool active)
    {
        combatEnabled = active;
        if (active)
        {
            if (equippedIndex >= 0) Equip(equippedIndex);
        }
        else
        {
            StopSelecting();
            foreach (var slot in weapons)
                if (slot.component != null) slot.component.enabled = false;
        }
    }
}
