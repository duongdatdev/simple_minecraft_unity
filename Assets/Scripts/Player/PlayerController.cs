using UnityEngine;

/// <summary>
/// Simple first-person player controller using CharacterController.
/// - WASD to move
/// - Hold Left Shift to run
/// - Space to jump
/// - Mouse to look around
/// Attach this to the Player GameObject which must have a CharacterController.
/// The camera should be a child of the Player and its local rotation will be controlled by MouseLook.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float stickToGroundForce = -1f; // small downward force so isGrounded remains stable

    [Header("References")]
    public Transform cameraTransform; // assign the child camera

    private CharacterController cc;
    private float verticalVelocity = 0f;
    private Vector3 moveDirection = Vector3.zero;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // lock the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        HandleJumpAndGravity();
    }

    private void HandleMovement()
    {
        // read input
        float inputX = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float inputZ = Input.GetAxis("Vertical");   // W/S or Up/Down

        // local movement relative to player orientation
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredMove = forward * inputZ + right * inputX;
        desiredMove.Normalize();

        // speed (run when holding Left Shift)
        float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

        // keep vertical component separately
        moveDirection = desiredMove * speed;
        moveDirection.y = verticalVelocity;

        // move the controller
        cc.Move(moveDirection * Time.deltaTime);
    }

    private void HandleJumpAndGravity()
    {
        if (cc.isGrounded)
        {
            // small downward force to keep contact
            if (verticalVelocity < 0f)
                verticalVelocity = stickToGroundForce;

            if (Input.GetButtonDown("Jump"))
            {
                // v = sqrt(2 * g * h)
                verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // note: vertical velocity is applied in HandleMovement by setting moveDirection.y
    }

    // optional: expose method to teleport player (useful for spawn)
    public void TeleportTo(Vector3 worldPosition)
    {
        cc.enabled = false;
        transform.position = worldPosition;
        cc.enabled = true;
    }
}
