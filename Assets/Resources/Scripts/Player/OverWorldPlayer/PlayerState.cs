using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cameraTransform;

    [Header("State")]
    public bool isGrounded;
    public bool isJumping;
    public Vector3 velocity;
}
