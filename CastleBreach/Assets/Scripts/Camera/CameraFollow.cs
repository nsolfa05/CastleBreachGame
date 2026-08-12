using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Design doc §3.7: the camera always follows the player; the mouse wheel
/// zooms out far enough to see the whole 40x30 map; and follow speed scales
/// with zoom — tight tracking when zoomed in, loose/slow when zoomed out.
/// Everything is tunable in the Inspector, including the speed curve.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Zoom (§3.7 — tunable)")]
    [Tooltip("Orthographic size when fully zoomed IN (smaller = closer).")]
    [SerializeField] private float minZoom = 5f;

    [Tooltip("Orthographic size when fully zoomed OUT. 16 shows all 30 rows with a small margin.")]
    [SerializeField] private float maxZoom = 16f;

    [Tooltip("How much one mouse-wheel notch changes the zoom.")]
    [SerializeField] private float zoomStep = 1f;

    [Tooltip("How quickly the camera eases to the new zoom level.")]
    [SerializeField] private float zoomSmoothing = 8f;

    [Header("Follow (§3.7 — follow speed scales with zoom)")]
    [Tooltip("X axis: 0 = fully zoomed in, 1 = fully zoomed out. Y axis: follow speed (higher = tighter tracking).")]
    [SerializeField] private AnimationCurve followSpeedByZoom = AnimationCurve.Linear(0f, 10f, 1f, 2.5f);

    private Camera cam;
    private float targetZoom;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            targetZoom = Mathf.Clamp(targetZoom - Mathf.Sign(scroll) * zoomStep, minZoom, maxZoom);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // unscaledDeltaTime so the camera still settles on the win/lose screen (timeScale 0).
        float dt = Time.unscaledDeltaTime;

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 1f - Mathf.Exp(-zoomSmoothing * dt));

        float zoomT = Mathf.InverseLerp(minZoom, maxZoom, cam.orthographicSize);
        float followSpeed = followSpeedByZoom.Evaluate(zoomT);

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        Vector3 smoothed = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * dt));
        transform.position = SnapToScreenPixel(smoothed);
    }

    /// <summary>
    /// Rounds x/y to the nearest on-screen pixel at the current zoom. The
    /// Lerp above almost never lands the camera exactly on the art's pixel
    /// grid, so adjacent Tilemap tiles (each their own quad) round their
    /// shared edge independently and leave a hairline gap that shows as a
    /// shimmering seam while the camera eases toward the player. Snapping
    /// only the camera (not individual sprites) is enough to fix that,
    /// since static tiles don't have seams with themselves — only with
    /// their neighbors, which this keeps aligned. Uses the CURRENT
    /// orthographic size rather than the art's fixed Pixels Per Unit, so it
    /// stays correct through the mouse-wheel zoom above instead of only
    /// being exact at one specific zoom level.
    /// </summary>
    private Vector3 SnapToScreenPixel(Vector3 position)
    {
        float worldUnitsPerScreenPixel = 2f * cam.orthographicSize / Screen.height;
        position.x = Mathf.Round(position.x / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
        position.y = Mathf.Round(position.y / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
        return position;
    }
}
