using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Fills a TMP_Text with a readable summary of the current controls,
/// reading live from KeyBindings so it always reflects rebinds rather than
/// hardcoding a list that would silently drift out of date.
///
/// Refreshes in OnEnable, so a panel toggled open after rebinding shows
/// the new keys. Mouse controls are listed too — they aren't rebindable
/// (aiming reads the pointer position directly, see PlayerAim), but a
/// controls screen that omitted them would be misleading.
/// </summary>
public class ControlsDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;

    [Tooltip("Include the mouse controls section. Off if you list those separately.")]
    [SerializeField] private bool includeMouse = true;

    [Tooltip("Include the notes explaining how the weapon/build menus work.")]
    [SerializeField] private bool includeNotes = true;

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        if (targetText == null) return;
        targetText.text = BuildText();
    }

    private string BuildText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("<b>MOVEMENT</b>");
        Line(sb, KeyBindings.Action.MoveUp);
        Line(sb, KeyBindings.Action.MoveDown);
        Line(sb, KeyBindings.Action.MoveLeft);
        Line(sb, KeyBindings.Action.MoveRight);

        sb.AppendLine();
        sb.AppendLine("<b>COMBAT</b>");
        Line(sb, KeyBindings.Action.Attack);
        Line(sb, KeyBindings.Action.WeaponMenu);

        sb.AppendLine();
        sb.AppendLine("<b>BUILDING</b>");
        Line(sb, KeyBindings.Action.BuildMenu);

        sb.AppendLine();
        sb.AppendLine("<b>GENERAL</b>");
        Line(sb, KeyBindings.Action.Cancel);
        Line(sb, KeyBindings.Action.Restart);

        if (includeMouse)
        {
            sb.AppendLine();
            sb.AppendLine("<b>MOUSE</b>");
            sb.AppendLine("Aim — Move Mouse");
            sb.AppendLine("Place Structure — Left Click");
            sb.AppendLine("Cancel Building — Right Click");
            sb.AppendLine("Zoom — Scroll Wheel");
        }

        if (includeNotes)
        {
            sb.AppendLine();
            sb.AppendLine("<b>HOW IT WORKS</b>");
            sb.AppendLine($"Press {Key(KeyBindings.Action.WeaponMenu)} to open the weapon menu, then a number key to equip. The menu stays open so you can keep switching.");
            sb.AppendLine($"Press {Key(KeyBindings.Action.BuildMenu)} to carry the last structure you used, or a number key to pick a specific one. Left click places it.");
            sb.AppendLine("The weapon and build menus can't be open at the same time.");
        }

        return sb.ToString();
    }

    private static void Line(StringBuilder sb, KeyBindings.Action action) =>
        sb.AppendLine($"{KeyBindings.LabelOf(action)} — {Key(action)}");

    private static string Key(KeyBindings.Action action) =>
        KeyBindings.DisplayName(KeyBindings.Get(action));
}
