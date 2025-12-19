using UnityEngine;

/// <summary>
/// Player block interaction with inventory integration:
/// - Left click (Hold): break block (drop item to inventory)
/// - Right click: place block (consume from inventory)
/// - Uses WorldGenerator.Instance.SetBlockAtGlobal
/// - Raycast uses camera forward and returns global coordinates
/// </summary>
[RequireComponent(typeof(Camera))]
public class BlockInteraction : MonoBehaviour
{
    public Camera playerCamera; // assign in inspector (Main Camera)
    public float reach = 6f;
    public LayerMask chunkLayer;
    public KeyCode breakKey = KeyCode.Mouse0;
    public KeyCode placeKey = KeyCode.Mouse1;

    [Header("Drops")] public GameObject droppedItemPrefab;

    [Header("Inventory Integration")] public Inventory playerInventory;
    public InventoryUI inventoryUI;

    [Header("Breaking Animation")]
    public float defaultBreakTime = 0.5f;
    public Vector2Int[] breakStageTiles = new Vector2Int[10]; // Coordinates of the 10 break stages in the atlas
    public Material breakOverlayMaterial; // Material for the overlay (should use the atlas texture)
    public float overlayOffset = 0.001f; // Offset to prevent z-fighting

    private float currentBreakProgress = 0f;
    private Vector3Int currentBreakBlock = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
    private GameObject breakOverlay;
    private MeshFilter breakOverlayMesh;
    private MeshRenderer breakOverlayRenderer;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerInventory == null) playerInventory = FindObjectOfType<Inventory>();
        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
        
        CreateBreakOverlay();
    }

    void CreateBreakOverlay()
    {
        breakOverlay = new GameObject("BreakOverlay");
        breakOverlayMesh = breakOverlay.AddComponent<MeshFilter>();
        breakOverlayRenderer = breakOverlay.AddComponent<MeshRenderer>();
        
        // Create a simple quad mesh
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        breakOverlayMesh.mesh = mesh;

        if (breakOverlayMaterial != null)
        {
            breakOverlayRenderer.material = breakOverlayMaterial;
        }
        else
        {
            // Try to find a default material or warn
            Debug.LogWarning("BlockInteraction: Break Overlay Material is missing! Please assign it in Inspector.");
            breakOverlayRenderer.enabled = false;
        }
        
        breakOverlay.SetActive(false);
    }

    void Update()
    {
        // Don't interact with blocks when inventory UI is open
        if (inventoryUI != null && inventoryUI.IsInventoryOpen())
        {
            ResetBreaking();
            return;
        }

        if (Input.GetKey(breakKey)) 
        {
            UpdateBreaking();
        }
        else
        {
            ResetBreaking();
        }

        if (Input.GetKeyDown(placeKey)) HandleRightClick();
    }

    void UpdateBreaking()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach, chunkLayer))
        {
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            
            // If we hit something that is not a chunk (like an item), try to find chunk behind it
            if (chunkComp == null)
            {
                RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, reach, chunkLayer);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    Chunk c = h.collider.GetComponent<Chunk>();
                    if (c != null)
                    {
                        hit = h;
                        chunkT = h.collider.transform;
                        chunkComp = c;
                        break;
                    }
                }
            }

            if (chunkComp == null) 
            {
                ResetBreaking();
                return;
            }

            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);
            Vector3 localInside = localHit - localNormal * 0.01f;
            
            int lx = Mathf.FloorToInt(localInside.x);
            int ly = Mathf.FloorToInt(localInside.y);
            int lz = Mathf.FloorToInt(localInside.z);
            
            int globalX = lx + chunkComp.ChunkX * BlockData.ChunkWidth;
            int globalZ = lz + chunkComp.ChunkZ * BlockData.ChunkWidth;
            
            Vector3Int targetBlock = new Vector3Int(globalX, ly, globalZ);
            
            // Check if block is air (already broken)
            if (chunkComp.GetBlockLocal(lx, ly, lz) == BlockType.Air)
            {
                ResetBreaking();
                return;
            }

            if (targetBlock != currentBreakBlock)
            {
                // New block target
                currentBreakProgress = 0f;
                currentBreakBlock = targetBlock;
                
                // Update overlay position/rotation to match face
                UpdateOverlayTransform(hit, chunkT);
                breakOverlay.SetActive(true);
            }
            
            // Increase progress
            // TODO: Adjust speed based on tool efficiency
            float speedMultiplier = 1.0f;
            if (playerInventory != null)
            {
                ItemStack tool = playerInventory.GetSelectedItemStack();
                if (!tool.IsEmpty() && tool.item.itemType == ItemType.Tool)
                {
                    // Simple speed boost for tools
                    speedMultiplier = 2.0f; 
                }
            }

            currentBreakProgress += (Time.deltaTime / defaultBreakTime) * speedMultiplier;
            
            if (currentBreakProgress >= 1.0f)
            {
                // Break!
                BreakBlock(chunkComp, lx, ly, lz, globalX, globalZ);
                ResetBreaking();
            }
            else
            {
                // Update visual stage
                UpdateOverlayVisual(currentBreakProgress);
            }
        }
        else
        {
            ResetBreaking();
        }
    }

    void ResetBreaking()
    {
        currentBreakProgress = 0f;
        currentBreakBlock = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        if (breakOverlay != null) breakOverlay.SetActive(false);
    }

    void UpdateOverlayTransform(RaycastHit hit, Transform chunkT)
    {
        // Position slightly off the face
        breakOverlay.transform.position = hit.point + hit.normal * overlayOffset;
        
        // Rotate to face the normal
        breakOverlay.transform.rotation = Quaternion.LookRotation(-hit.normal);
        
        // Snap to block grid center for cleaner look?
        // Actually, hit.point is exact. For blocky look, we want it centered on the face.
        Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
        Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);
        Vector3 localInside = localHit - localNormal * 0.01f;
        
        int lx = Mathf.FloorToInt(localInside.x);
        int ly = Mathf.FloorToInt(localInside.y);
        int lz = Mathf.FloorToInt(localInside.z);
        
        Vector3 blockCenterLocal = new Vector3(lx + 0.5f, ly + 0.5f, lz + 0.5f);
        Vector3 faceCenterLocal = blockCenterLocal + localNormal * 0.5f;
        Vector3 faceCenterGlobal = chunkT.TransformPoint(faceCenterLocal);
        
        breakOverlay.transform.position = faceCenterGlobal + hit.normal * overlayOffset;
        breakOverlay.transform.rotation = Quaternion.LookRotation(-hit.normal);
    }

    void UpdateOverlayVisual(float progress)
    {
        if (breakStageTiles == null || breakStageTiles.Length == 0) return;
        
        int stage = Mathf.FloorToInt(progress * 10f);
        stage = Mathf.Clamp(stage, 0, 9);
        
        // Map stage to user defined tiles
        if (stage < breakStageTiles.Length)
        {
            Vector2Int tile = breakStageTiles[stage];
            Vector2[] uvs = TextureAtlas.GetUVsFromTile(tile.x, tile.y);
            
            // Update mesh UVs
            Mesh mesh = breakOverlayMesh.mesh;
            mesh.uv = uvs;
        }
    }

    void BreakBlock(Chunk chunkComp, int lx, int ly, int lz, int globalX, int globalZ)
    {
        // Get block type before breaking
        BlockType blockType = chunkComp.GetBlockLocal(lx, ly, lz);

        // Perform break: set to Air
        WorldGenerator.Instance.SetBlockAtGlobal(globalX, ly, globalZ, BlockType.Air);

        // Drop item in world
        if (ItemDatabase.Instance != null)
        {
            Item itemToDrop = ItemDatabase.Instance.GetItemForBlock(blockType);
            if (itemToDrop != null)
            {
                // Spawn at exact block center
                Vector3 blockCenter = new Vector3(globalX + 0.5f, ly + 0.5f, globalZ + 0.5f);
                SpawnDroppedItem(itemToDrop, blockCenter);
            }
        }

        // Damage tool if equipped
        if (playerInventory != null)
        {
            ItemStack selectedStack = playerInventory.GetSelectedItemStack();
            if (!selectedStack.IsEmpty() && selectedStack.item.itemType == ItemType.Tool)
            {
                bool broke = playerInventory.DamageSelectedTool(1);
                if (broke)
                {
                    Debug.Log("Tool broke!");
                    // TODO: Play break sound
                }
            }
        }
    }

    void HandleRightClick()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach,
                chunkLayer))
        {
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            if (chunkComp == null) return;

            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);

            // Check for interaction (block we hit)
            Vector3 localInside = localHit - localNormal * 0.01f;
            int bx = Mathf.FloorToInt(localInside.x);
            int by = Mathf.FloorToInt(localInside.y);
            int bz = Mathf.FloorToInt(localInside.z);

            BlockType hitBlock = chunkComp.GetBlockLocal(bx, by, bz);
            if (hitBlock == BlockType.CraftingTable)
            {
                if (inventoryUI != null)
                {
                    inventoryUI.OpenCraftingTable();
                    return; // Interaction consumed the click
                }
            }

            // Proceed to placement logic
            TryPlace(hit, chunkT, chunkComp, localHit, localNormal);
        }
    }

    void TryPlace(RaycastHit hit, Transform chunkT, Chunk chunkComp, Vector3 localHit, Vector3 localNormal)
    {
        // to place block on the face we hit, step slightly outside towards normal
        Vector3 localOutside = localHit + localNormal * 0.01f;
        int lx = Mathf.FloorToInt(localOutside.x);
        int ly = Mathf.FloorToInt(localOutside.y);
        int lz = Mathf.FloorToInt(localOutside.z);

        int globalX = lx + chunkComp.ChunkX * BlockData.ChunkWidth;
        int globalZ = lz + chunkComp.ChunkZ * BlockData.ChunkWidth;

        // check valid height
        if (ly < 0 || ly >= BlockData.ChunkHeight) return;

        // ensure place spot is currently Air
        if (WorldGenerator.Instance.IsBlockSolidAtGlobal(globalX, ly, globalZ)) return;

        // --- Prevent placing if player is occupying that space ---
        // compute world center of the target block using chunk transform
        Vector3 blockWorldCenter = chunkT.TransformPoint(new Vector3(lx + 0.5f, ly + 0.5f, lz + 0.5f));
        // slightly smaller than full block to avoid edge cases
        Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.45f);

        Collider[] overlaps = Physics.OverlapBox(blockWorldCenter, halfExtents, Quaternion.identity);
        foreach (var col in overlaps)
        {
            if (col == null) continue;
            // If we hit the player's collider (tagged "Player") or a CharacterController, block placement is not allowed
            if (col.CompareTag("Player") || col.GetComponentInParent<CharacterController>() != null)
            {
                Debug.Log("Cannot place block: player is occupying that space.");
                return;
            }
        }

        // Check inventory for selected item
        if (playerInventory == null) return;

        ItemStack selectedStack = playerInventory.GetSelectedItemStack();
        if (selectedStack.IsEmpty() || selectedStack.item.itemType != ItemType.Block)
        {
            // Debug.Log("No block selected in hotbar!");
            return;
        }

        BlockType blockToPlace = selectedStack.item.blockType;

        // Place the block
        WorldGenerator.Instance.SetBlockAtGlobal(globalX, ly, globalZ, blockToPlace);

        // Remove one item from inventory
        playerInventory.RemoveItem(selectedStack.item.itemName, 1);
    }

    void SpawnDroppedItem(Item item, Vector3 position)
    {
        if (droppedItemPrefab != null)
        {
            GameObject dropObj = Instantiate(droppedItemPrefab, position, Quaternion.identity);
            DroppedItem dropScript = dropObj.GetComponent<DroppedItem>();
            if (dropScript != null)
            {
                dropScript.Initialize(item, 1);
            }

            Rigidbody rb = dropObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Ensure it stays in place initially (no random force)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            // Fallback if prefab is not assigned
            DroppedItem.SpawnDroppedItem(position, new ItemStack(item, 1), Vector3.zero);
        }
    }
}
