using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the campaign trail as a curved dashed line through an ordered
/// list of CampaignNodes: a Catmull-Rom spline (Unity has no built-in
/// spline component) feeding a LineRenderer, with a small procedurally
/// generated tiling dash texture for the dashed look — no external image
/// asset needed, matching this project's placeholder-first approach.
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

    [Tooltip("World-space length of one visible dash. Smaller relative to Gap Length = shorter, more spaced-out dashes.")]
    [SerializeField] private float dashLength = 0.5f;

    [Tooltip("World-space length of the gap between dashes. Larger Dash+Gap together = fewer dashes over the same trail length.")]
    [SerializeField] private float gapLength = 0.5f;

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
    // only) — rebuilds the dash texture and redraws immediately, so tuning
    // Dash Length/Gap Length/Segments Per Node updates live instead of only
    // reacting the next time a node happens to move.
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
        float totalLength = 0f;
        for (int i = 0; i < nodesInOrder.Count - 1; i++)
        {
            Vector3 p0 = nodesInOrder[Mathf.Max(i - 1, 0)].position;
            Vector3 p1 = nodesInOrder[i].position;
            Vector3 p2 = nodesInOrder[i + 1].position;
            Vector3 p3 = nodesInOrder[Mathf.Min(i + 2, nodesInOrder.Count - 1)].position;

            for (int s = 0; s < segmentsPerNode; s++)
            {
                float t = s / (float)segmentsPerNode;
                Vector3 point = CatmullRom(p0, p1, p2, p3, t);
                if (points.Count > 0) totalLength += Vector3.Distance(points[points.Count - 1], point);
                points.Add(point);
            }
        }
        points.Add(nodesInOrder[nodesInOrder.Count - 1].position);
        totalLength += Vector3.Distance(points[points.Count - 2], points[points.Count - 1]);

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        float pairLength = Mathf.Max(dashLength + gapLength, 0.01f);
        line.sharedMaterial.mainTextureScale = new Vector2(totalLength / pairLength, 1f);
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

    // Instance method now, not static — needs dashLength/gapLength to bake
    // the actual dash:gap ratio into the texture (used to be a fixed 50/50
    // split). Resolution is fixed at 64 pixels, enough to represent most
    // ratios without visibly blocky edges given Point filtering anyway.
    private Material BuildDashMaterial()
    {
        const int resolution = 64;
        float pairLength = Mathf.Max(dashLength + gapLength, 0.01f);
        int dashPixels = Mathf.Clamp(Mathf.RoundToInt(resolution * dashLength / pairLength), 1, resolution - 1);

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
