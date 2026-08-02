using UnityEditor;

/// <summary>
/// Custom inspector for DeathEffect — groups its fields (Body, Particle
/// Burst) into collapsible foldout sections (see FoldoutHeaderEditor).
/// Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(DeathEffect))]
public class DeathEffectEditor : FoldoutHeaderEditor
{
}
