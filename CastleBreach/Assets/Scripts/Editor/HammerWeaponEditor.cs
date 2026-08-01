using UnityEditor;

/// <summary>
/// Custom inspector for HammerWeapon — groups its fields into collapsible
/// foldout sections (see FoldoutHeaderEditor). Editor-only; never included in
/// a build.
/// </summary>
[CustomEditor(typeof(HammerWeapon))]
public class HammerWeaponEditor : FoldoutHeaderEditor
{
}
