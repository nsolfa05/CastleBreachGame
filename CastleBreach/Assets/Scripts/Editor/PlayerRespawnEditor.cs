using UnityEditor;

/// <summary>
/// Custom inspector for PlayerRespawn — groups its fields (Respawn, Gold loss
/// on death) into collapsible foldout sections (see FoldoutHeaderEditor).
/// Editor-only; never included in a build.
/// </summary>
[CustomEditor(typeof(PlayerRespawn))]
public class PlayerRespawnEditor : FoldoutHeaderEditor
{
}
