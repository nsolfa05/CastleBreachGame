using UnityEditor;

/// <summary>
/// Custom inspector for FireStaffWeapon — groups its fields into collapsible
/// foldout sections (see FoldoutHeaderEditor). Editor-only; never included in
/// a build.
/// </summary>
[CustomEditor(typeof(FireStaffWeapon))]
public class FireStaffWeaponEditor : FoldoutHeaderEditor
{
}
