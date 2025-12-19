using UnityEngine;

public class ZombieController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpForce = 5f;
    public float detectionRange = 20f;
    public float attackRange = 1.5f;
    public int damage = 10;
    
    private Transform player;
    private Rigidbody rb;
    private bool isGrounded;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < detectionRange)
        {
            // Look at player
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // Move
            if (dist > attackRange)
            {
                Vector3 moveDir = transform.forward * moveSpeed;
                rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
            }
        }
    }

    void FixedUpdate()
    {
        // Simple jump check: if blocked ahead, try to jump
        // Raycast slightly above feet
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, transform.forward, 1f))
        {
            if (isGrounded)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                isGrounded = false; // Prevent double jump immediately
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Check if colliding with ground (normals pointing up)
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
