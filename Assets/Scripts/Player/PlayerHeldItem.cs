using UnityEngine;
using System.Collections.Generic;

public class PlayerHeldItem : MonoBehaviour
{
    public PlayerArm playerArm;
    public Inventory inventory;
    
    [Header("Settings")]
    public Material fallbackBlockMaterial; // Fallback material if auto-detection fails
    public Vector3 blockPosition = new Vector3(0, 0.1f, 0.5f);
    public Vector3 blockRotation = new Vector3(0, 45, 0);
    public float blockScale = 0.4f;

    public Vector3 itemPosition = new Vector3(0, 0.1f, 0.5f);
    public Vector3 itemRotation = new Vector3(0, 0, 0);
    public float itemScale = 0.4f;

    private GameObject blockObj;
    private MeshFilter blockMeshFilter;
    private MeshRenderer blockMeshRenderer;

    private GameObject itemObj;
    private SpriteRenderer itemSpriteRenderer;

    private BlockTextureData[] blockTextures;
    private Material blockMaterial;

    void Start()
    {
        if (playerArm == null) playerArm = GetComponent<PlayerArm>();
        if (inventory == null) inventory = GetComponentInParent<Inventory>();

        InitializeBlockData();
        CreateVisuals();

        if (inventory != null)
        {
            inventory.OnHotbarSelectionChanged += (slot) => UpdateHeldItem();
            inventory.OnSlotChanged += (slot, item) => {
                if (slot == inventory.selectedHotbarSlot) UpdateHeldItem();
            };
            UpdateHeldItem();
        }
    }

    void InitializeBlockData()
    {
        Material sourceMat = null;

        if (WorldGenerator.Instance != null)
        {
            // 1. Try to get from prefab
            if (WorldGenerator.Instance.chunkPrefab != null)
            {
                var chunk = WorldGenerator.Instance.chunkPrefab.GetComponent<Chunk>();
                if (chunk != null)
                {
                    blockTextures = chunk.blockTextures;
                }
                var chunkRenderer = WorldGenerator.Instance.chunkPrefab.GetComponent<MeshRenderer>();
                if (chunkRenderer != null)
                {
                    sourceMat = chunkRenderer.sharedMaterial;
                }
            }

            // 2. Fallback: Try to find a chunk in the scene (if prefab material was missing)
            if (sourceMat == null)
            {
                var activeChunk = FindObjectOfType<Chunk>();
                if (activeChunk != null)
                {
                    var chunkRenderer = activeChunk.GetComponent<MeshRenderer>();
                    if (chunkRenderer != null)
                    {
                        sourceMat = chunkRenderer.sharedMaterial;
                        // Also try to get textures if missing
                        if (blockTextures == null || blockTextures.Length == 0) blockTextures = activeChunk.blockTextures;
                    }
                }
            }
        }

        // 3. Manual Fallback
        if (sourceMat == null)
        {
            sourceMat = fallbackBlockMaterial;
        }

        // 4. Load from Resources if still missing
        if (blockTextures == null || blockTextures.Length == 0)
        {
            blockTextures = Resources.LoadAll<BlockTextureData>("BlockTextures");
        }

        // Create the overlay material
        if (sourceMat != null)
        {
            Shader overlayShader = Shader.Find("Custom/OverlayLit");
            if (overlayShader != null)
            {
                blockMaterial = new Material(overlayShader);
                if (sourceMat.HasProperty("_MainTex"))
                    blockMaterial.mainTexture = sourceMat.GetTexture("_MainTex");
                else if (sourceMat.HasProperty("_BaseMap")) // URP support
                    blockMaterial.mainTexture = sourceMat.GetTexture("_BaseMap");
                else
                    blockMaterial.mainTexture = sourceMat.mainTexture; // Try default
                
                // Handle Color safely
                if (sourceMat.HasProperty("_Color"))
                    blockMaterial.color = sourceMat.GetColor("_Color");
                else if (sourceMat.HasProperty("_BaseColor"))
                    blockMaterial.SetColor("_Color", sourceMat.GetColor("_BaseColor"));
                else
                    blockMaterial.color = Color.white;
            }
            else
            {
                blockMaterial = sourceMat;
            }
        }

        // Update the renderer if we found it
        if (blockMaterial != null && blockMeshRenderer != null)
        {
            blockMeshRenderer.sharedMaterial = blockMaterial;
        }
    }

