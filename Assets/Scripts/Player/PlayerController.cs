using UnityEngine;
using UnityEngine.UI;

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
    public Inventory inventory; // Reference to inventory for armor calculation

    [Header("Stats")]
    public int maxHealth = 20;
    public float currentHealth;
    public int maxHunger = 20;
    public float currentHunger;
    public float hungerDepletionRate = 0.1f; // Hunger lost per second while moving
    public float healRate = 1f; // Health gained per second when full hunger

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] footstepSounds;
    public AudioClip jumpSound;
    public AudioClip hurtSound;
    public float footstepInterval = 0.5f;
    private float footstepTimer;

    private CharacterController cc;
    private float verticalVelocity = 0f;
    private Vector3 moveDirection = Vector3.zero;
    private float healTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        currentHunger = maxHunger;

        cc = GetComponent<CharacterController>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        
        if (inventory == null)
            inventory = GetComponent<Inventory>();

        // NOTE: PlayerArm and PlayerCombat are NOT auto-added anymore.
        // Please add them manually to your Camera GameObject and configure them in the Inspector if you want first-person arms or melee combat.

        // Runtime validation: log a friendly warning so you don't forget to add them
        if (cameraTransform != null)
        {
            if (cameraTransform.GetComponent<PlayerArm>() == null)
            {
                Debug.LogWarning("PlayerController: PlayerArm component is missing on the Camera. Add the 'PlayerArm' script to the Camera to enable first-person arms.");
            }
            if (cameraTransform.GetComponent<PlayerCombat>() == null)
            {
                Debug.LogWarning("PlayerController: PlayerCombat component is missing on the Camera. Add the 'PlayerCombat' script to the Camera to enable melee combat (left click attacks).");
            }
            if (cameraTransform.GetComponent<PlayerHeldItem>() == null)
            {
                Debug.LogWarning("PlayerController: PlayerHeldItem component is missing on the Camera. Add the 'PlayerHeldItem' script to the Camera to enable held item rendering.");
            }
        }

        // Auto-create HUD if missing
        if (Object.FindAnyObjectByType<HUDManager>() == null)
        {
            GameObject hudObj = new GameObject("HUD");
            HUDManager hud = hudObj.AddComponent<HUDManager>();
            
            // Create Canvas
            GameObject canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(hudObj.transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Health Bar Parent
            GameObject healthParent = new GameObject("HealthBar");
            healthParent.transform.SetParent(canvasObj.transform);
            RectTransform healthRect = healthParent.AddComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0.5f, 0);
            healthRect.anchorMax = new Vector2(0.5f, 0);
            healthRect.pivot = new Vector2(0.5f, 0);
            healthRect.anchoredPosition = new Vector2(-150, 50);
            HorizontalLayoutGroup healthLayout = healthParent.AddComponent<HorizontalLayoutGroup>();
            healthLayout.childControlWidth = false;
            healthLayout.childControlHeight = false;
            healthLayout.spacing = 2;
            hud.healthBarParent = healthParent.transform;

            // Create Hunger Bar Parent
            GameObject hungerParent = new GameObject("HungerBar");
            hungerParent.transform.SetParent(canvasObj.transform);
            RectTransform hungerRect = hungerParent.AddComponent<RectTransform>();
            hungerRect.anchorMin = new Vector2(0.5f, 0);
            hungerRect.anchorMax = new Vector2(0.5f, 0);
            hungerRect.pivot = new Vector2(0.5f, 0);
            hungerRect.anchoredPosition = new Vector2(150, 50);
            HorizontalLayoutGroup hungerLayout = hungerParent.AddComponent<HorizontalLayoutGroup>();
            hungerLayout.childControlWidth = false;
            hungerLayout.childControlHeight = false;
            hungerLayout.spacing = 2;
            hungerLayout.reverseArrangement = true; // Right to left
            hud.hungerBarParent = hungerParent.transform;

            // Create simple prefabs for icons (red/brown squares if sprites missing)
            hud.heartPrefab = CreateIconPrefab("HeartIcon", Color.red);
            hud.foodPrefab = CreateIconPrefab("FoodIcon", new Color(0.6f, 0.4f, 0.2f));
        }

        // lock the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    GameObject CreateIconPrefab(string name, Color color)
    {
        GameObject go = new GameObject(name);
        Image img = go.AddComponent<Image>();
        img.color = color;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);
        return go;
    }

    void Update()
    {
        HandleMovement();
        HandleJumpAndGravity();
        HandleStats();
    }

    private void HandleStats()
    {
        // Deplete hunger if moving
        if (cc.velocity.magnitude > 0.1f)
        {
            currentHunger -= hungerDepletionRate * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftShift)) // Sprinting consumes more
                currentHunger -= hungerDepletionRate * 0.5f * Time.deltaTime;
        }
        
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        // Heal if hunger is full
        if (currentHunger > 18 && currentHealth < maxHealth)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= 4f) // Heal every 4 seconds
            {
                Heal(1);
                currentHunger -= 1; // Healing consumes hunger
                healTimer = 0;
            }
        }
        else
        {
            healTimer = 0;
        }

        // Starvation
        if (currentHunger <= 0)
        {
            healTimer += Time.deltaTime;
            if (healTimer >= 4f)
            {
                TakeDamage(1);
                healTimer = 0;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        // Calculate armor reduction
        int armorPoints = 0;
        if (inventory != null)
        {
            foreach (var slot in inventory.armorSlots)
            {
                if (slot != null && slot.item != null)
                {
                    armorPoints += slot.item.armorPoints;
                }
            }
        }

        // Simple armor formula: each point reduces damage by 4%, max 80%
        float reduction = Mathf.Clamp01(armorPoints * 0.04f);
        int finalDamage = Mathf.RoundToInt(damage * (1 - reduction));
        if (finalDamage < 1) finalDamage = 1;

        currentHealth -= finalDamage;
        if (hurtSound != null) audioSource.PlayOneShot(hurtSound);
        Debug.Log($"Player took {finalDamage} damage. Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
    }

    public void Eat(int hungerAmount, int healAmount)
    {
        currentHunger += hungerAmount;
        if (currentHunger > maxHunger) currentHunger = maxHunger;
        Heal(healAmount);
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // Respawn or Game Over logic
        TeleportTo(new Vector3(0, 100, 0)); // Simple respawn
        currentHealth = maxHealth;
        currentHunger = maxHunger;
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

        // Footsteps
        if (cc.isGrounded && cc.velocity.magnitude > 2f && footstepSounds.Length > 0)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0)
            {
                PlayRandomFootstep();
                footstepTimer = Input.GetKey(KeyCode.LeftShift) ? footstepInterval * 0.6f : footstepInterval;
            }
        }
    }

    private void PlayRandomFootstep()
    {
        if (footstepSounds.Length == 0) return;
        int index = Random.Range(0, footstepSounds.Length);
        audioSource.PlayOneShot(footstepSounds[index]);
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
                if (jumpSound != null) audioSource.PlayOneShot(jumpSound);
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
