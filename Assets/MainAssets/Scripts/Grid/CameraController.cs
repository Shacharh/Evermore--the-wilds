using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 20f;
    [Tooltip("How quickly the camera accelerates/decelerates when using keyboard movement.\n" +
             "Higher = snappier response. Lower = floatier feel.")]
    [SerializeField] private float keyboardAcceleration = 8f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;
    [SerializeField] private float maxZoom = 40f;
    [SerializeField] private float currentZoom = 15f;

    [Header("Camera Angle")]
    [Range(20f, 80f)]
    [SerializeField] private float cameraAngle = 45f;

    [Header("Middle-Mouse Pan")]
    [Tooltip("How fast the camera pans when dragging with the middle mouse button.")]
    [SerializeField] private float panSpeed = 0.03f;

    [Header("Edge Scrolling")]
    [Tooltip("Pixels from the screen edge that trigger edge scrolling.")]
    [SerializeField] private float edgeScrollMargin = 20f;
    [Tooltip("Maximum scroll speed when the cursor is hard against the edge.")]
    [SerializeField] private float edgeScrollMaxSpeed = 25f;
    [Tooltip("How quickly edge-scroll speed ramps up/down (higher = snappier).")]
    [SerializeField] private float edgeScrollAcceleration = 8f;

    [Header("Map Bounds (optional)")]
    [Tooltip("Two empty GameObjects that mark the bottom-left and top-right corners " +
             "of the playable area. The camera target is clamped between them so the " +
             "player cannot pan off the map.\n" +
             "Leave both unassigned for unlimited panning.")]
    [SerializeField] private Transform boundsMinCorner;
    [SerializeField] private Transform boundsMaxCorner;

    private InputAction zoomAction;
    private float targetZoom;

    // Smoothed keyboard velocity (gives the natural acceleration/deceleration feel)
    private Vector3 _keyboardVelocity;

    // Middle-mouse pan state
    private bool _isPanning;

    // Edge-scroll current speed (smoothly ramped)
    private Vector3 _edgeScrollVelocity;

    void Awake()
    {
        targetZoom = currentZoom;

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Camera");
            if (map != null)
            {
                zoomAction = map.FindAction("Zoom");
                inputActions.Enable();
            }
        }
    }

    void Update()
    {
        if (cameraTarget == null || cinemachineCamera == null) return;

        HandleMiddleMousePan();
        HandleEdgeScroll();
        HandleMovement();
        HandleZoom();
        ApplyCameraPosition();
    }

    // ── Middle-mouse pan ──────────────────────────────────────────────────────

    private void HandleMiddleMousePan()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.middleButton.wasPressedThisFrame)  _isPanning = true;
        if (mouse.middleButton.wasReleasedThisFrame) _isPanning = false;

        if (!_isPanning) return;

        // delta is the pixel movement since last frame — already computed by the Input System
        Vector2 delta = mouse.delta.ReadValue();

        // Invert so dragging right pans the world rightward
        Vector3 move = new Vector3(-delta.x, 0f, -delta.y) * panSpeed;
        cameraTarget.Translate(move, Space.World);
        ApplyBoundsClamping();
    }

    // ── Edge scrolling ────────────────────────────────────────────────────────

    private void HandleEdgeScroll()
    {
        if (!GameSettings.EdgeScrollEnabled) return;
        if (_isPanning) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        float sw = Screen.width;
        float sh = Screen.height;

        float right = Mathf.InverseLerp(sw - edgeScrollMargin, sw, mousePos.x);
        float left  = Mathf.InverseLerp(edgeScrollMargin, 0f,  mousePos.x);
        float up    = Mathf.InverseLerp(sh - edgeScrollMargin, sh, mousePos.y);
        float down  = Mathf.InverseLerp(edgeScrollMargin, 0f,  mousePos.y);

        Vector3 targetVel = new Vector3(
            (right - left) * edgeScrollMaxSpeed,
            0f,
            (up - down)    * edgeScrollMaxSpeed);

        _edgeScrollVelocity = Vector3.Lerp(
            _edgeScrollVelocity, targetVel,
            Time.deltaTime * edgeScrollAcceleration);

        if (_edgeScrollVelocity.sqrMagnitude > 0.001f)
        {
            cameraTarget.Translate(_edgeScrollVelocity * Time.deltaTime, Space.World);
            ApplyBoundsClamping();
        }
    }

    private void HandleMovement()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // WASD primary, arrow keys as fallback
        float h = 0f, v = 0f;
        if (kb.aKey.isPressed     || kb.leftArrowKey.isPressed)  h -= 1f;
        if (kb.dKey.isPressed     || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.sKey.isPressed     || kb.downArrowKey.isPressed)  v -= 1f;
        if (kb.wKey.isPressed     || kb.upArrowKey.isPressed)    v += 1f;

        Vector3 targetVelocity = new Vector3(h, 0f, v).normalized * moveSpeed;

        // Smoothly ramp velocity up (when key held) and back to zero (when released)
        _keyboardVelocity = Vector3.Lerp(
            _keyboardVelocity, targetVelocity,
            Time.deltaTime * keyboardAcceleration);

        if (_keyboardVelocity.sqrMagnitude > 0.001f)
        {
            cameraTarget.Translate(_keyboardVelocity * Time.deltaTime, Space.World);
            ApplyBoundsClamping();
        }
    }

    /// <summary>
    /// Clamps the camera target's XZ position to stay within the rectangle
    /// defined by <see cref="boundsMinCorner"/> and <see cref="boundsMaxCorner"/>.
    /// Y is never clamped — the pivot stays at ground level.
    /// Does nothing if either corner Transform is unassigned.
    /// </summary>
    private void ApplyBoundsClamping()
    {
        if (boundsMinCorner == null || boundsMaxCorner == null) return;

        Vector3 pos  = cameraTarget.position;
        float   minX = Mathf.Min(boundsMinCorner.position.x, boundsMaxCorner.position.x);
        float   maxX = Mathf.Max(boundsMinCorner.position.x, boundsMaxCorner.position.x);
        float   minZ = Mathf.Min(boundsMinCorner.position.z, boundsMaxCorner.position.z);
        float   maxZ = Mathf.Max(boundsMinCorner.position.z, boundsMaxCorner.position.z);

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        cameraTarget.position = pos;
    }

    private void HandleZoom()
    {
        if (zoomAction == null) return;

        // Read scroll delta (usually Y is +/- 120 or similar)
        float scrollDelta = zoomAction.ReadValue<float>();

        if (Mathf.Abs(scrollDelta) > 0.1f)
        {
            // Normalize scroll and update target zoom
            targetZoom -= (scrollDelta / 120f) * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // Smoothly interpolate the zoom for a "premium" feel
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 10f);
    }

    private void ApplyCameraPosition()
    {
        float rad = cameraAngle * Mathf.Deg2Rad;

        // Trigonometry to find the camera offset
        // y = Height, z = Depth
        float yOffset = currentZoom * Mathf.Sin(rad);
        float zOffset = currentZoom * Mathf.Cos(rad);

        Vector3 desiredPosition = cameraTarget.position + new Vector3(0, yOffset, -zOffset);

        // Update the Virtual Camera Transform
        cinemachineCamera.transform.position = desiredPosition;
        cinemachineCamera.transform.LookAt(cameraTarget.position);
    }

    public void SetGridBounds(int width, int height, float spacing)
    {
        if (cameraTarget == null) return;
        float centerX = (width - 1) * spacing / 2f;
        float centerZ = (height - 1) * spacing / 2f;
        cameraTarget.position = new Vector3(centerX, 0, centerZ);
    }
}