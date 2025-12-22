using UnityEngine;

/// <summary>
/// Mouse look for first-person camera. Attach to the Camera (child of Player).
/// - Yaw (player transform) is applied to parent
/// - Pitch (camera) is applied to camera local rotation
/// </summary>
public class MouseLook : MonoBehaviour
{
    public Transform playerBody; // assign the Player GameObject (parent)
    public float mouseSensitivity = 2.0f;

    private float xRotation = 0f; // pitch
    private InventoryUI inventoryUI; // cache reference to UI

    void Start()
    {
        if (playerBody == null)
        {
            playerBody = transform.parent;
        }

        // initialize rotation
        xRotation = transform.localEulerAngles.x;

        // cache inventory UI if present
        inventoryUI = Object.FindAnyObjectByType<InventoryUI>();
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // yaw rotates the player body
        playerBody.Rotate(Vector3.up * mouseX);

        // pitch rotates the camera around local X axis
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);
        transform.localEulerAngles = new Vector3(xRotation, 0f, 0f);

        // ensure cached reference (in case InventoryUI is created after this script starts)
        if (inventoryUI == null)
            inventoryUI = Object.FindAnyObjectByType<InventoryUI>();

        // unlock cursor when pressing Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        // Only lock/hide cursor on left-click when inventory is NOT open
        else if (Input.GetMouseButtonDown(0) && (inventoryUI == null || !inventoryUI.IsInventoryOpen()))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}