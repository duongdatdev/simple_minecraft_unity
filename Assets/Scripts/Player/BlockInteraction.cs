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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip breakSound;
    public AudioClip placeSound;

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

    [Header("Eating/Use")]
    public float holdTimeToEat = 0.3f; // Time to hold right click before eating
    private float rightClickHoldTime = 0f;
    private bool rightClickHandled = false;

    private PlayerController playerController;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerInventory == null) playerInventory = Object.FindAnyObjectByType<Inventory>();
        if (inventoryUI == null) inventoryUI = Object.FindAnyObjectByType<InventoryUI>();
        
        // Cache PlayerController reference
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
            playerController = Object.FindAnyObjectByType<PlayerController>();
        
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

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
            // Create a clone to modify settings safely
            Material overlayMat = new Material(breakOverlayMaterial);

            // Auto-fix: Attempt to enable Alpha Clipping / Cutout mode if the user forgot
            // Check for URP
            if (overlayMat.shader.name.Contains("Universal Render Pipeline") || overlayMat.shader.name.Contains("URP"))
            {
                if (overlayMat.HasProperty("_AlphaClip"))
                {
                    overlayMat.SetFloat("_AlphaClip", 1); // Enable Alpha Clipping
                    overlayMat.SetFloat("_Cutoff", 0.1f); // Threshold
                }
            }
            // Check for Standard Shader
            else if (overlayMat.shader.name == "Standard")
            {
                overlayMat.SetFloat("_Mode", 1); // 1 = Cutout
                overlayMat.EnableKeyword("_ALPHATEST_ON");
                overlayMat.DisableKeyword("_ALPHABLEND_ON");
                overlayMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                overlayMat.renderQueue = 2450;
            }
            // Check for Legacy Transparent
            else if (!overlayMat.shader.name.Contains("Transparent") && !overlayMat.shader.name.Contains("Cutout"))
            {
                // Try to switch to a cutout shader if it seems to be opaque
                Shader cutout = Shader.Find("Unlit/Transparent Cutout");
                if (cutout != null) overlayMat.shader = cutout;
            }

            breakOverlayRenderer.material = overlayMat;
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
        // Don't interact if paused
        if (PauseMenu.IsPaused)
        {
            ResetBreaking();
            return;
        }

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

        // Handle right click: hold to eat, quick click to use/plant
        if (Input.GetKey(placeKey))
        {
            rightClickHoldTime += Time.deltaTime;
            
            // If held long enough and not yet handled, try to eat
            if (rightClickHoldTime >= holdTimeToEat && !rightClickHandled)
            {
                rightClickHandled = true;
                TryEatFood();
            }
        }
        else if (Input.GetKeyUp(placeKey))
        {
            // Released: if it was a quick click (not held for eating), handle as use/plant
            if (rightClickHoldTime < holdTimeToEat)
            {
                HandleRightClick();
            }
            
            // Reset
            rightClickHoldTime = 0f;
            rightClickHandled = false;
        }
    }

    void UpdateBreaking()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach, chunkLayer))
        {
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            
            // Check for CropBlock
            CropBlock cropBlock = hit.collider.GetComponent<CropBlock>();
            if (cropBlock != null)
            {
                chunkComp = cropBlock.chunk;
                chunkT = chunkComp.transform;
                // Use the crop's local position directly
                Vector3Int pos = cropBlock.localPosition;
                
                // Override hit point logic for crops
                int globalX = pos.x + chunkComp.ChunkX * BlockData.ChunkWidth;
                int globalZ = pos.z + chunkComp.ChunkZ * BlockData.ChunkWidth;
                
                HandleBreakingLogic(chunkComp, pos.x, pos.y, pos.z, globalX, globalZ, hit, chunkT);
                return;
            }

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
                    // Also check for CropBlock in raycast all
                    CropBlock cb = h.collider.GetComponent<CropBlock>();
                    if (cb != null)
                    {
                        chunkComp = cb.chunk;
                        chunkT = chunkComp.transform;
                        Vector3Int pos = cb.localPosition;
                        int globalX = pos.x + chunkComp.ChunkX * BlockData.ChunkWidth;
                        int globalZ = pos.z + chunkComp.ChunkZ * BlockData.ChunkWidth;
                        HandleBreakingLogic(chunkComp, pos.x, pos.y, pos.z, globalX, globalZ, h, chunkT);
                        return;
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
            
            int gX = lx + chunkComp.ChunkX * BlockData.ChunkWidth;
            int gZ = lz + chunkComp.ChunkZ * BlockData.ChunkWidth;
            
            HandleBreakingLogic(chunkComp, lx, ly, lz, gX, gZ, hit, chunkT);
        }
        else
        {
            ResetBreaking();
        }
    }

    void HandleBreakingLogic(Chunk chunkComp, int lx, int ly, int lz, int globalX, int globalZ, RaycastHit hit, Transform chunkT)
    {
            Vector3Int targetBlock = new Vector3Int(globalX, ly, globalZ);
            
            // Check what block we hit
            BlockType hitBlock = chunkComp.GetBlockLocal(lx, ly, lz);

            // Check if block is air (already broken)
            if (hitBlock == BlockType.Air)
            {
                ResetBreaking();
                return;
            }

            // Prevent breaking unbreakable blocks (bedrock)
            if (!BlockData.IsBreakable(hitBlock))
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
            
            // Increase progress using block hardness and tool speed
            float speedMultiplier = 1.0f;
            if (playerInventory != null)
            {
                ItemStack toolStack = playerInventory.GetSelectedItemStack();
                if (toolStack != null && !toolStack.IsEmpty() && toolStack.item.itemType == ItemType.Tool)
                {
                    speedMultiplier = GetToolSpeedMultiplier(hitBlock, toolStack.item);
                }
            }

            float hardness = BlockData.GetBlockHardness(hitBlock);
            if (float.IsPositiveInfinity(hardness))
            {
                // Unbreakable, just reset
                ResetBreaking();
                return;
            }

            float requiredTime = defaultBreakTime * Mathf.Max(0.01f, hardness);
            currentBreakProgress += (Time.deltaTime / requiredTime) * speedMultiplier;
            
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

    float GetToolSpeedMultiplier(BlockType block, Item tool)
    {
        if (tool == null || tool.itemType != ItemType.Tool) return 1.0f;

        float multiplier = 1.0f;
        bool isEffective = false;

        switch (block)
        {
            case BlockType.Stone:
            case BlockType.IronOre:
            case BlockType.DiamondBlock:
                if (tool.toolType == ToolType.Pickaxe) isEffective = true;
                break;
            case BlockType.Dirt:
            case BlockType.Grass:
            case BlockType.Sand:
                if (tool.toolType == ToolType.Shovel) isEffective = true;
                break;
            case BlockType.Wood:
            case BlockType.Planks:
            case BlockType.CraftingTable:
                if (tool.toolType == ToolType.Axe) isEffective = true;
                break;
            case BlockType.Leaves:
                // Swords break leaves faster
                if (tool.toolType == ToolType.Sword) isEffective = true;
                break;
        }

        if (isEffective)
        {
            switch (tool.toolTier)
            {
                case ToolTier.Wood: multiplier = 2.0f; break;
                case ToolTier.Stone: multiplier = 4.0f; break;
                case ToolTier.Iron: multiplier = 6.0f; break;
                case ToolTier.Diamond: multiplier = 8.0f; break;
                case ToolTier.Gold: multiplier = 12.0f; break;
            }
        }
        
        return multiplier;
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

        // Prevent breaking unbreakable blocks like bedrock
        if (!BlockData.IsBreakable(blockType))
        {
            Debug.Log("Cannot break that block (unbreakable)");
            return;
        }

        // Perform break: set to Air
        WorldGenerator.Instance.SetBlockAtGlobal(globalX, ly, globalZ, BlockType.Air);
        if (breakSound != null) audioSource.PlayOneShot(breakSound);

        // Drop item in world
        if (ItemDatabase.Instance != null)
        {
            Item itemToDrop = null;
            int dropCount = 1;

            // Special-case: breaking IronOre yields an Iron Ingot directly
            if (blockType == BlockType.IronOre)
            {
                itemToDrop = ItemDatabase.Instance.GetItem("iron_ingot");
            }
            else if (blockType >= BlockType.CarrotStage0 && blockType <= BlockType.CarrotStage3)
            {
                itemToDrop = ItemDatabase.Instance.GetItem("carrot");
                if (blockType == BlockType.CarrotStage3) dropCount = 2;
            }
            else
            {
                itemToDrop = ItemDatabase.Instance.GetItemForBlock(blockType);
            }

            if (itemToDrop != null)
            {
                // Spawn at exact block center
                Vector3 blockCenter = new Vector3(globalX + 0.5f, ly + 0.5f, globalZ + 0.5f);
                SpawnDroppedItem(itemToDrop, blockCenter, dropCount);
            }
        }

        // Damage tool if equipped
        if (playerInventory != null)
        {
            ItemStack selectedStack = playerInventory.GetSelectedItemStack();
            if (selectedStack != null && !selectedStack.IsEmpty() && selectedStack.item.itemType == ItemType.Tool)
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
        // 1. Check for block interaction (Crafting Table, etc.)
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach, chunkLayer))
        {
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            if (chunkComp != null)
            {
                Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
                Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);
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
            }
        }

        // 2. Check for Item Usage (Eating) and Tool Usage (Hoe/Planting)
        if (playerInventory != null)
        {
            ItemStack selectedStack = playerInventory.GetSelectedItemStack();
            if (selectedStack != null && !selectedStack.IsEmpty())
            {
                // Hoe / Carrot Planting Logic
                if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, reach, chunkLayer))
                {
                    Transform chunkT = hit.collider.transform;
                    Chunk chunkComp = chunkT.GetComponent<Chunk>();
                    
                    // Handle CropBlock hit for planting/hoeing (though usually you plant ON farmland, not ON crop)
                    CropBlock cropBlock = hit.collider.GetComponent<CropBlock>();
                    if (cropBlock != null)
                    {
                        chunkComp = cropBlock.chunk;
                        chunkT = chunkComp.transform;
                        // If we hit a crop, we might be trying to interact with the block BELOW it (farmland) or just fail
                        // For now, let's assume we want to interact with the crop block itself (e.g. bonemeal?)
                        // But for planting, we need to hit the farmland.
                        // If we hit a crop, we are looking at the crop.
                    }

                    if (chunkComp != null)
                    {
                        int bx, by, bz;
                        
                        if (cropBlock != null)
                        {
                            bx = cropBlock.localPosition.x;
                            by = cropBlock.localPosition.y;
                            bz = cropBlock.localPosition.z;
                        }
                        else
                        {
                            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
                            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);
                            Vector3 localInside = localHit - localNormal * 0.01f;
                            bx = Mathf.FloorToInt(localInside.x);
                            by = Mathf.FloorToInt(localInside.y);
                            bz = Mathf.FloorToInt(localInside.z);
                        }
                        
                        int globalX = bx + chunkComp.ChunkX * BlockData.ChunkWidth;
                        int globalZ = bz + chunkComp.ChunkZ * BlockData.ChunkWidth;

                        BlockType hitBlock = chunkComp.GetBlockLocal(bx, by, bz);

                        // Hoe Logic
                        if (selectedStack.item.toolType == ToolType.Hoe)
                        {
                            if (hitBlock == BlockType.Dirt || hitBlock == BlockType.Grass)
                            {
                                WorldGenerator.Instance.SetBlockAtGlobal(globalX, by, globalZ, BlockType.Farmland);
                                if (audioSource && placeSound) audioSource.PlayOneShot(placeSound);
                                return;
                            }
                        }
                        
                        // Carrot Planting Logic
                        if (selectedStack.item.itemName == "carrot")
                        {
                            if (hitBlock == BlockType.Farmland)
                            {
                                // Place carrot above
                                int aboveY = by + 1;
                                if (aboveY < BlockData.ChunkHeight && !WorldGenerator.Instance.IsBlockSolidAtGlobal(globalX, aboveY, globalZ))
                                {
                                    WorldGenerator.Instance.SetBlockAtGlobal(globalX, aboveY, globalZ, BlockType.CarrotStage0);
                                    playerInventory.ConsumeSelectedItem(1);
                                    if (audioSource && placeSound) audioSource.PlayOneShot(placeSound);
                                    return;
                                }
                            }
                        }
                    }
                }

                // Removed eating from quick click - now handled by hold timer in Update()
            }
        }

        // 3. Place Block (if raycast hit)
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, reach, chunkLayer))
        {
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            if (chunkComp == null) return;

            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);

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
        if (selectedStack == null || selectedStack.IsEmpty() || selectedStack.item.itemType != ItemType.Block)
        {
            // Debug.Log("No block selected in hotbar!");
            return;
        }

        BlockType blockToPlace = selectedStack.item.blockType;

        // Place the block
        WorldGenerator.Instance.SetBlockAtGlobal(globalX, ly, globalZ, blockToPlace);
        if (placeSound != null) audioSource.PlayOneShot(placeSound);

        // Remove one item from inventory
        playerInventory.RemoveItem(selectedStack.item.itemName, 1);
    }

    void SpawnDroppedItem(Item item, Vector3 position, int count = 1)
    {
        if (droppedItemPrefab != null)
        {
            GameObject dropObj = Instantiate(droppedItemPrefab, position, Quaternion.identity);
            DroppedItem dropScript = dropObj.GetComponent<DroppedItem>();
            if (dropScript != null)
            {
                dropScript.Initialize(item, count);
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
            DroppedItem.SpawnDroppedItem(position, new ItemStack(item, count), Vector3.zero);
        }
    }

    void TryEatFood()
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("playerInventory is null in TryEatFood");
            return;
        }

        ItemStack selectedStack = playerInventory.GetSelectedItemStack();
        if (selectedStack != null && !selectedStack.IsEmpty() && selectedStack.item != null && selectedStack.item.isConsumable)
        {
            // Cache item details before mutating inventory in case ConsumeSelectedItem clears the stack/item
            Item foodItem = selectedStack.item;
            int hunger = foodItem.hungerAmount;
            int heal = foodItem.healAmount;
            string name = foodItem.itemName;

            if (playerController != null)
            {
                playerController.Eat(hunger, heal);
                // Consume item after applying effects
                playerInventory.ConsumeSelectedItem(1);
                Debug.Log($"Ate {name}");
            }
            else
            {
                Debug.LogError("PlayerController not found! Cannot eat food.");
            }
        }
    }
}
