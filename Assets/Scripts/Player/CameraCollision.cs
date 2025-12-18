using UnityEngine;

/// <summary>
/// Prevents the camera from clipping through walls/blocks in first-person view.
/// Attach this to the Camera GameObject (child of Player).
/// Uses aggressive near clip plane adjustment to prevent seeing through walls.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraCollision : MonoBehaviour
{
    [Header("Collision Settings")]
    [Tooltip("The player's transform (parent)")]
    public Transform playerTransform;

    [Tooltip("Distance to check for walls around camera")]
    public float checkDistance = 1.0f;

    [Tooltip("Radius for sphere cast collision detection")]
    public float collisionRadius = 0.2f;

    [Tooltip("Default near clip plane value")]
    public float defaultNearClipPlane = 0.01f;

    [Tooltip("Maximum near clip plane when very close to wall")]
    public float maxNearClipPlane = 1.5f;

    [Tooltip("Layer mask for objects that should block the camera")]
    public LayerMask collisionLayerMask = ~0; // all layers by default

    [Tooltip("Smooth speed for adjustments")]
    public float smoothSpeed = 25f;

    [Tooltip("Number of rays to cast in different directions")]
    public int rayCount = 16;

    private Camera cam;
    private float targetNearClip;

    void Start()
    {
        // Get components
        cam = GetComponent<Camera>();
        
        if (playerTransform == null)
        {
            playerTransform = transform.parent;
        }
        
        // Set default near clip plane
        targetNearClip = defaultNearClipPlane;
        cam.nearClipPlane = defaultNearClipPlane;
    }

    void LateUpdate()
    {
        if (playerTransform == null || cam == null) return;

        // Get current camera position
        Vector3 cameraPos = transform.position;
        Vector3 cameraForward = transform.forward;
        Vector3 cameraUp = transform.up;
        Vector3 cameraRight = transform.right;
        
        // Reset target near clip
        targetNearClip = defaultNearClipPlane;
        float minWallDistance = checkDistance;
        bool wallDetected = false;

        // Create a grid of rays covering the camera's field of view
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(rayCount));
        float fov = cam.fieldOfView;
        float aspect = cam.aspect;
        
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                // Calculate direction for this ray
                float xOffset = (x / (float)(gridSize - 1) - 0.5f) * 2f; // -1 to 1
                float yOffset = (y / (float)(gridSize - 1) - 0.5f) * 2f; // -1 to 1
                
                // Convert to angles based on FOV
                float horizontalAngle = xOffset * fov * aspect * 0.5f;
                float verticalAngle = yOffset * fov * 0.5f;
                
                // Create direction vector
                Vector3 direction = cameraForward;
                direction = Quaternion.AngleAxis(horizontalAngle, cameraUp) * direction;
                direction = Quaternion.AngleAxis(verticalAngle, cameraRight) * direction;
                direction.Normalize();
                
                // Cast ray
                RaycastHit hit;
                if (Physics.SphereCast(cameraPos, collisionRadius, direction, out hit, checkDistance, collisionLayerMask))
                {
                    wallDetected = true;
                    if (hit.distance < minWallDistance)
                    {
                        minWallDistance = hit.distance;
                    }
                }
            }
        }

        // Also cast rays in cardinal directions for extra coverage
        Vector3[] cardinalDirections = new Vector3[]
        {
            cameraForward,
            cameraForward + cameraUp * 0.3f,
            cameraForward - cameraUp * 0.3f,
            cameraForward + cameraRight * 0.3f,
            cameraForward - cameraRight * 0.3f,
        };

        foreach (Vector3 dir in cardinalDirections)
        {
            RaycastHit hit;
            if (Physics.SphereCast(cameraPos, collisionRadius, dir.normalized, out hit, checkDistance, collisionLayerMask))
            {
                wallDetected = true;
                if (hit.distance < minWallDistance)
                {
                    minWallDistance = hit.distance;
                }
            }
        }

        // Calculate near clip plane based on distance to closest wall
        if (wallDetected && minWallDistance < checkDistance)
        {
            // Use exponential scaling for near clip plane
            // The closer the wall, the more aggressive the near clip
            float normalizedDistance = minWallDistance / checkDistance;
            
            // Exponential curve: very aggressive when close
            float clipFactor = 1f - Mathf.Pow(normalizedDistance, 0.5f);
            targetNearClip = Mathf.Lerp(defaultNearClipPlane, maxNearClipPlane, clipFactor);
            
            // Ensure minimum near clip based on distance
            targetNearClip = Mathf.Max(targetNearClip, minWallDistance * 0.8f);
        }

        // Smooth interpolation for near clip plane
        cam.nearClipPlane = Mathf.Lerp(cam.nearClipPlane, targetNearClip, smoothSpeed * Time.deltaTime);
    }
}
