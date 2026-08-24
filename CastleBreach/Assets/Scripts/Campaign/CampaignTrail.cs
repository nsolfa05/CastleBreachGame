using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the campaign trail as a curved dashed line through an ordered
/// list of CampaignNodes: a Catmull-Rom spline (Unity has no built-in
/// spline component) feeding a LineRenderer, with a small procedurally
/// generated tiling dash texture for the dashed look — no external image
/// asset needed, matching this project's placeholder-first approach.
///
/// Every trail-look setting (dash length, gap length, line width) lives
/// here on one component rather than being split across this script and
/// the Line Renderer's own fields — this script drives Line Renderer's
/// width directly, so there's exactly one place to look when tuning how
/// the trail appears. Dash Length and Gap Length are independent: each
/// dash keeps its own physical world-space size no matter what the other
/// is set to or how long the trail gets — only the gap between dashes
/// changes.
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

    [Tooltip("Optional per-segment bend point: element i bends the curve between Nodes In Order[i] and [i+1] toward this Transform. Leave a slot empty (None) for the default smooth automatic curve through that segment instead.")]
    [SerializeField] private List<Transform> curveHandles = new List<Transform>();

    [Tooltip("Interpolated points per segment between two nodes — higher = smoother curve.")]
    [SerializeField] private int segmentsPerNode = 20;

    [Tooltip("World-space length of each solid dash. Stays this physical size regardless of Gap Length or how long the trail is.")]
    [SerializeField] private float dashLength = 0.5f;

    [Tooltip("World-space length of the empty gap between dashes. Bigger = more spaced-out dashes; smaller = tighter/denser dashes. Doesn't change Dash Length's own size.")]
    [SerializeField] private float gapLength = 0.5f;

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
        float totalLength = 0f;
        for (int i = 0; i < nodesInOrder.Count - 1; i++)
        {
            Vector3 p1 = nodesInOrder[i].position;
            Vector3 p2 = nodesInOrder[i + 1].position;
            Transform handle = i < curveHandles.Count ? curveHandles[i] : null;

            for (int s = 0; s < segmentsPerNode; s++)
            {
                float t = s / (float)segmentsPerNode;
                Vector3 point;
                if (handle != null)
                {
                    // Manually bent segment: quadratic Bezier through the
                    // handle instead of the automatic spline, so dragging
                    // the handle directly controls how this one segment
                    // curves, independent of every other segment.
                    point = QuadraticBezier(p1, handle.position, p2, t);
                }
                else
                {
                    Vector3 p0 = nodesInOrder[Mathf.Max(i - 1, 0)].position;
                    Vector3 p3 = nodesInOrder[Mathf.Min(i + 2, nodesInOrder.Count - 1)].position;
                    point = CatmullRom(p0, p1, p2, p3, t);
                }
                if (points.Count > 0) totalLength += Vector3.Distance(points[points.Count - 1], point);
                points.Add(point);
            }
        }
        points.Add(nodesInOrder[nodesInOrder.Count - 1].position);
        totalLength += Vector3.Distance(points[points.Count - 2], points[points.Count - 1]);

        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        // One full texture repeat = exactly (dashLength + gapLength) world
        // units of trail, so each dash keeps its set physical size no
        // matter how gapLength or the trail's total length change — only
        // the number of repeats that fit changes, not the dash itself.
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

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 control, Vector3 b, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * control + t * t * b;
    }

    // Instance method, not static — needs dashLength/gapLength to bake the
    // actual ratio into the texture. Resolution is fixed at 64 pixels,
    // enough to represent most ratios without visibly blocky edges given
    // Point filtering anyway.
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

        // Clone the shader from one of the node sprites rather than
        // guessing a shader name (e.g. "Sprites/Default") that may not
        // resolve the same way in every render pipeline setup — this
        // guarantees compatibility with whatever this project actually
        // uses, since it's the exact shader already rendering correctly
        // on the nodes themselves. Falls back to Sprites/Default only if
        // no node sprite exists yet to copy from.
        Material baseMaterial = FindReferenceSpriteMaterial();
        Material material = baseMaterial != null
            ? new Material(baseMaterial)
            : new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = texture;
        material.color = Color.white;
        return material;
    }

    private Material FindReferenceSpriteMaterial()
    {
        foreach (var node in nodesInOrder)
        {
            if (node == null) continue;
            var renderer = node.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial;
        }
        return null;
    }
}
