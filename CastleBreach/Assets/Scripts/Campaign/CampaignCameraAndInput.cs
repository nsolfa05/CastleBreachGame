using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Click-and-drag horizontal panning across the campaign trail, plus
/// click-to-activate on a CampaignNode — both live in one script because
/// they share the same mouse-down/up gesture and have to agree on whether
/// it was a drag (pan) or a genuine click (activate a node). A plain
/// per-node click handler can't tell the difference on its own, and two
/// separate scripts independently reacting to the same press could double
/// up or misfire. Threshold-based like a typical map/canvas UI: screen
/// movement under `clickThreshold` since mouse-down still counts as a
/// click; past it, the whole gesture counts as a drag and node activation
/// is suppressed for that press.
///
/// Deliberately X-only — camera Y stays fixed. The campaign trail scrolls
/// left/right; it doesn't pan vertically even though nodes zigzag in Y.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CampaignCameraAndInput : MonoBehaviour
{
    [Tooltip("Leftmost/rightmost camera X — set to your outermost node positions plus a little margin.")]
    [SerializeField] private float minX = 0f;
    [SerializeField] private float maxX = 40f;

    [Tooltip("Screen-pixel movement below this still counts as a click, not a drag.")]
    [SerializeField] private float clickThreshold = 8f;

    private Camera cam;
    private bool dragging;
    private Vector2 pressScreenPos;
    private float pressCameraX;
    private float maxDragDistance;

    private void Awake() => cam = GetComponent<Camera>();

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            pressScreenPos = mouse.position.ReadValue();
            pressCameraX = transform.position.x;
            maxDragDistance = 0f;
        }
        else if (dragging && mouse.leftButton.isPressed)
        {
            Vector2 currentScreenPos = mouse.position.ReadValue();
            maxDragDistance = Mathf.Max(maxDragDistance, (currentScreenPos - pressScreenPos).magnitude);

            float worldDeltaX = ScreenToWorldX(currentScreenPos) - ScreenToWorldX(pressScreenPos);
            float newX = Mathf.Clamp(pressCameraX - worldDeltaX, minX, maxX);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
        }
        else if (dragging && mouse.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
            if (maxDragDistance <= clickThreshold)
                TryActivateNodeUnder(mouse.position.ReadValue());
        }
    }

    private float ScreenToWorldX(Vector2 screenPos) =>
        cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f)).x;

    private void TryActivateNodeUnder(Vector2 screenPos)
    {
        Vector2 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
        var hit = Physics2D.OverlapPoint(worldPoint);
        if (hit != null && hit.TryGetComponent<CampaignNode>(out var node))
            node.Activate();
    }
}
