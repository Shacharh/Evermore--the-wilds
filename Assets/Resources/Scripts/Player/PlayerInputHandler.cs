using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private PlayerInputActions inputActions;

    public Vector2 Move { get; private set; }
    public bool JumpPressed { get; private set; }

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();

        inputActions.Player.Move.performed += ctx => Move = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += _ => Move = Vector2.zero;

        inputActions.Player.Jump.performed += _ => JumpPressed = true;

    }

    private void LateUpdate()
    {
        // reset one-frame inputs
        JumpPressed = false;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }
}
