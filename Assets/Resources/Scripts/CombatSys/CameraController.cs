using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private Transform cameraTarget;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Movement Settings")]
    [SerializeField] private float dragPanSpeed = 0.5f;
    [SerializeField] private float edgePanSpeed = 15f;
    [SerializeField] private float edgePanBorderThickness = 10f;
    [SerializeField] private float keyboardPanSpeed = 15f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 8f; // Closer minimum
    [SerializeField] private float maxZoom = 25f; // Further maximum

    [Header("Camera Angle")]
    [SerializeField] private float cameraAngle = 60f; // Increased for better top-down view
    [SerializeField] private float cameraHeight = 12f; // Increased to see more of the grid

    [Header("Grid Bounds - Auto Synced")]
    [SerializeField] private float gridWidth = 10f;
    [SerializeField] private float gridHeight = 10f;
    [SerializeField] private float tileSpacing = 1.1f;
    [SerializeField] private float boundaryPadding = 2f;

    private InputAction panAction;
    private InputAction rightClickAction;
    private InputAction keyboardMoveAction;
    private InputAction zoomAction;
    private InputAction mousePositionAction;

    private float currentZoom;
    private bool isDragging = false;
    private Vector2 lastMousePosition;

    void Awake()
    {
        currentZoom = cameraHeight;

        // Create camera target if not assigned
        if (cameraTarget == null)
        {
            GameObject target = new GameObject("CameraTarget");
            cameraTarget = target.transform;
        }

        // Set up input actions
        if (inputActions != null)
        {
            var cameraMap = inputActions.FindActionMap("Camera");
            panAction = cameraMap.FindAction("Pan");
            rightClickAction = cameraMap.FindAction("RightClick");
            keyboardMoveAction = cameraMap.FindAction("KeyBoardMove");
            zoomAction = cameraMap.FindAction("Zoom");
            mousePositionAction = cameraMap.FindAction("MousePosition");

            rightClickAction.started += _ => OnRightClickStarted();
            rightClickAction.canceled += _ => OnRightClickCanceled();

            // CRITICAL: Enable the actions here!
            inputActions.Enable();
            Debug.Log("Input Actions ENABLED in Awake");
        }
        else
        {
            Debug.LogError("InputActions asset is not assigned!");
        }
    }

    void Start()
    {
        // Center camera on grid
        CenterCamera();

        // Set up Cinemachine camera
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = cameraTarget;
            UpdateCameraPosition();
        }

        // DEBUG: Check if actions are found and enabled
        Debug.Log($"Pan Action: {(panAction != null ? "Found" : "NULL")}");
        Debug.Log($"RightClick Action: {(rightClickAction != null ? "Found" : "NULL")}");
        Debug.Log($"KeyboardMove Action: {(keyboardMoveAction != null ? "Found" : "NULL")}");
        Debug.Log($"Zoom Action: {(zoomAction != null ? "Found" : "NULL")}");
        Debug.Log($"MousePosition Action: {(mousePositionAction != null ? "Found" : "NULL")}");

        if (inputActions != null)
        {
            Debug.Log($"Input Actions enabled: {inputActions.enabled}");
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }

    void Update()
    {
        Vector3 oldPos = cameraTarget.position;

        HandleMouseDragPan();
        HandleEdgePan();
        HandleKeyboardPan();
        HandleZoom();

        // Force update camera position every frame
        UpdateCameraPosition();

        if (oldPos != cameraTarget.position)
        {
            Debug.Log($"CameraTarget moved to: {cameraTarget.position}");
        }
    }

    void OnRightClickStarted()
    {
        isDragging = true;
        if (mousePositionAction != null)
            lastMousePosition = mousePositionAction.ReadValue<Vector2>();
        Debug.Log("Right click started");
    }

    void OnRightClickCanceled()
    {
        isDragging = false;
        Debug.Log("Right click canceled");
    }

    void HandleMouseDragPan()
    {
        if (isDragging && panAction != null)
        {
            Vector2 delta = panAction.ReadValue<Vector2>();

            if (delta.magnitude > 0.1f)
            {
                Debug.Log($"Pan delta: {delta}");
            }

            // Convert screen space delta to world space movement
            Vector3 move = new Vector3(-delta.x, 0, -delta.y) * dragPanSpeed * Time.deltaTime;

            cameraTarget.position += move;
            ClampCameraPosition();
        }
    }

    void HandleEdgePan()
    {
        if (mousePositionAction == null) return;

        Vector2 mousePos = mousePositionAction.ReadValue<Vector2>();
        Vector3 moveDirection = Vector3.zero;

        // Check mouse position against screen edges
        if (mousePos.x < edgePanBorderThickness)
            moveDirection.x = -1;
        else if (mousePos.x > Screen.width - edgePanBorderThickness)
            moveDirection.x = 1;

        if (mousePos.y < edgePanBorderThickness)
            moveDirection.z = -1;
        else if (mousePos.y > Screen.height - edgePanBorderThickness)
            moveDirection.z = 1;

        if (moveDirection != Vector3.zero)
        {
            cameraTarget.position += moveDirection.normalized * edgePanSpeed * Time.deltaTime;
            ClampCameraPosition();
        }
    }

    void HandleKeyboardPan()
    {
        if (Keyboard.current == null) return;

        Vector3 moveDirection = Vector3.zero;

        // Check which arrow key is pressed
        if (Keyboard.current.upArrowKey.isPressed)
        {
            moveDirection.z = 1;
            Debug.Log("UP arrow detected");
        }
        if (Keyboard.current.downArrowKey.isPressed)
        {
            moveDirection.z = -1;
            Debug.Log("DOWN arrow detected");
        }
        if (Keyboard.current.leftArrowKey.isPressed)
        {
            moveDirection.x = -1;
            Debug.Log("LEFT arrow detected");
        }
        if (Keyboard.current.rightArrowKey.isPressed)
        {
            moveDirection.x = 1;
            Debug.Log("RIGHT arrow detected");
        }

        if (moveDirection != Vector3.zero)
        {
            cameraTarget.position += moveDirection.normalized * keyboardPanSpeed * Time.deltaTime;
            ClampCameraPosition();
        }
    }

    void HandleZoom()
    {
        if (zoomAction == null) return;

        float scroll = zoomAction.ReadValue<float>();

        if (scroll != 0)
        {
            Debug.Log($"Zoom scroll: {scroll}");
            currentZoom -= scroll * zoomSpeed * Time.deltaTime;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
            UpdateCameraPosition();
        }
    }

    void UpdateCameraPosition()
    {
        // Calculate camera position based on angle and zoom
        float radians = cameraAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0, currentZoom * Mathf.Sin(radians), -currentZoom * Mathf.Cos(radians));

        // Update MAIN camera position directly
        Camera.main.transform.position = cameraTarget.position + offset;
        Camera.main.transform.LookAt(cameraTarget);

        // Also update this transform (in case it's the main camera)
        transform.position = cameraTarget.position + offset;
        transform.LookAt(cameraTarget);
    }

    void ClampCameraPosition()
    {
        float maxX = (gridWidth - 1) * tileSpacing + boundaryPadding;
        float maxZ = (gridHeight - 1) * tileSpacing + boundaryPadding;

        Vector3 pos = cameraTarget.position;
        pos.x = Mathf.Clamp(pos.x, -boundaryPadding, maxX);
        pos.z = Mathf.Clamp(pos.z, -boundaryPadding, maxZ);
        cameraTarget.position = pos;
    }

    void CenterCamera()
    {
        float centerX = (gridWidth - 1) * tileSpacing / 2f;
        float centerZ = (gridHeight - 1) * tileSpacing / 2f;
        cameraTarget.position = new Vector3(centerX, 0, centerZ);
    }

    // Called by GridManager to sync grid dimensions
    public void SetGridBounds(int width, int height, float spacing)
    {
        gridWidth = width;
        gridHeight = height;
        tileSpacing = spacing;

        CenterCamera();
    }
}