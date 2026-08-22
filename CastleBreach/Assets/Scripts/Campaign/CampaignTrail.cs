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

    [Tooltip("World-space length of one dash+gap pair.")]
    [SerializeField] private float dashWorldSize = 1f;

    private LineRenderer line;
    private Vector3[] lastPositions;

    private void OnEnable()
    {
        line = GetComponent<LineRenderer>();
        line.textureMode = LineTextureMode.Tile;
        line.material = BuildDashMaterial();
        Redraw();
    }

    private void Update()
    {
        if (NodesMoved())
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
        if (nodesInOrder.Count < 2)
        {
            line.positionCount = 0;
            return;
        }

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
        line.material.mainTextureScale = new Vector2(totalLength / Mathf.Max(dashWorldSize, 0.01f), 1f);
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

    private static Material BuildDashMaterial()
    {
        var texture = new Texture2D(4, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        texture.SetPixels(new[] { Color.white, Color.white, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0) });
        texture.Apply();

        return new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
    }
}
