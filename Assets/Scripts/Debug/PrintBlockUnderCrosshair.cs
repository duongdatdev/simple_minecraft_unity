using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Debug helper: press P to raycast and print info about the block under the crosshair.
/// Robust: uses reflection to query chunk for block data (works even if public API name differs).
/// Attach to Main Camera or player camera.
/// </summary>
public class PrintBlockUnderCrosshair : MonoBehaviour
{
    // maximum ray distance
    public float reach = 8f;
    public LayerMask hitLayers = ~0;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>() ?? Camera.main;
        if (cam == null) Debug.LogWarning("PrintBlockUnderCrosshair: No camera found.");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (cam == null) { Debug.Log("PrintBlockUnderCrosshair: no camera."); return; }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, reach, hitLayers))
            {
                // find Chunk component on hit object or its parents
                Transform t = hit.collider.transform;
                var chunk = t.GetComponent<MonoBehaviour>() as MonoBehaviour;
                // try to find a Chunk type on the hit object or parents
                Type chunkType = null;
                Component chunkComp = null;
                var comp = t.GetComponent<Component>();
                // search for any component named "Chunk" in the hit object or parents
                Transform cursor = t;
                while (cursor != null)
                {
                    foreach (var c in cursor.GetComponents<Component>())
                    {
                        if (c == null) continue;
                        if (c.GetType().Name == "Chunk")
                        {
                            chunkComp = c;
                            chunkType = c.GetType();
                            break;
                        }
                    }
                    if (chunkComp != null) break;
                    cursor = cursor.parent;
                }

                if (chunkComp == null)
                {
                    Debug.Log($"[DEBUG PICK] Hit object '{hit.collider.gameObject.name}' but no Chunk component found. Hit point: {hit.point}");
                    return;
                }

                // compute local chunk-space hit and a point slightly inside the block
                var chunkTransform = chunkComp.transform;
                Vector3 localHit = chunkTransform.InverseTransformPoint(hit.point);
                Vector3 localNormal = chunkTransform.InverseTransformDirection(hit.normal);
                Vector3 localInside = localHit - localNormal * 0.01f;

                // floor to int to get local indices
                int lx = Mathf.FloorToInt(localInside.x);
                int ly = Mathf.FloorToInt(localInside.y);
                int lz = Mathf.FloorToInt(localInside.z);

                // try to read chunk.ChunkX and ChunkZ properties (common in code)
                int chunkX = TryGetIntPropertyOrField(chunkComp, "ChunkX", out bool hasChunkX) ? chunkX = TryGetIntPropertyOrFieldValue(chunkComp, "ChunkX") : 0;
                int chunkZ = TryGetIntPropertyOrField(chunkComp, "ChunkZ", out bool hasChunkZ) ? chunkZ = TryGetIntPropertyOrFieldValue(chunkComp, "ChunkZ") : 0;

                // get block type by trying several method/field names via reflection
                string blockTypeStr = TryGetBlockTypeString(chunkComp, lx, ly, lz);

                int gx = lx + chunkX * BlockData.ChunkWidth;
                int gz = lz + chunkZ * BlockData.ChunkWidth;

                Debug.Log($"[DEBUG PICK] chunkComp='{chunkComp.name}' chunkXY=({chunkX},{chunkZ}) local=({lx},{ly},{lz}) global=({gx},{ly},{gz}) blockType={blockTypeStr} hitNormal={hit.normal} hitPoint={hit.point}");
            }
            else
            {
                Debug.Log("[DEBUG PICK] Nothing hit under crosshair (raycast missed).");
            }
        }
    }

    // Try several common method/field names to obtain a block type at local coords and return a string representation.
    private string TryGetBlockTypeString(Component chunkComp, int lx, int ly, int lz)
    {
        Type t = chunkComp.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // candidate method names that might exist (try each)
        string[] methodNames = new string[] {
            "GetBlockLocalSafe", "GetBlockLocal", "GetBlock", "GetBlockAt", "GetBlockAtLocal"
        };

        foreach (var mname in methodNames)
        {
            var m = t.GetMethod(mname, flags);
            if (m != null)
            {
                try
                {
                    var val = m.Invoke(chunkComp, new object[] { lx, ly, lz });
                    if (val != null) return val.ToString();
                }
                catch { /* ignore invocation errors */ }
            }
        }

        // candidate field or property names (blocks array or similar)
        // try "blocks" field (BlockType[,,])
        var f = t.GetField("blocks", flags);
        if (f != null)
        {
            try
            {
                var blocksObj = f.GetValue(chunkComp);
                if (blocksObj is Array arr)
                {
                    int max0 = arr.GetLength(0);
                    int max1 = arr.GetLength(1);
                    int max2 = arr.GetLength(2);
                    if (lx >= 0 && lx < max0 && ly >= 0 && ly < max1 && lz >= 0 && lz < max2)
                    {
                        var b = arr.GetValue(lx, ly, lz);
                        if (b != null) return b.ToString();
                    }
                    else
                    {
                        return "OutOfRange";
                    }
                }
            }
            catch { /* ignore */ }
        }

        // try properties named "Blocks" etc.
        var prop = t.GetProperty("Blocks", flags) ?? t.GetProperty("blocks", flags);
        if (prop != null)
        {
            try
            {
                var blocksObj = prop.GetValue(chunkComp, null);
                if (blocksObj is Array arr)
                {
                    int max0 = arr.GetLength(0);
                    int max1 = arr.GetLength(1);
                    int max2 = arr.GetLength(2);
                    if (lx >= 0 && lx < max0 && ly >= 0 && ly < max1 && lz >= 0 && lz < max2)
                    {
                        var b = arr.GetValue(lx, ly, lz);
                        if (b != null) return b.ToString();
                    }
                }
            }
            catch { }
        }

        // fallback: try calling a global accessor on WorldGenerator if exists: IsBlockSolidAtGlobal or GetBlock... (less likely)
        // compute global coords if chunk has ChunkX/ChunkZ
        int chunkX = TryGetIntPropertyOrFieldValue(chunkComp, "ChunkX");
        int chunkZ = TryGetIntPropertyOrFieldValue(chunkComp, "ChunkZ");
        int gx = lx + chunkX * BlockData.ChunkWidth;
        int gz = lz + chunkZ * BlockData.ChunkWidth;
        try
        {
            var wg = WorldGenerator.Instance;
            if (wg != null)
            {
                // try to call IsBlockSolidAtGlobal or similar (just show boolean)
                MethodInfo isSolid = wg.GetType().GetMethod("IsBlockSolidAtGlobal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isSolid != null)
                {
                    var solid = isSolid.Invoke(wg, new object[] { gx, ly, gz });
                    return $"IsSolid={solid}";
                }
            }
        }
        catch { }

        return "Unknown";
    }

    // helpers to safely get integer property/field; returns true if property/field exists
    private bool TryGetIntPropertyOrField(Component comp, string name, out bool exists)
    {
        exists = false;
        var t = comp.GetType();
        var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            exists = true;
            return true;
        }
        var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(int))
        {
            exists = true;
            return true;
        }
        return false;
    }

    // get int value or 0 if not found
    private int TryGetIntPropertyOrFieldValue(Component comp, string name)
    {
        var t = comp.GetType();
        var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.PropertyType == typeof(int))
        {
            try { return (int)prop.GetValue(comp); } catch { return 0; }
        }
        var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(int))
        {
            try { return (int)field.GetValue(comp); } catch { return 0; }
        }
        return 0;
    }
}
