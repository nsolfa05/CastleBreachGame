using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Design doc §3.7: the camera always follows the player; the mouse wheel
/// zooms out far enough to see the whole 40x30 map; and follow speed scales
/// with zoom — tight tracking when zoomed in, loose/slow when zoomed out.
/// Everything is tunable in the Inspector, including the speed curve.
///
/// Smoothness notes (these three things were the difference between
/// "jittery" and "glides"):
/// - Zoom is MULTIPLICATIVE, not additive, and is smoothed in log space.
///   Adding a flat amount to orthographicSize makes each wheel notch feel
///   different depending on the current zoom (+1 is a 20% change at size 5
///   but 6.7% at size 15); scaling by a factor makes every notch feel
///   identical, and easing the logarithm keeps the motion perceptually
///   even rather than fast-then-crawling.
/// - Pixel Snap defaults OFF. It exists for the Guide 12d tile-seam fix,
///   but quantizing the camera to whole pixels visibly steps slow motion
///   and can oscillate between two pixels at rest — and because the snap
///   grid is derived from orthographicSize, it resizes every frame while
///   zooming, which reads as the background snapping around. Smoothness
///   won that tradeoff; see the field tooltip.
/// - REQUIRES the followed Rigidbody2D to have Interpolate enabled
///   (Editor setting, not code). Physics moves the player in FixedUpdate
///   at a fixed 50Hz while this runs every rendered frame — without
///   interpolation the camera chases a position that only updates 50
///   times a second, which jitters no matter how good the easing here is.
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

    [Tooltip("How much one mouse-wheel notch multiplies the zoom by. 1.15 = each notch changes zoom by 15%, so every notch feels the same at any zoom level.")]
    [SerializeField] private float zoomStepFactor = 1.15f;

    [Tooltip("How quickly the camera eases to the new zoom level. Higher = snappier, lower = floatier.")]
    [SerializeField] private float zoomSmoothing = 10f;

    [Header("Follow (§3.7 — follow speed scales with zoom)")]
    [Tooltip("X axis: 0 = fully zoomed in, 1 = fully zoomed out. Y axis: follow speed (higher = tighter tracking).")]
    [SerializeField] private AnimationCurve followSpeedByZoom = AnimationCurve.Linear(0f, 10f, 1f, 2.5f);

    [Header("Pixel art")]
    [Tooltip("Rounds the camera to whole screen pixels — the Guide 12d fix for shimmering seams between tiles. Costs smoothness: slow movement becomes visibly stepped and the background snaps while zooming, because the snap grid resizes with the zoom level. Leave OFF for smooth motion; turn ON only if tile seams bother you more than stepping does.")]
    [SerializeField] private bool pixelSnap = false;

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

        // Normalize to wheel notches: the Input System reports 120 per notch
        // on a standard mouse wheel, while trackpads send smaller continuous
        // values. Dividing keeps a real wheel at exactly one step per notch
        // and lets a trackpad scroll produce smooth fractional zoom, instead
        // of Mathf.Sign flattening every input to a full step regardless of
        // how far it actually scrolled.
        float scroll = mouse.scroll.ReadValue().y / 120f;
        if (Mathf.Abs(scroll) > 0.0001f)
            targetZoom = Mathf.Clamp(targetZoom * Mathf.Pow(zoomStepFactor, -scroll), minZoom, maxZoom);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // unscaledDeltaTime so the camera still settles on the win/lose screen (timeScale 0).
        float dt = Time.unscaledDeltaTime;

        // Ease the logarithm, not the size itself: zooming 5->10 and 10->20
        // are the same perceptual change, and only log-space easing treats
        // them as such. Easing the raw size makes a zoom-out start fast and
        // then crawl.
        float logNow = Mathf.Log(cam.orthographicSize);
        float logTarget = Mathf.Log(targetZoom);
        cam.orthographicSize = Mathf.Exp(Mathf.Lerp(logNow, logTarget, 1f - Mathf.Exp(-zoomSmoothing * dt)));

        float zoomT = Mathf.InverseLerp(minZoom, maxZoom, cam.orthographicSize);
        float followSpeed = followSpeedByZoom.Evaluate(zoomT);

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        Vector3 smoothed = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * dt));
        transform.position = pixelSnap ? SnapToScreenPixel(smoothed) : smoothed;
    }

    /// <summary>
    /// Rounds x/y to the nearest on-screen pixel at the current zoom (see
    /// Guide 12d). Uses the CURRENT orthographic size rather than the art's
    /// fixed Pixels Per Unit, so it stays correct through the mouse-wheel
    /// zoom instead of only being exact at one zoom level — but that same
    /// property is why it snaps while zooming, hence the default-off toggle.
    /// </summary>
    private Vector3 SnapToScreenPixel(Vector3 position)
    {
        float worldUnitsPerScreenPixel = 2f * cam.orthographicSize / Screen.height;
        position.x = Mathf.Round(position.x / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
        position.y = Mathf.Round(position.y / worldUnitsPerScreenPixel) * worldUnitsPerScreenPixel;
        return position;
    }
}
