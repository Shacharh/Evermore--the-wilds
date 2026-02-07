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
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 5f;   // Closest view
    [SerializeField] private float maxZoom = 40f;  // Furthest view
    [SerializeField] private float currentZoom = 15f;

    [Header("Camera Angle")]
    [Range(20f, 80f)]
    [SerializeField] private float cameraAngle = 45f;

    private InputAction moveAction;
    private InputAction zoomAction;
    private Vector3 currentVelocity;
    private float targetZoom;

    void Awake()
    {
        targetZoom = currentZoom; // Initialize zoom target

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

        HandleMovement();
        HandleZoom();
        ApplyCameraPosition();
    }

    private void HandleMovement()
    {
        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 direction = new Vector3(input.x, 0, input.y);
            // Move the target relative to world space
            cameraTarget.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
        }
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