using System;

/// <summary>
/// Overrides a serialized field's displayed label in a FoldoutHeaderEditor-
/// based Inspector, independent of its C# field name — e.g. a field named
/// `weight` can show as "Weight Impact Lvl" without renaming the field
/// itself (which would mean re-touching every place that reads it, and
/// risking losing already-tuned serialized values without a matching
/// FormerlySerializedAs). Plain runtime-safe attribute (no UnityEditor
/// dependency), so it can sit on a field in any script, not just Editor code.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class InspectorLabelAttribute : Attribute
{
    public readonly string Label;

    public InspectorLabelAttribute(string label)
    {
        Label = label;
    }
}
