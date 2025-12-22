using System.Collections;
using UnityEngine;

public class PigController : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float jumpForce = 5f;
    public float maxStepHeight = 1f; // maximum step height (in units)
    public int maxHealth = 10;
    public int currentHealth;
    
    private Rigidbody rb;
    private bool isGrounded;

    // Visual feedback
    public float flashDuration = 0.15f;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashCoroutine;
    
    // Wander vars
    private float wanderTimer;
    private float wanderInterval = 3f;
    private Vector3 wanderDirection;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] idleSounds;
    public AudioClip[] hurtSounds;
    public AudioClip deathSound;
    public float idleSoundInterval = 5f;
    private float idleTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) originalColors[i] = renderers[i].material.color;
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // 3D sound
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 15f;

        currentHealth = maxHealth;
        PickNewWanderDirection();
    }

    void Update()
    {
        // Idle sounds
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0)
        {
            if (idleSounds.Length > 0)
            {
                audioSource.PlayOneShot(idleSounds[Random.Range(0, idleSounds.Length)]);
            }
            idleTimer = idleSoundInterval + Random.Range(-2f, 2f);
        }

        Wander();
    }

    void Wander()
    {
        wanderTimer += Time.deltaTime;
        if (wanderTimer >= wanderInterval)
        {
            PickNewWanderDirection();
            wanderTimer = 0;
        }

        // Move
        Vector3 moveDir = wanderDirection * moveSpeed;
        rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
        
        // Rotate
        if (wanderDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(wanderDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    void PickNewWanderDirection()
    {
        float x = Random.Range(-1f, 1f);
        float z = Random.Range(-1f, 1f);
        wanderDirection = new Vector3(x, 0, z).normalized;
        wanderInterval = Random.Range(2f, 5f);
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        // Visual feedback
        FlashRed();
        if (hurtSounds.Length > 0)
            audioSource.PlayOneShot(hurtSounds[Random.Range(0, hurtSounds.Length)]);

        // Run away when hit
        wanderDirection = transform.forward; 
        moveSpeed = 5f; // Panic speed
        Invoke("ResetSpeed", 3f);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void FlashRed()
    {
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = Color.red;
        }
        yield return new WaitForSeconds(flashDuration);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].material.color = originalColors[i];
        }
        flashCoroutine = null;
    }

    void ResetSpeed()
    {
        moveSpeed = 2f;
    }

    void Die()
    {
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }
        SpawnItem("porkchop");
        Destroy(gameObject);
    }

    void SpawnItem(string itemName)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            // BlockInteraction is likely on the Camera (child of Player)
            BlockInteraction blockInteraction = playerObj.GetComponentInChildren<BlockInteraction>();
            if (blockInteraction != null && blockInteraction.droppedItemPrefab != null)
            {
                GameObject droppedItemObj = Instantiate(blockInteraction.droppedItemPrefab, transform.position + Vector3.up, Quaternion.identity);
                DroppedItem droppedItem = droppedItemObj.GetComponent<DroppedItem>();
                if (droppedItem != null)
                {
                    Item item = ItemDatabase.Instance.GetItem(itemName);
                    if (item != null)
                    {
                        droppedItem.SetItem(item, Random.Range(1, 3));
                    }
                }
            }
            else
            {
                Debug.LogWarning("PigController: Could not find BlockInteraction or droppedItemPrefab on Player or its children.");
            }
        }
    }
    
    void FixedUpdate()
    {
        // Improved step-up logic: detect an obstacle ahead and determine its height.
        float checkDistance = 0.75f;
        float footOffset = 0.1f;
        float headHeight = 2.0f;
        // use public field maxStepHeight for step limit

        Vector3 origin = transform.position + Vector3.up * footOffset;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, checkDistance))
        {
            Vector3 probePoint = transform.position + transform.forward * (hit.distance + 0.1f) + Vector3.up * headHeight;
            if (Physics.Raycast(probePoint, Vector3.down, out RaycastHit groundHit, headHeight + 0.2f))
            {
                float stepHeight = groundHit.point.y - transform.position.y;
                if (stepHeight > 0.05f && stepHeight <= maxStepHeight)
                {
                    Vector3 clearanceCheck = new Vector3(groundHit.point.x, groundHit.point.y + 0.5f, groundHit.point.z);
                    if (!Physics.CheckSphere(clearanceCheck, 0.25f))
                    {
                        if (isGrounded)
                        {
                            float requiredV = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * (stepHeight + 0.05f));
                            rb.linearVelocity = new Vector3(rb.linearVelocity.x, requiredV, rb.linearVelocity.z);
                            isGrounded = false;
                        }
                    }
                }
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
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
