using UnityEngine;

/// <summary>
/// Player block interaction with inventory integration:
/// - Left click: break block (drop item to inventory)
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

    [Header("Inventory Integration")]
    public Inventory playerInventory;
    public InventoryUI inventoryUI;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerInventory == null) playerInventory = FindObjectOfType<Inventory>();
        if (inventoryUI == null) inventoryUI = FindObjectOfType<InventoryUI>();
    }

    void Update()
    {
        // Don't interact with blocks when inventory UI is open
        if (inventoryUI != null && inventoryUI.IsInventoryOpen())
            return;

        if (Input.GetKeyDown(breakKey)) TryBreak();
        if (Input.GetKeyDown(placeKey)) HandleRightClick();
    }

    void TryBreak()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach, chunkLayer))
        {
            // hit.collider.transform is chunk transform
            Transform chunkT = hit.collider.transform;
            Chunk chunkComp = chunkT.GetComponent<Chunk>();
            if (chunkComp == null) return;

            // get hit in chunk-local coords
            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);

            Vector3 localInside = localHit - localNormal * 0.01f;
            int lx = Mathf.FloorToInt(localInside.x);
            int ly = Mathf.FloorToInt(localInside.y);
            int lz = Mathf.FloorToInt(localInside.z);

            int globalX = lx + chunkComp.ChunkX * BlockData.ChunkWidth;
            int globalZ = lz + chunkComp.ChunkZ * BlockData.ChunkWidth;

            // Get block type before breaking
            BlockType blockType = chunkComp.GetBlockLocal(lx, ly, lz);
            
            // Don't break air
            if (blockType == BlockType.Air) return;

            // Perform break: set to Air
            WorldGenerator.Instance.SetBlockAtGlobal(globalX, ly, globalZ, BlockType.Air);

            // Add item to inventory
            if (playerInventory != null && ItemDatabase.Instance != null)
            {
                Item droppedItem = ItemDatabase.Instance.GetItemForBlock(blockType);
                if (droppedItem != null)
                {
                    bool added = playerInventory.AddItem(droppedItem, 1);
                    if (!added)
                    {
                        Debug.Log("Inventory full! Item not added.");
                        // TODO: Drop item entity in world
                    }
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
    }

    void HandleRightClick()
    {
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, reach, chunkLayer))
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
}
