using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 20f;

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

    [Header("Start Position Offset")]
    [Tooltip("Added to the grid-center position calculated by SetGridBounds. " +
             "Tweak Z to shift the view forward/backward, Y for height.")]
    [SerializeField] private Vector3 targetStartOffset = Vector3.zero;

    [Header("Map Bounds (optional)")]
    [Tooltip("Two empty GameObjects that mark the bottom-left and top-right corners " +
             "of the playable area. The camera target is clamped between them so the " +
             "player cannot pan off the map.\n" +
             "Leave both unassigned for unlimited panning.")]
    [SerializeField] private Transform boundsMinCorner;
    [SerializeField] private Transform boundsMaxCorner;

    [Header("Monster Focus")]
    [Tooltip("Zoom level when focused on a monster.")]
    [SerializeField] private float focusZoom = 9f;
    [Tooltip("Camera pitch angle (degrees) when focused — lower = more cinematic.")]
    [Range(10f, 80f)]
    [SerializeField] private float focusAngle = 28f;
    [Tooltip("Lateral offset applied to the camera target when focusing, giving a slight diagonal view.")]
    [SerializeField] private Vector3 focusOffset = new Vector3(2f, 0f, 0.5f);
    [Tooltip("Speed of the focus-in and focus-out transitions.")]
    [SerializeField] private float focusSpeed = 6f;

    [Header("Orbit Rotation")]
    [Tooltip("How fast the camera orbits when dragging with the right mouse button.")]
    [SerializeField] private float rotateSpeed = 0.25f;

    private InputAction moveAction;
    private InputAction zoomAction;
    private Vector3     currentVelocity;
    private float       targetZoom;

    // Middle-mouse pan state
    private bool _isPanning;

    // Edge-scroll current speed (smoothly ramped)
    private Vector3 _edgeScrollVelocity;

    // Orbit yaw (horizontal rotation around the target)
    private float _orbitYaw;
    public float OrbitYaw => _orbitYaw;

    // ── Enemy-turn zoom state ──────────────────────────────────────────────────
    private bool  _enemyTurnZoomActive;
    private float _enemyTurnSavedZoom;
    private bool  _subscribedToTurnManager;

    // ── Top-down state ─────────────────────────────────────────────────────────
    // Activated by scrolling out past maxZoom. Camera snaps to 90° pitch and locks
    // angle until the user scrolls back in past maxZoom to return to normal mode.
    private bool  _isTopDown;
    private float _topDownAngle = 90f;   // target pitch in top-down mode
    private float _currentAngle;         // smooth working copy of cameraAngle

    [Header("Enemy Turn Zoom")]
    [Tooltip("Zoom distance used while the enemy takes its turn (shows the full map).")]
    [SerializeField] private float enemyTurnZoom = 35f;

    // ── Focus state ────────────────────────────────────────────────────────────

    private bool    _isLocked;       // true while focused on a monster
    private bool    _isRestoring;    // true while smoothly returning to saved state
    private Vector3 _focusWorldPos;  // the monster's world position
    private Vector3 _savedPos;
    private float   _savedZoom;
    private float   _savedAngle;
    private float   _savedYaw;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        currentZoom    = maxZoom;
        targetZoom     = maxZoom;
        _currentAngle  = cameraAngle;

        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Camera");
            if (map != null)
            {
                moveAction = map.FindAction("KeyBoardMove");
                zoomAction = map.FindAction("Zoom");
                inputActions.Enable();
            }
        }
    }

    void Update()
    {
        if (cameraTarget == null || cinemachineCamera == null) return;

        // Lazily subscribe to TurnManager once it exists
        if (!_subscribedToTurnManager && TurnManager.Instance != null)
        {
            TurnManager.Instance.onEnemyTurnStart.AddListener(BeginEnemyTurnZoom);
            TurnManager.Instance.onPlayerTurnStart.AddListener(EndEnemyTurnZoom);
            _subscribedToTurnManager = true;
        }

        // Reset orbit yaw hotkey — only when the camera is free (not AI-locked).
        if (!_isLocked && HotkeyManager.Instance != null &&
            HotkeyManager.Instance.WasPressedThisFrame(HotkeyAction.ResetCamera))
            ResetOrbitYaw();

        if (_isLocked)
        {
            SmoothToFocus();
        }
        else if (_isRestoring)
        {
            SmoothToSaved();
        }
        else
        {
            HandleOrbitRotation();
            HandleMiddleMousePan();
            HandleEdgeScroll();
            HandleMovement();
            HandleZoom();
        }

        // Always lerp currentZoom toward targetZoom regardless of lock state
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * 10f);
        ApplyCameraPosition();
    }

    // ── Monster Focus API ──────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly zooms in on <paramref name="worldPos"/> at a cinematic angle
    /// and locks all camera movement until <see cref="ReleaseFocus"/> is called.
    /// Safe to call while already focused (updates the target without re-saving).
    /// </summary>
    public void FocusOnMonster(Vector3 worldPos)
    {
        // Save the original camera state on first entry only.
        // If we're mid-restore, _savedPos already holds the original — keep it.
        if (!_isLocked && !_isRestoring)
        {
            _savedPos   = cameraTarget.position;
            _savedZoom  = targetZoom;
            _savedAngle = cameraAngle;
            _savedYaw   = _orbitYaw;
        }

        _isRestoring   = false;
        _isLocked      = true;
        _focusWorldPos = worldPos;
    }

    /// <summary>
    /// Moves the focus target to a new world position while already focused.
    /// Use this to track a moving monster without releasing and re-acquiring focus.
    /// </summary>
    public void UpdateFocusTarget(Vector3 worldPos)
    {
        if (_isLocked) _focusWorldPos = worldPos;
    }

    /// <summary>
    /// Unlocks camera movement and smoothly returns to the pre-focus state.
    /// </summary>
    public void ReleaseFocus()
    {
        if (!_isLocked) return;
        _isLocked    = false;
        _isRestoring = true;
    }

    // ── Focus helpers ──────────────────────────────────────────────────────────

    private void SmoothToFocus()
    {
        float t = Time.deltaTime * focusSpeed;

        Vector3 dest = _focusWorldPos + focusOffset;
        cameraTarget.position = Vector3.Lerp(cameraTarget.position, dest,  t);
        targetZoom            = Mathf.Lerp(targetZoom,  focusZoom,  t);
        cameraAngle           = Mathf.Lerp(cameraAngle, focusAngle, t);
    }

    private void SmoothToSaved()
    {
        float t = Time.deltaTime * focusSpeed;

        cameraTarget.position = Vector3.Lerp(cameraTarget.position, _savedPos,  t);
        targetZoom            = Mathf.Lerp(targetZoom,  _savedZoom,  t);
        cameraAngle           = Mathf.Lerp(cameraAngle, _savedAngle, t);
        _orbitYaw             = Mathf.LerpAngle(_orbitYaw, _savedYaw, t);

        // Snap once close enough so we don't lerp forever
        if (Vector3.Distance(cameraTarget.position, _savedPos) < 0.05f &&
            Mathf.Abs(targetZoom - _savedZoom) < 0.1f)
        {
            cameraTarget.position = _savedPos;
            targetZoom            = _savedZoom;
            cameraAngle           = _savedAngle;
            _orbitYaw             = _savedYaw;
            _isRestoring          = false;
        }
    }

    // ── Orbit rotation (right-mouse drag) ─────────────────────────────────────

    private void HandleOrbitRotation()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        if (!mouse.rightButton.isPressed) return;

        float dx = mouse.delta.ReadValue().x;
        _orbitYaw = (_orbitYaw + dx * rotateSpeed) % 360f;
    }

    // ── Camera-relative direction helpers ─────────────────────────────────────

    // Camera right and forward in world XZ based on current orbit yaw.
    // right   = (cos, 0, sin)   — screen-right at any yaw
    // forward = (-sin, 0, cos)  — screen-up  at any yaw
    private void YawVectors(out Vector3 right, out Vector3 forward)
    {
        float rad = _orbitYaw * Mathf.Deg2Rad;
        float s = Mathf.Sin(rad), c = Mathf.Cos(rad);
        right   = new Vector3( c, 0f,  s);
        forward = new Vector3(-s, 0f,  c);
    }

    // ── Middle-mouse pan ───────────────────────────────────────────────────────

    private void HandleMiddleMousePan()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.middleButton.wasPressedThisFrame)  _isPanning = true;
        if (mouse.middleButton.wasReleasedThisFrame) _isPanning = false;

        if (!_isPanning) return;

        YawVectors(out Vector3 right, out Vector3 forward);
        Vector2 delta = mouse.delta.ReadValue();
        // Drag "grabs" the world: mouse-right pulls camera-right (target moves in -right).
        Vector3 move = (-right * delta.x - forward * delta.y) * panSpeed;
        cameraTarget.Translate(move, Space.World);
        ApplyBoundsClamping();
    }

    // ── Edge scrolling ─────────────────────────────────────────────────────────

    private void HandleEdgeScroll()
    {
        if (!GameSettings.EdgeScrollEnabled) return;
        if (_isPanning) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        float sw = Screen.width;
        float sh = Screen.height;

        float r = Mathf.InverseLerp(sw - edgeScrollMargin, sw, mousePos.x);
        float l = Mathf.InverseLerp(edgeScrollMargin, 0f,  mousePos.x);
        float u = Mathf.InverseLerp(sh - edgeScrollMargin, sh, mousePos.y);
        float d = Mathf.InverseLerp(edgeScrollMargin, 0f,  mousePos.y);

        YawVectors(out Vector3 right, out Vector3 forward);
        Vector3 targetVel = right   * ((r - l) * edgeScrollMaxSpeed)
                          + forward * ((u - d) * edgeScrollMaxSpeed);

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
        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 0.01f)
        {
            YawVectors(out Vector3 right, out Vector3 forward);
            Vector3 direction = right * input.x + forward * input.y;
            cameraTarget.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
            ApplyBoundsClamping();
        }
    }

    /// <summary>Snaps the orbit yaw back to 0 (north-facing default).</summary>
    public void ResetOrbitYaw() => _orbitYaw = 0f;

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

        float scrollDelta = zoomAction.ReadValue<float>();
        if (Mathf.Abs(scrollDelta) > 0.1f)
        {
            bool scrollingOut = scrollDelta < 0f;

            if (_isTopDown)
            {
                // Any scroll-in while in top-down exits back to normal
                if (!scrollingOut) _isTopDown = false;
            }
            else
            {
                targetZoom -= (scrollDelta / 120f) * zoomSpeed;
                if (targetZoom > maxZoom)
                {
                    // Scrolled past the max — enter top-down mode
                    targetZoom = maxZoom;
                    _isTopDown = true;
                }
                else
                {
                    targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
                }
            }
        }
        // currentZoom lerp is handled in Update() so it runs during focus/restore too
    }

    private void ApplyCameraPosition()
    {
        // Smoothly transition pitch between normal cameraAngle and 90° top-down
        float targetAngle = _isTopDown ? _topDownAngle : cameraAngle;
        _currentAngle = Mathf.Lerp(_currentAngle, targetAngle, Time.deltaTime * focusSpeed);

        float pitchRad = _currentAngle * Mathf.Deg2Rad;
        float yawRad   = _orbitYaw     * Mathf.Deg2Rad;

        float yOffset = currentZoom * Mathf.Sin(pitchRad);
        float hOffset = currentZoom * Mathf.Cos(pitchRad);  // horizontal distance from target

        // Orbit around Y axis: yaw=0 → camera is directly behind (−Z), positive yaw rotates clockwise
        float xOffset =  hOffset * Mathf.Sin(yawRad);
        float zOffset = -hOffset * Mathf.Cos(yawRad);

        Vector3 desiredPosition = cameraTarget.position + new Vector3(xOffset, yOffset, zOffset);

        cinemachineCamera.transform.position = desiredPosition;
        cinemachineCamera.transform.LookAt(cameraTarget.position);
    }

    // ── Enemy-turn zoom-out ────────────────────────────────────────────────────

    private void BeginEnemyTurnZoom()
    {
        if (_isLocked || _isRestoring || _enemyTurnZoomActive) return;
        _enemyTurnSavedZoom  = targetZoom;
        _enemyTurnZoomActive = true;
        targetZoom           = Mathf.Max(targetZoom, enemyTurnZoom);
    }

    private void EndEnemyTurnZoom()
    {
        if (!_enemyTurnZoomActive) return;
        targetZoom           = _enemyTurnSavedZoom;
        _enemyTurnZoomActive = false;
    }

    private void OnDestroy()
    {
        if (_subscribedToTurnManager && TurnManager.Instance != null)
        {
            TurnManager.Instance.onEnemyTurnStart.RemoveListener(BeginEnemyTurnZoom);
            TurnManager.Instance.onPlayerTurnStart.RemoveListener(EndEnemyTurnZoom);
        }
    }

    public void SetGridBounds(int width, int height, float spacing)
    {
        if (cameraTarget == null) return;
        float centerX = (width - 1) * spacing / 2f;
        float centerZ = (height - 1) * spacing / 2f;
        cameraTarget.position = new Vector3(centerX, 0, centerZ) + targetStartOffset;
    }
}
