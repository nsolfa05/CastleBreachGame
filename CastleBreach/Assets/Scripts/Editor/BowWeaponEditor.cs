using UnityEditor;

/// <summary>
/// Custom inspector for BowWeapon — groups its fields into collapsible
/// foldout sections (see FoldoutHeaderEditor). Editor-only; never included in
/// a build.
/// </summary>
[CustomEditor(typeof(BowWeapon))]
public class BowWeaponEditor : FoldoutHeaderEditor
{
}
