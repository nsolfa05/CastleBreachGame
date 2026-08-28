using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// One rebindable control in the Settings menu: a label ("Move Up") and a
/// button showing its current key. Click the button, press any key, done.
///
/// Handles both kinds of binding KeyBindings exposes — a fixed Action, or
/// an indexed weapon/build slot — chosen by the Kind field, so the same
/// prefab serves every row.
///
/// Only one row listens at a time (enforced by the static `listeningRow`):
/// without that, clicking a second row while the first was still waiting
/// would bind the same keypress to both.
/// </summary>
public class KeyRebindRow : MonoBehaviour
{
    public enum BindingKind { Action, WeaponSlot, BuildSlot }

    [SerializeField] private BindingKind kind = BindingKind.Action;

    [Tooltip("Which control this row rebinds. Only used when Kind is Action.")]
    [SerializeField] private KeyBindings.Action action = KeyBindings.Action.MoveUp;

    [Tooltip("Which slot this row rebinds, 0-based (0 = the first weapon/building). Only used when Kind is WeaponSlot or BuildSlot.")]
    [SerializeField] private int slotIndex;

    [Tooltip("Optional override for the row's label. Leave blank to use the automatic name.")]
    [SerializeField] private string labelOverride = "";

    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private Button rebindButton;

    private static KeyRebindRow listeningRow;

    /// <summary>
    /// Sets what this row binds. Used by KeyRebindMenu when it spawns rows
    /// from a prefab; hand-placed rows just fill the Inspector fields
    /// instead and never call this.
    /// </summary>
    public void Configure(BindingKind rowKind, KeyBindings.Action rowAction, int rowSlotIndex)
    {
        kind = rowKind;
        action = rowAction;
        slotIndex = rowSlotIndex;
        Refresh();
    }

    private void Start()
    {
        if (rebindButton != null)
            rebindButton.onClick.AddListener(BeginListening);

        Refresh();
    }

    private void OnDestroy()
    {
        if (listeningRow == this) listeningRow = null;
    }

    /// <summary>Called by the Button, or directly if you wire it yourself.</summary>
    public void BeginListening()
    {
        if (listeningRow != null && listeningRow != this)
            listeningRow.CancelListening();

        listeningRow = this;
        if (keyText != null) keyText.text = "Press a key…";
    }

    private void CancelListening()
    {
        if (listeningRow == this) listeningRow = null;
        Refresh();
    }

    private void Update()
    {
        if (listeningRow != this) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Escape always means "never mind" rather than "bind Escape" — a
        // rebind UI with no way out is a trap, and that matters more here
        // than being able to move the Cancel binding onto some other key.
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelListening();
            return;
        }

        foreach (var control in keyboard.allKeys)
        {
            if (!control.wasPressedThisFrame) continue;

            Assign(control.keyCode);
            listeningRow = null;
            RefreshAllRows();
            return;
        }
    }

    private void Assign(Key key)
    {
        switch (kind)
        {
            case BindingKind.Action:
                // Rebind (not Set) so the displaced control swaps rather
                // than silently ending up unbound.
                KeyBindings.Rebind(action, key);
                break;
            case BindingKind.WeaponSlot:
                KeyBindings.SetWeaponSlot(slotIndex, key);
                break;
            case BindingKind.BuildSlot:
                KeyBindings.SetBuildSlot(slotIndex, key);
                break;
        }
    }

    /// <summary>
    /// Refreshes every row, not just this one: Rebind can swap a key away
    /// from another action, and that row needs to stop showing the key it
    /// no longer owns.
    /// </summary>
    private static void RefreshAllRows()
    {
        foreach (var row in FindObjectsByType<KeyRebindRow>(FindObjectsSortMode.None))
            row.Refresh();
    }

    public void Refresh()
    {
        if (labelText != null)
            labelText.text = string.IsNullOrWhiteSpace(labelOverride) ? AutoLabel() : labelOverride;

        if (keyText != null)
            keyText.text = KeyBindings.DisplayName(CurrentKey());
    }

    private string AutoLabel() => kind switch
    {
        BindingKind.WeaponSlot => $"Weapon {slotIndex + 1}",
        BindingKind.BuildSlot => $"Building {slotIndex + 1}",
        _ => KeyBindings.LabelOf(action),
    };

    private Key CurrentKey() => kind switch
    {
        BindingKind.WeaponSlot => KeyBindings.GetWeaponSlot(slotIndex),
        BindingKind.BuildSlot => KeyBindings.GetBuildSlot(slotIndex),
        _ => KeyBindings.Get(action),
    };
}
