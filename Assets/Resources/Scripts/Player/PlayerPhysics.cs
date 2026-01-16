using System;
using UnityEngine;
using UnityEngine.Windows;

public class PlayerPhysics : MonoBehaviour
{
    [Header("Gravity")]
    public float gravity = -25f;
    public float groundedGravity = -2f;

    [Header("Jump")]
    public float jumpHeight = 2f;

    [Header("Ground Check")]
    public float rayDistance = 3f;
    public LayerMask groundMask;

    [SerializeField] private PlayerState state;
    [SerializeField] private PlayerInputHandler input;


    void Update()
    {
        CheckGround();
        HandleJump();
        ApplyGravity();
    }

    void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;

        // raycast with debug
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask))
        {
            state.isGrounded = true;
            Debug.DrawLine(origin, hit.point, Color.green);
        }
        else
        {
            state.isGrounded = false;
            Debug.DrawLine(origin, origin + Vector3.down * rayDistance, Color.red);
        }

        // For debug
        Debug.Log($"CheckGround | isGrounded={state.isGrounded}");
    }

    void HandleJump()
    {
        if (!state.isGrounded || !input.JumpPressed) return;
        // physics formula: v = sqrt(2gh)
        state.velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        state.isJumping = true;

        Debug.Log($"HandleJump | Grounded={state.isGrounded}");

    }

    void ApplyGravity()
    {
        state.velocity.y += gravity * Time.deltaTime;
        state.controller.Move(state.velocity * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        Vector3 end = origin + Vector3.down * rayDistance;

        Gizmos.color = Color.green;
        Vector3 direction = end - origin;
        float thickness = 0.05f;

        // Draw a cube along the line to simulate thickness
        Gizmos.DrawCube(origin + direction / 2, new Vector3(thickness, direction.magnitude, thickness));
    }

}
