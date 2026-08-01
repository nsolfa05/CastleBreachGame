using UnityEditor;

/// <summary>
/// Custom inspector for WeaponSwitcher — groups its fields into collapsible
/// foldout sections (see FoldoutHeaderEditor). Editor-only; never included in
/// a build.
/// </summary>
[CustomEditor(typeof(WeaponSwitcher))]
public class WeaponSwitcherEditor : FoldoutHeaderEditor
{
}
