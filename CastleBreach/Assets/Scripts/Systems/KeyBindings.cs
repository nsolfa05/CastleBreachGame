using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of truth for every keyboard control (§2), rebindable by
/// the player and persisted in PlayerPrefs alongside GameSettings.
///
/// Every gameplay script asks this class "was this ACTION pressed" instead
/// of naming a physical key, so a rebind takes effect everywhere with no
/// further changes. Adding a new bindable action means adding one enum
/// entry plus its default here — nothing else needs touching.
///
/// Two kinds of binding:
/// - Fixed actions (the Action enum): movement, attack, menus, cancel.
/// - Indexed SLOT actions (weapon slot 1..N, build slot 1..N): deliberately
///   NOT enum entries, because the weapon list and BuildModeController's
///   Build Options list both grow as new content is added — per the
///   project's rule that new content shouldn't require code changes. Slots
///   are keyed by index and default to Digit1 + index.
///
/// Values are cached in memory after first read: movement is polled every
/// frame, and hitting PlayerPrefs that often would be wasteful.
/// </summary>
public static class KeyBindings
{
    public enum Action
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        Attack,
        WeaponMenu,
        BuildMenu,
        Cancel,
        Restart,
    }

    /// <summary>Digits 1-9 are the only sensible slot defaults, so that's the cap.</summary>
    public const int MaxSlots = 9;

    private const string PrefPrefix = "Bind_";

    private static readonly Dictionary<Action, Key> Defaults = new Dictionary<Action, Key>
    {
        { Action.MoveUp,     Key.W },
        { Action.MoveDown,   Key.S },
        { Action.MoveLeft,   Key.A },
        { Action.MoveRight,  Key.D },
        { Action.Attack,     Key.Space },
        { Action.WeaponMenu, Key.V },
        { Action.BuildMenu,  Key.B },
        { Action.Cancel,     Key.Escape },
        { Action.Restart,    Key.R },
    };

    private static readonly Dictionary<Action, string> Labels = new Dictionary<Action, string>
    {
        { Action.MoveUp,     "Move Up" },
        { Action.MoveDown,   "Move Down" },
        { Action.MoveLeft,   "Move Left" },
        { Action.MoveRight,  "Move Right" },
        { Action.Attack,     "Attack / Charge" },
        { Action.WeaponMenu, "Weapon Menu" },
        { Action.BuildMenu,  "Build Menu" },
        { Action.Cancel,     "Cancel / Close Menu" },
        { Action.Restart,    "Restart (after win/lose)" },
    };

    private static readonly Dictionary<string, Key> cache = new Dictionary<string, Key>();

    // ---- Fixed actions -------------------------------------------------

    public static Key Get(Action action) => GetStored(NameOf(action), Defaults[action]);

    public static void Set(Action action, Key key) => SetStored(NameOf(action), key);

    public static bool WasPressed(Action action) => KeyWasPressed(Get(action));

    public static bool IsPressed(Action action) => KeyIsPressed(Get(action));

    /// <summary>Needed by hold-to-charge weapons (ChargedWeapon), which fire on release.</summary>
    public static bool WasReleased(Action action) => KeyWasReleased(Get(action));

    public static string LabelOf(Action action) => Labels[action];

    // ---- Indexed slot actions ------------------------------------------

    public static Key GetWeaponSlot(int index) =>
        GetStored(WeaponSlotName(index), DefaultSlotKey(index));

    public static Key GetBuildSlot(int index) =>
        GetStored(BuildSlotName(index), DefaultSlotKey(index));

    public static void SetWeaponSlot(int index, Key key) => SetStored(WeaponSlotName(index), key);

    public static void SetBuildSlot(int index, Key key) => SetStored(BuildSlotName(index), key);

    public static bool WeaponSlotPressed(int index) => KeyWasPressed(GetWeaponSlot(index));

    public static bool BuildSlotPressed(int index) => KeyWasPressed(GetBuildSlot(index));

    private static Key DefaultSlotKey(int index) =>
        index >= 0 && index < MaxSlots ? Key.Digit1 + index : Key.None;

    private static string WeaponSlotName(int index) => $"WeaponSlot{index}";
    private static string BuildSlotName(int index) => $"BuildSlot{index}";

    // ---- Rebinding helpers ---------------------------------------------

    /// <summary>
    /// Assigns a key to an action, SWAPPING with whatever action already
    /// held that key. Swapping rather than clearing is deliberate: clearing
    /// would silently leave some other action unbound and unusable, which
    /// is a much worse surprise than two controls trading places.
    /// </summary>
    public static void Rebind(Action action, Key key)
    {
        foreach (Action other in Enum.GetValues(typeof(Action)))
        {
            if (other != action && Get(other) == key)
            {
                Set(other, Get(action));
                break;
            }
        }
        Set(action, key);
    }

    public static void ResetAllToDefaults()
    {
        foreach (Action action in Enum.GetValues(typeof(Action)))
            Set(action, Defaults[action]);

        for (int i = 0; i < MaxSlots; i++)
        {
            SetWeaponSlot(i, DefaultSlotKey(i));
            SetBuildSlot(i, DefaultSlotKey(i));
        }
    }

    /// <summary>
    /// Human-readable name for a key: "Digit1" reads as "1", "LeftShift" as
    /// "Left Shift". Used by both the rebinding UI and the controls list.
    /// </summary>
    public static string DisplayName(Key key)
    {
        if (key == Key.None) return "—";

        string raw = key.ToString();

        if (raw.StartsWith("Digit")) return raw.Substring(5);
        if (raw.StartsWith("Numpad")) return "Numpad " + raw.Substring(6);

        // Split camelCase into words: "LeftShift" -> "Left Shift".
        var result = new System.Text.StringBuilder(raw.Length + 4);
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                result.Append(' ');
            result.Append(raw[i]);
        }
        return result.ToString();
    }

    // ---- Storage --------------------------------------------------------

    private static string NameOf(Action action) => action.ToString();

    private static Key GetStored(string name, Key fallback)
    {
        if (cache.TryGetValue(name, out Key cached))
            return cached;

        string stored = PlayerPrefs.GetString(PrefPrefix + name, string.Empty);
        Key key = !string.IsNullOrEmpty(stored) && Enum.TryParse(stored, out Key parsed)
            ? parsed
            : fallback;

        cache[name] = key;
        return key;
    }

    private static void SetStored(string name, Key key)
    {
        cache[name] = key;
        PlayerPrefs.SetString(PrefPrefix + name, key.ToString());
    }

    private static bool KeyWasPressed(Key key)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || key == Key.None) return false;
        return keyboard[key].wasPressedThisFrame;
    }

    private static bool KeyIsPressed(Key key)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || key == Key.None) return false;
        return keyboard[key].isPressed;
    }

    private static bool KeyWasReleased(Key key)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || key == Key.None) return false;
        return keyboard[key].wasReleasedThisFrame;
    }
}
