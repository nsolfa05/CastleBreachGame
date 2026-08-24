using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the campaign trail as a curved dashed line through an ordered
/// list of CampaignNodes: a Catmull-Rom spline (Unity has no built-in
/// spline component) feeding a LineRenderer, with a small procedurally
/// generated tiling dash texture for the dashed look — no external image
/// asset needed, matching this project's placeholder-first approach.
///
/// Every trail-look setting (dash count, dash chunkiness, line width)
/// lives here on one component rather than being split across this script
/// and the Line Renderer's own fields — this script drives Line Renderer's
/// width directly, so there's exactly one place to look when tuning how
/// the trail appears.
///
/// [ExecuteAlways] so dragging a node around in the Scene view redraws the
/// trail live in the Editor, not just at runtime — that's what makes
/// "drag a node, watch the trail follow" actually work while laying the
/// campaign out by hand, rather than only updating once you press Play.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class CampaignTrail : MonoBehaviour
{
    [Tooltip("Nodes in trail order, left to right — drag Transforms in, in order.")]
    [SerializeField] private List<Transform> nodesInOrder = new List<Transform>();

    [Tooltip("Interpolated points per segment between two nodes — higher = smoother curve.")]
    [SerializeField] private int segmentsPerNode = 20;

    [Tooltip("How many dashes to spread evenly across the whole trail, regardless of how long it is — dragging nodes further apart makes each dash longer, not more numerous.")]
    [SerializeField, Range(2, 60)] private int dashCount = 15;

    [Tooltip("How much of each dash+gap cycle is solid line vs. empty gap. 0.5 = even dashes. Higher = longer dashes/shorter gaps. Lower = shorter dashes/longer gaps.")]
    [SerializeField, Range(0.05f, 0.95f)] private float dashToGapRatio = 0.5f;

    [Tooltip("Line thickness. Drives the Line Renderer's own width — no need to edit that component directly.")]
    [SerializeField] private float width = 0.1f;

    private LineRenderer line;
    private Vector3[] lastPositions;
    private bool loggedMissingNodeWarning;

    private void OnEnable()
    {
        line = GetComponent<LineRenderer>();
        line.textureMode = LineTextureMode.Tile;
        // sharedMaterial, not material: this material is never shared with
        // anything else anyway (freshly built just for this line), and
        // accessing .material in edit mode instantiates a new copy every
        // time it's touched, leaking orphaned materials into the scene.
        line.sharedMaterial = BuildDashMaterial();
        Redraw();
    }

    private void Update()
    {
        if (NodesMoved())
            Redraw();
    }

    // Fires automatically whenever a field changes in the Inspector (Editor
    // only) — rebuilds the dash texture and redraws immediately, so every
    // slider here updates live instead of only reacting the next time a
    // node happens to move.
    private void OnValidate()
    {
        line = GetComponent<LineRenderer>();
        line.sharedMaterial = BuildDashMaterial();
        Redraw();
    }

    private bool NodesMoved()
    {
        if (lastPositions == null || lastPositions.Length != nodesInOrder.Count)
            return true;

        for (int i = 0; i < nodesInOrder.Count; i++)
            if (nodesInOrder[i] == null || lastPositions[i] != nodesInOrder[i].position)
                return true;

        return false;
    }

    private void Redraw()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.widthMultiplier = width;

        bool hasUnassignedNode = false;
        foreach (var node in nodesInOrder)
            if (node == null) hasUnassignedNode = true;

        if (nodesInOrder.Count < 2 || hasUnassignedNode)
        {
            line.positionCount = 0;
            if (hasUnassignedNode && !loggedMissingNodeWarning)
            {
                Debug.LogWarning(
                    "CampaignTrail: one or more slots in Nodes In Order shows \"None\" — " +
                    "fill every slot with a node before the trail can draw.", this);
                loggedMissingNodeWarning = true;
            }
            return;
        }
        loggedMissingNodeWarning = false;

        lastPositions = new Vector3[nodesInOrder.Count];
        for (int i = 0; i < nodesInOrder.Count; i++)
            lastPositions[i] = nodesInOrder[i].position;

        var points = new List<Vector3>();
        for (int i = 0; i < nodesInOrder.Count - 1; i++)
        {
            Vector3 p0 = nodesInOrder[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = nodesInOrder[i].position;
            Vector3 p2 = nodesInOrder[i + 1].position;
            Vector3 p3 = nodesInOrder[Mathf.Min(i + 2, nodesInOrder.Count - 1)].position;

            for (int s = 0; s < segmentsPerNode; s++)
            {
                float t = s / (float)segmentsPerNode;
                points.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        points.Add(nodesInOrder[nodesInOrder.Count - 1].position);

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        // dashCount dashes across the whole line, however long it is right
        // now — tiling the texture exactly dashCount times does that
        // directly, no length math needed (unlike the old world-unit-sized
        // dash/gap approach, where dash count was a side effect of total
        // trail length instead of something you set).
        line.sharedMaterial.mainTextureScale = new Vector2(dashCount, 1f);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1
            + (p2 - p0) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (3f * p1 - 3f * p2 + p3 - p0) * t3
        );
    }

    // Instance method, not static — needs dashToGapRatio to bake the actual
    // split into the texture. Resolution is fixed at 64 pixels, enough to
    // represent the ratio without visibly blocky edges given Point filtering.
    private Material BuildDashMaterial()
    {
        const int resolution = 64;
        int dashPixels = Mathf.Clamp(Mathf.RoundToInt(resolution * dashToGapRatio), 1, resolution - 1);

        var texture = new Texture2D(resolution, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };

        var pixels = new Color[resolution];
        for (int i = 0; i < resolution; i++)
            pixels[i] = i < dashPixels ? Color.white : new Color(1, 1, 1, 0);
        texture.SetPixels(pixels);
        texture.Apply();

        return new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
    }
}
