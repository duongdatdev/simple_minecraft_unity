using System.Collections;
using UnityEngine;

public class ZombieController : MonoBehaviour
{
    [Header("Stats")]
    public float moveSpeed = 1f; // Increased speed
    public float jumpForce = 5f;
    public float maxStepHeight = 1f; // maximum step height (in units)
    public float detectionRange = 20f;
    public float attackRange = 1.5f;
    public int damage = 5;
    public int maxHealth = 20;
    public int currentHealth;

    [Header("Scaling")]
    public float scalingFactor = 0.1f; // 10% increase per minute

    [Header("Combat")]
    public float attackCooldown = 1.0f;
    private float lastAttackTime = -999f;

    [Header("Feedback")]
    public float flashDuration = 0.15f;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashCoroutine;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] idleSounds;
    public AudioClip[] hurtSounds;
    public AudioClip deathSound;
    public AudioClip attackSound;
    public float idleSoundInterval = 5f;
    private float idleTimer;

    // Animator for switching animations when moving/attacking/being hurt/etc.
    private Animator animator;
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    // The following animation hashes are commented out until those animations are created in the Animator controller:
    // private static readonly int AttackHash = Animator.StringToHash("Attack");
    // private static readonly int HurtHash = Animator.StringToHash("Hurt");
    // private static readonly int DieHash = Animator.StringToHash("Die");
    // private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    // private static readonly int JumpHash = Animator.StringToHash("Jump");

    private Transform player;
    private Rigidbody rb;
    private bool isGrounded;

    [Header("Spawn / Grounding")]
    public float groundCheckDistance = 10f;
    public float groundCheckInterval = 0.5f;
    private bool waitingForGround = false;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
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
        audioSource.maxDistance = 20f;

        // Animator
        animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = GetComponent<Animator>();
        
        // Apply scaling based on game time (simulated by timeAlive for now, or use Time.time since level load)
        float difficultyMultiplier = 1f + (Time.timeSinceLevelLoad / 60f) * scalingFactor;
        maxHealth = Mathf.RoundToInt(maxHealth * difficultyMultiplier);
        damage = Mathf.RoundToInt(damage * difficultyMultiplier);
        currentHealth = maxHealth; 

        // If the world/chunk hasn't finished loading colliders yet the zombie might spawn in empty space.
        // Ensure we find ground before enabling normal physics and movement.
        EnsureOnGround();
    }

    void Update()
    {
        if (waitingForGround) return;

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

        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < detectionRange)
        {
            // Line of Sight Check
            if (CanSeePlayer())
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

                    // Attempt to step up if there's an obstacle while chasing the player
                    if (isGrounded)
                    {
                        TryStepUp(transform.forward);
                    }

                    // Only set velocity when we have a non-kinematic rigidbody
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.linearVelocity = new Vector3(moveDir.x, rb.linearVelocity.y, moveDir.z);
                    }
                }
                else
                {
                    // Attack player if cooldown elapsed
                    if (Time.time - lastAttackTime >= attackCooldown)
                    {
                        var pc = player.GetComponent<PlayerController>();
                        if (pc != null)
                        {
                            pc.TakeDamage(damage);
                            if (attackSound != null) audioSource.PlayOneShot(attackSound);
                        }
                        // Attack animation not set up yet; commented out until the animation exists in the Animator controller
                        // if (animator != null) animator.SetTrigger(AttackHash);
                        lastAttackTime = Time.time; 
                    }
                }
            }
        }

        // Update animator parameters based on current velocity
        if (animator != null)
        {
            bool moving = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude > 0.1f;
            animator.SetBool(IsMovingHash, moving);
        }
    }

    bool CanSeePlayer()
    {
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        // Raycast from eye level
        if (Physics.Raycast(transform.position + Vector3.up * 1.6f, dirToPlayer, out RaycastHit hit, detectionRange))
        {
            if (hit.transform.CompareTag("Player"))
            {
                return true;
            }
        }
        return false;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        FlashRed();
        if (hurtSounds.Length > 0)
            audioSource.PlayOneShot(hurtSounds[Random.Range(0, hurtSounds.Length)]);
        // Hurt animation not implemented yet; uncomment when ready
        // if (animator != null) animator.SetTrigger(HurtHash);
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

    void Die()
    {
        if (deathSound != null) 
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        // Play death animation if available, drop loot and destroy after a short delay to allow animation to play
        if (animator != null)
        {
            // Death animation not implemented yet; uncomment when ready
            // animator.SetTrigger(DieHash);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            rb.isKinematic = true;
            DropLoot();
            Destroy(gameObject, 0.5f);
        }
        else
        {
            // Drop items immediately if no animator present
            DropLoot();
            Destroy(gameObject);
        }
    }

    void DropLoot()
    {
        // 50% chance to drop Rotten Flesh (simulated by Porkchop for now or add Rotten Flesh)
        // 10% chance to drop Diamond
        // 20% chance to drop Iron Ingot

        if (Random.value < 0.5f)
        {
            SpawnItem("porkchop");
        }
        if (Random.value < 0.1f)
        {
            SpawnItem("diamond");
        }
        if (Random.value < 0.2f)
        {
            SpawnItem("iron_ingot");
        }

        if (Random.value < 0.05f)
        {
            SpawnItem("iron_sword");
        }
        
        if (Random.value < 0.5f)
        {
            SpawnItem("carrot");
        }
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
                        droppedItem.SetItem(item, 1);
                    }
                }
            }
            else
            {
                Debug.LogWarning("ZombieController: Could not find BlockInteraction or droppedItemPrefab on Player or its children.");
            }
        }
    }

    // Ensure the zombie doesn't spawn into empty space when chunks haven't loaded.
    private void EnsureOnGround()
    {
        if (waitingForGround) return;
        Vector3 origin = transform.position;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance))
        {
            float desiredY = hit.point.y + 0.1f;
            if (transform.position.y < desiredY - 0.01f)
            {
                transform.position = new Vector3(transform.position.x, desiredY, transform.position.z);
            }
            if (rb != null) rb.isKinematic = false;
            isGrounded = true;
            waitingForGround = false;
        }
        else
        {
            if (rb != null) rb.isKinematic = true;
            waitingForGround = true;
            StartCoroutine(WaitForGroundRoutine());
        }
    }

    private System.Collections.IEnumerator WaitForGroundRoutine()
    {
        while (true)
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance * 2f))
            {
                transform.position = new Vector3(transform.position.x, hit.point.y + 0.1f, transform.position.z);
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.linearVelocity = Vector3.zero;
                }
                isGrounded = true;
                waitingForGround = false;
                yield break;
            }
            yield return new WaitForSeconds(groundCheckInterval);
        }
    }

    void FixedUpdate()
    {
        if (waitingForGround) return;
        // Keep checking for obstacles each physics step so the zombie can step up while moving.
        TryStepUp(transform.forward);
    }

    // Attempts a step up in the given forward direction. Returns true if a step/jump was initiated.
    private bool TryStepUp(Vector3 forwardDir)
    {
        float checkDistance = 0.75f;
        float footOffset = 0.1f;
        float headHeight = 2.0f;

        Vector3 origin = transform.position + Vector3.up * footOffset;
        if (Physics.Raycast(origin, forwardDir, out RaycastHit hit, checkDistance))
        {
            Vector3 probePoint = transform.position + forwardDir * (hit.distance + 0.1f) + Vector3.up * headHeight;
            if (Physics.Raycast(probePoint, Vector3.down, out RaycastHit groundHit, headHeight + 0.2f))
            {
                float stepHeight = groundHit.point.y - transform.position.y;
                if (stepHeight > 0.05f && stepHeight <= maxStepHeight)
                {
                    Vector3 clearanceCheck = new Vector3(groundHit.point.x, groundHit.point.y + 0.5f, groundHit.point.z);
                    if (!Physics.CheckSphere(clearanceCheck, 0.25f))
                    {
                        if (isGrounded && rb != null && !rb.isKinematic)
                        {
                            float requiredV = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * (stepHeight + 0.05f));
                            Vector3 current = rb.linearVelocity;
                            rb.linearVelocity = new Vector3(current.x, requiredV, current.z);
                            isGrounded = false;
                            // Jump animation not implemented yet; uncomment when ready
                            // if (animator != null)
                            // {
                            //     animator.SetTrigger(JumpHash);
                            //     animator.SetBool(IsGroundedHash, false);
                            // }
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    void OnCollisionStay(Collision collision)
    {
        // Check if colliding with ground (normals pointing up)
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.7f)
            {
                isGrounded = true;
                // Grounded animation not implemented yet
                // if (animator != null) animator.SetBool(IsGroundedHash, true);
                return; 
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
