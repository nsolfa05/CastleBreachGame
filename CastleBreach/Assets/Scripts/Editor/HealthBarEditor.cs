using UnityEditor;

/// <summary>
/// Custom inspector for HealthBar — groups its fields (wiring/sizing, and the
/// new Hide Until Damaged section) into collapsible foldout sections (see
/// FoldoutHeaderEditor). Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(HealthBar))]
public class HealthBarEditor : FoldoutHeaderEditor
{
}