    void CreateVisuals()
    {
        Transform rightArm = playerArm != null ? playerArm.GetRightArm() : transform;
        if (rightArm == null) return; 

        // Block Model
        blockObj = new GameObject("HeldBlock");
        blockObj.transform.SetParent(rightArm);
        blockObj.transform.localPosition = blockPosition;
        blockObj.transform.localRotation = Quaternion.Euler(blockRotation);
        blockObj.transform.localScale = Vector3.one * blockScale;
        
        blockMeshFilter = blockObj.AddComponent<MeshFilter>();
        blockMeshRenderer = blockObj.AddComponent<MeshRenderer>();
        if (blockMaterial != null) blockMeshRenderer.sharedMaterial = blockMaterial;
        blockObj.SetActive(false);

        // Item Sprite
        itemObj = new GameObject("HeldItemSprite");
        itemObj.transform.SetParent(rightArm);
        itemObj.transform.localPosition = itemPosition;
        itemObj.transform.localRotation = Quaternion.Euler(itemRotation);
        itemObj.transform.localScale = Vector3.one * itemScale;

        itemSpriteRenderer = itemObj.AddComponent<SpriteRenderer>();
        itemObj.SetActive(false);
    }

    public void UpdateHeldItem()
    {
        if (inventory == null) return;

        // Retry init if missing (e.g. chunks spawned after Start)
        if (blockMaterial == null || blockTextures == null || blockTextures.Length == 0)
        {
            InitializeBlockData();
        }
        
        ItemStack stack = inventory.GetSelectedItemStack();
        
        if (stack == null || stack.IsEmpty())
        {
            if (blockObj) blockObj.SetActive(false);
            if (itemObj) itemObj.SetActive(false);
            return;
        }

        Item item = stack.item;

        // Check if it's a block
        if (item.blockType != BlockType.Air)
        {
            // Show block
            if (itemObj) itemObj.SetActive(false);
            if (blockObj)
            {
                blockObj.SetActive(true);
                UpdateBlockMesh(item.blockType);
            }
        }
        else
        {
            // Show item sprite
            if (blockObj) blockObj.SetActive(false);
            if (itemObj)
            {
                itemObj.SetActive(true);
                itemSpriteRenderer.sprite = item.icon;
            }
        }
    }

    void UpdateBlockMesh(BlockType type)
    {
        if (blockTextures == null) return;

        BlockTextureData data = null;
        foreach (var d in blockTextures)
        {
            if (d.blockType == type)
            {
                data = d;
                break;
            }
        }

        if (data == null) return;

        // Generate simple cube mesh
        Mesh mesh = new Mesh();
        
        List<Vector3> meshVerts = new List<Vector3>();
        List<int> meshTris = new List<int>();
        List<Vector2> meshUVs = new List<Vector2>();

        int vertCount = 0;

        for (int i = 0; i < 6; i++) // 6 faces
        {
            // Get UVs for this face
            // Face indices in BlockData: 0=Back, 1=Front, 2=Top, 3=Bottom, 4=Left, 5=Right
            
            Vector2Int tile = Vector2Int.zero;
            switch (i)
            {
                case 0: tile = data.back; break;
                case 1: tile = data.front; break;
                case 2: tile = data.up; break;
                case 3: tile = data.down; break;
                case 4: tile = data.left; break;
                case 5: tile = data.right; break;
            }

            Vector2[] faceUVs = TextureAtlas.GetUVsFromTile(tile.x, tile.y);

            // Add vertices
            int[] faceVertIndices = BlockData.FaceVertices[i];
            for (int v = 0; v < 4; v++)
            {
                // Center the block around 0,0,0
                Vector3 vertPos = BlockData.Verts[faceVertIndices[v]] - new Vector3(0.5f, 0.5f, 0.5f);
                meshVerts.Add(vertPos);
                meshUVs.Add(faceUVs[v]);
            }

            // Add triangles (0, 1, 2, 0, 2, 3)
            meshTris.Add(vertCount + 0);
            meshTris.Add(vertCount + 1);
            meshTris.Add(vertCount + 2);
            meshTris.Add(vertCount + 0);
            meshTris.Add(vertCount + 2);
            meshTris.Add(vertCount + 3);

            vertCount += 4;
        }

        mesh.SetVertices(meshVerts);
        mesh.SetTriangles(meshTris, 0);
        mesh.SetUVs(0, meshUVs);
        mesh.RecalculateNormals();

        blockMeshFilter.mesh = mesh;
    }
}
