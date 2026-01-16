using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public float turnSmoothTime = 0.1f;

    private float turnSmoothVelocity;

    [SerializeField] private PlayerState state;
    [SerializeField] private PlayerInputHandler input;

    //private void Awake()
    //{
    //    state = GetComponent<PlayerState>();
    //    input = GetComponent<PlayerInputHandler>();
    //}

    void Update()
    {
        Vector3 direction = new Vector3(input.Move.x, 0f, input.Move.y).normalized;

        if (direction.magnitude < 0.1f) return;

        float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + state.cameraTransform.eulerAngles.y;

        float angle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref turnSmoothVelocity,
            turnSmoothTime
        );

        transform.rotation = Quaternion.Euler(0f, angle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        state.controller.Move(moveDir * speed * Time.deltaTime);

    }
}


//using UnityEngine;
//using UnityEngine.InputSystem;

//public class PlayerMovement : MonoBehaviour
//{
//    public CharacterController controller;
//    public Transform cam;
//    public float speed = 6f;
//    public float turnSmoothTime = 0.1f;

//    private PlayerInputActions inputActions;
//    private InputAction moveAction;
//    private float turnSmoothVelocity;

//    [Header("Gravity")]
//    public float gravity = -9.81f;
//    public float groundedGravity = -2f; // keeps player stuck to ground
//    public float rayDistance = 0.3f;
//    public LayerMask groundMask;

//    private Vector3 velocity;
//    private bool isGrounded;


//    private void Awake()
//    {
//        inputActions = new PlayerInputActions();
//    }

//    private void OnEnable()
//    {
//        moveAction = inputActions.Player.Move;
//        moveAction.Enable();
//    }

//    private void OnDisable()
//    {
//        moveAction.Disable();
//    }

//    void Update()
//    {
//        CheckGround();


//        Vector2 input = moveAction.ReadValue<Vector2>();
//        Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;

//        if (direction.magnitude >= 0.1f)
//        {
//            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;

//            float angle = Mathf.SmoothDampAngle(
//                transform.eulerAngles.y,
//                targetAngle,
//                ref turnSmoothVelocity,
//                turnSmoothTime
//            );

//            transform.rotation = Quaternion.Euler(0f, angle, 0f);

//            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
//            controller.Move(moveDir.normalized * speed * Time.deltaTime);
//        }

//        ApplyGravity();
//    }

//    void CheckGround()
//    {
//        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
//        isGrounded = Physics.Raycast(
//            rayOrigin,
//            Vector3.down,
//            rayDistance,
//            groundMask
//        );

//        if (isGrounded && velocity.y < 0)
//        {
//            velocity.y = groundedGravity;
//        }
//    }

//    void ApplyGravity()
//    {
//        velocity.y += gravity * Time.deltaTime;
//        controller.Move(velocity * Time.deltaTime);
//    }

//    void OnDrawGizmos()
//    {
//        Gizmos.color = isGrounded ? Color.green : Color.red;
//        Gizmos.DrawLine(
//            transform.position + Vector3.up * 0.1f,
//            transform.position + Vector3.down * rayDistance
//        );
//    }



//}
