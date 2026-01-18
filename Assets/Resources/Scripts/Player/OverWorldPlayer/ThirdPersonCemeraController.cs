using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System;

public class ThirdPersonCemeraController : MonoBehaviour
{
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float zoomLerpSpeed = 10f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField]  private CinemachineCamera cam;
    [SerializeField]  private CinemachineOrbitalFollow orbital;
    
    private Vector2 scrollDelta;
    private PlayerInputActions controls;

    private float targetZoom;
    private float currentZoom;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(cam==null || orbital == null)
        {
            throw new Exception("Cinemachine Camera or Orbital Follow component is not assigned.");
        }

        controls = new PlayerInputActions();
        controls.Enable();
        controls.CemeraControls.MouseZoom.performed += HandeleMouseScroll;

        Cursor.lockState = CursorLockMode.Locked;

        targetZoom = currentZoom = orbital.Radius;
    }

    private void HandeleMouseScroll(InputAction.CallbackContext context)
    {
        scrollDelta = context.ReadValue<Vector2>();
        //Debug.Log($"Scroll Delta: {scrollDelta}");
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (orbital == null) return;

        if (scrollDelta.y != 0) 
        {
            targetZoom = Mathf.Clamp(orbital.Radius - scrollDelta.y * zoomSpeed, minDistance, maxDistance);
            scrollDelta = Vector2.zero;
        }

        float bumperDelta = controls.CemeraControls.GamePadZoom.ReadValue<float>();

        if (bumperDelta != 0)
        {
            targetZoom = Mathf.Clamp(orbital.Radius - bumperDelta * zoomSpeed, minDistance, maxDistance);
        }


        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomLerpSpeed);
        orbital.Radius = currentZoom;
    }
}
