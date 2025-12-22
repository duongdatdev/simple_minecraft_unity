using UnityEngine;

/// <summary>
/// Dropped item entity in the world
/// Players can pick up by walking near it
/// Has physics and lifetime
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class DroppedItem : MonoBehaviour
{
    [Header("Item Data")]
    public ItemStack itemStack;

    [Header("Pickup Settings")]
    public float pickupRadius = 3.0f; // Magnet radius
    public float pickupDistance = 1.0f; // Actual pickup distance
    public float pickupDelay = 0.5f; // delay before can be picked up (prevent instant pickup)
    public float magnetSpeed = 8f; // speed item moves towards player
    
    [Header("Lifetime")]
    public float lifetime = 300f; // 5 minutes before despawn
    
    [Header("Visual")]
    public GameObject visualModel; // 3D model or sprite
    public float rotationSpeed = 90f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    [Header("Audio")]
    public AudioClip pickupSound;

    private Rigidbody rb;
    private SphereCollider pickupCollider;
    private float spawnTime;
    private Vector3 startPosition;
    private Transform targetPlayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        pickupCollider = GetComponent<SphereCollider>();
        
        // Setup physics
        rb.mass = 0.1f;
        rb.linearDamping = 1f;
        rb.angularDamping = 0.5f;
        
        // Setup physics collider (BoxCollider) to prevent falling through ground
        BoxCollider physicsCollider = GetComponent<BoxCollider>();
        if (physicsCollider == null)
        {
            physicsCollider = gameObject.AddComponent<BoxCollider>();
            physicsCollider.size = new Vector3(0.3f, 0.3f, 0.3f);
        }
        physicsCollider.isTrigger = false;
        
        // Setup pickup collider (SphereCollider)
        pickupCollider.isTrigger = true;
        pickupCollider.radius = pickupRadius;
        
        spawnTime = Time.time;
        startPosition = transform.position;
    }

    private void Update()
    {
        // Check lifetime
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Visual effects
        if (visualModel != null)
        {
            // Rotate
            visualModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Bob up and down
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visualModel.transform.localPosition = new Vector3(0, bobOffset, 0);
        }

        // Magnet effect towards player (if close enough)
        if (targetPlayer != null && Time.time - spawnTime > pickupDelay)
        {
            float distance = Vector3.Distance(transform.position, targetPlayer.position);

            // Move towards player
            Vector3 direction = (targetPlayer.position - transform.position).normalized;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, direction * magnetSpeed, Time.deltaTime * 5f);

            // Pickup if close enough
            if (distance < pickupDistance)
            {
                TryPickup(targetPlayer.gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if player entered pickup range
        if (other.CompareTag("Player"))
        {
            targetPlayer = other.transform;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Keep tracking player for magnet effect
        if (other.CompareTag("Player") && Time.time - spawnTime > pickupDelay)
        {
            targetPlayer = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && targetPlayer == other.transform)
        {
            targetPlayer = null;
        }
    }

    /// <summary>
    /// Initialize the dropped item with an item stack
    /// </summary>
    public void SetItem(Item item, int count = 1)
    {
        itemStack = new ItemStack(item, count);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (itemStack == null || itemStack.item == null) return;

        // Create visual if missing
        if (visualModel == null)
        {
            visualModel = new GameObject("Visual");
            visualModel.transform.SetParent(transform, false);
        }

        // Check if it's a block or item
        if (itemStack.item.itemType == ItemType.Block)
        {
            // Create block mesh (simplified)
            MeshFilter mf = visualModel.GetComponent<MeshFilter>();
            if (mf == null) mf = visualModel.AddComponent<MeshFilter>();
            
            MeshRenderer mr = visualModel.GetComponent<MeshRenderer>();
            if (mr == null) mr = visualModel.AddComponent<MeshRenderer>();

            // Create a cube
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mf.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);

            // Set texture (this requires a material with the block texture, which is complex to get here without reference)
            // For now, just set color or leave as default
            mr.material = new Material(Shader.Find("Standard"));
            if (itemStack.item.icon != null)
                mr.material.mainTexture = itemStack.item.icon.texture;
        }
        else
        {
            // Create sprite for item
            SpriteRenderer sr = visualModel.GetComponent<SpriteRenderer>();
            if (sr == null) sr = visualModel.AddComponent<SpriteRenderer>();
            
            sr.sprite = itemStack.item.icon;
            visualModel.transform.localScale = Vector3.one * 0.5f;
        }
    }

    /// <summary>
    /// Try to pickup item
    /// </summary>
    private void TryPickup(GameObject player)
    {
        // Check pickup delay
        if (Time.time - spawnTime < pickupDelay)
            return;

        // Get player inventory
        Inventory inventory = player.GetComponent<Inventory>();
        if (inventory == null) return;

        // Try to add to inventory
        bool added = inventory.AddItem(itemStack.item, itemStack.count);
        
        if (added)
        {
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            Debug.Log($"Picked up {itemStack.GetDisplayString()}");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("Inventory full!");
        }
    }

    /// <summary>
    /// Initialize dropped item with item stack
    /// </summary>
    public void Initialize(ItemStack stack)
    {
        itemStack = stack.Clone();
        
        // TODO: Set visual model based on item type
        UpdateVisual();
    }

    /// <summary>
    /// Initialize with item and count
    /// </summary>
    public void Initialize(Item item, int count)
    {
        itemStack = new ItemStack(item, count);
        UpdateVisual();
    }

    /// <summary>
    /// Update visual representation
    /// </summary>
    private void UpdateVisual()
    {
        if (itemStack == null || itemStack.IsEmpty()) return;
        
        if (visualModel == null)
        {
            visualModel = new GameObject("Visual");
            visualModel.transform.SetParent(transform);
            visualModel.transform.localPosition = Vector3.zero;
            visualModel.transform.localScale = Vector3.one * 0.5f;
        }

        // Try to use SpriteRenderer
        SpriteRenderer sr = visualModel.GetComponent<SpriteRenderer>();
        if (sr == null) sr = visualModel.AddComponent<SpriteRenderer>();

        if (itemStack.item != null && itemStack.item.icon != null)
        {
            sr.sprite = itemStack.item.icon;
        }
        else
        {
            // Fallback: Create a cube if no icon
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(visualModel.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one;
            Destroy(cube.GetComponent<Collider>());
        }
    }

    /// <summary>
    /// Add initial force (for throwing items)
    /// </summary>
    public void AddLaunchForce(Vector3 force)
    {
        if (rb != null)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    /// <summary>
    /// Spawn dropped item at position
    /// </summary>
    public static DroppedItem SpawnDroppedItem(Vector3 position, ItemStack stack, Vector3 launchForce = default)
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/DroppedItem");
        
        // If no prefab, create simple gameobject
        if (prefab == null)
        {
            GameObject obj = new GameObject("DroppedItem");
            obj.transform.position = position;
            obj.tag = "Item"; 
            obj.layer = LayerMask.NameToLayer("Default");
            
            DroppedItem item = obj.AddComponent<DroppedItem>();
            // Rigidbody and SphereCollider are added automatically via [RequireComponent]
            
            item.Initialize(stack);
            
            if (launchForce != Vector3.zero)
            {
                item.AddLaunchForce(launchForce);
            }
            
            return item;
        }
        else
        {
            GameObject obj = Instantiate(prefab, position, Quaternion.identity);
            DroppedItem item = obj.GetComponent<DroppedItem>();
            
            if (item != null)
            {
                item.Initialize(stack);
                
                if (launchForce != Vector3.zero)
                {
                    item.AddLaunchForce(launchForce);
                }
            }
            
            return item;
        }
    }
}
