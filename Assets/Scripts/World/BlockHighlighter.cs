// Assets/Scripts/World/BlockHighlighterLines.cs

using UnityEngine;

/// <summary>
/// BlockHighlighterLines
/// - Highlight all 6 faces of targeted voxel block by drawing thin quads ON the face plane.
/// - This debug variant keeps original logic but adds heavy instrumentation:
///   - Frame/event logs for culling/occlusion/near-plane/inside checks
///   - Debug.DrawLine/DrawRay visual cues
///   - Public toggles to enable/disable specific checks for narrowing down cause
/// 
/// Use inspector flags: debugMode, drawDebugGizmos, logOcclusionHits.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BlockHighlighterLines : MonoBehaviour
{
    [Header("Raycast")] public float maxDistance = 8f;
    public LayerMask blockLayer = ~0;

    [Header("Style")] public Color outlineColor = Color.black;

    [Tooltip("Quad thickness in world units (if very small, line appears thin).")] [Range(0.0005f, 0.05f)]
    public float lineWidth = 0.003f;

    [Header("Render")]
    [Tooltip("If true, attempt to use Unlit/OutlineAlways shader (ZTest Always) to render outlines on top.")]
    public bool renderOnTop = true;

    [Header("Behavior")] public bool hideWhenCameraInside = true;
    public int showDelayFrames = 4;

    [Header("Culling / Occlusion")]
    [Tooltip("If true, do not draw faces whose normal faces away from the camera (backface culling).")]
    public bool cullBackfaces = true;

    [Tooltip(
        "If true, perform a Physics.Raycast to the face center to avoid drawing faces that are occluded by other geometry.")]
    public bool testOcclusion = false;

    [Tooltip(
        "If true, perform occlusion test per-edge (raycast to edge midpoint) to avoid drawing edges hidden by adjacent blocks.")]
    public bool perEdgeOcclusion = true;

    [Header("Debugging (turn on to collect data)")]
    public bool debugMode = false;

    public bool drawDebugGizmos = false;
    public bool logOcclusionHits = false;
    public bool temporarilyDisableRenderOnTop = false; // quick test to disable "always on top" shader
    public bool forceDisablePerEdgeOcclusion = false;

    // runtime
    Camera cam;
    Material mat;
    Vector3Int currentLocalBlock = Vector3Int.zero;
    Transform targetChunkTransform = null;
    bool hasTarget = false;
    int showDelayCounter = 0;
    bool hiddenByInside = false;

    // debug counters
    int frameCounter = 0;
    int lastLoggedFrameChange = -1;
    Vector3 lastFaceCenter = Vector3.zero;
    float nearPlaneEps = 0.01f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        CreateMaterial();
    }

    void CreateMaterial()
    {
        Shader s = null;
        if (renderOnTop && !temporarilyDisableRenderOnTop)
        {
            s = Shader.Find("Unlit/OutlineAlways");
            if (s == null)
            {
                Debug.LogWarning(
                    "Shader 'Unlit/OutlineAlways' not found — outlines may be occluded. Place shader at Assets/Shaders/UnlitOutlineAlways.shader");
            }
        }

        if (s == null)
        {
            s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
        }

        mat = new Material(s);
        mat.hideFlags = HideFlags.DontSave;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", outlineColor);

        // If we didn't get Always shader, push renderQueue high to try draw later
        if (s != null && s.name != "Unlit/OutlineAlways")
        {
            mat.renderQueue = 4000;
        }
    }

    void Update()
    {
        frameCounter++;
        UpdateTarget();
    }

    void UpdateTarget()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // Bắn ray
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, blockLayer.value))
        {
            // Check if we hit a chunk
            Chunk chunk = hit.collider.GetComponent<Chunk>();

            // If not a chunk (e.g. Player, Item, etc.), try to find one behind it
            if (chunk == null)
            {
                RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, blockLayer.value);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                bool found = false;
                foreach (var h in hits)
                {
                    Chunk c = h.collider.GetComponent<Chunk>();
                    if (c != null)
                    {
                        hit = h;
                        chunk = c; // Found a chunk
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // No chunk found
                    hasTarget = false;
                    targetChunkTransform = null;
                    return;
                }
            }

            // Lấy chunk và block hit thực
            Transform chunkT = hit.collider.transform;
            Vector3 localHit = chunkT.InverseTransformPoint(hit.point);
            Vector3 localNormal = chunkT.InverseTransformDirection(hit.normal);
            Vector3 inside = localHit - localNormal * 0.01f;

            Vector3Int newBlock = new Vector3Int(
                Mathf.FloorToInt(inside.x),
                Mathf.FloorToInt(inside.y),
                Mathf.FloorToInt(inside.z)
            );

            // Chỉ cập nhật khi block khác hoặc chunk khác
            if (!hasTarget || newBlock != currentLocalBlock || targetChunkTransform != chunkT)
            {
                if (debugMode)
                    Debug.Log(
                        $"[Outline] TargetBlock changed: old={currentLocalBlock} new={newBlock} chunk={chunkT.name} frame={frameCounter}");

                currentLocalBlock = newBlock;
                targetChunkTransform = chunkT;
                hasTarget = true;
            }
        }
        else
        {
            if (hasTarget && debugMode)
                Debug.Log($"[Outline] Lost target at frame {frameCounter}");

            hasTarget = false;
            targetChunkTransform = null;
        }
    }


    void OnRenderObject()
    {
        if (!hasTarget || targetChunkTransform == null) return;

        bool isInside = hideWhenCameraInside && IsCameraInsideBlockWorld(currentLocalBlock, targetChunkTransform);

        if (isInside)
        {
            if (debugMode) Debug.Log($"[Outline] Camera is inside block {currentLocalBlock} at frame {frameCounter}");
            hiddenByInside = true;
            showDelayCounter = showDelayFrames;
            return;
        }

        if (hiddenByInside)
        {
            if (showDelayCounter-- > 0) return;
            hiddenByInside = false;
        }

        if (mat == null) CreateMaterial();
        if (mat == null) return;

        mat.SetPass(0);
        GL.PushMatrix();
        // Ensure model matrix identity to avoid surprises (we feed world coords)
        GL.MultMatrix(Matrix4x4.identity);

        GL.Begin(GL.QUADS);
        GL.Color(outlineColor);

        DrawAllFacesOnSurface(currentLocalBlock, targetChunkTransform);

        GL.End();
        GL.PopMatrix();
    }

    // Draw 4 edge quads for each face; quads lie ON the face plane (no normal offset)
    void DrawAllFacesOnSurface(Vector3Int b, Transform chunk)
    {
        Vector3[] face = new Vector3[4];

        // +X
        face[0] = new Vector3(b.x + 1f, b.y + 0f, b.z + 0f);
        face[1] = new Vector3(b.x + 1f, b.y + 0f, b.z + 1f);
        face[2] = new Vector3(b.x + 1f, b.y + 1f, b.z + 1f);
        face[3] = new Vector3(b.x + 1f, b.y + 1f, b.z + 0f);
        DrawFaceEdgeQuads(face, chunk, b);

        // -X
        face[0] = new Vector3(b.x + 0f, b.y + 0f, b.z + 1f);
        face[1] = new Vector3(b.x + 0f, b.y + 0f, b.z + 0f);
        face[2] = new Vector3(b.x + 0f, b.y + 1f, b.z + 0f);
        face[3] = new Vector3(b.x + 0f, b.y + 1f, b.z + 1f);
        DrawFaceEdgeQuads(face, chunk, b);

        // +Y
        face[0] = new Vector3(b.x + 0f, b.y + 1f, b.z + 0f);
        face[1] = new Vector3(b.x + 1f, b.y + 1f, b.z + 0f);
        face[2] = new Vector3(b.x + 1f, b.y + 1f, b.z + 1f);
        face[3] = new Vector3(b.x + 0f, b.y + 1f, b.z + 1f);
        DrawFaceEdgeQuads(face, chunk, b);

        // -Y
        face[0] = new Vector3(b.x + 0f, b.y + 0f, b.z + 1f);
        face[1] = new Vector3(b.x + 1f, b.y + 0f, b.z + 1f);
        face[2] = new Vector3(b.x + 1f, b.y + 0f, b.z + 0f);
        face[3] = new Vector3(b.x + 0f, b.y + 0f, b.z + 0f);
        DrawFaceEdgeQuads(face, chunk, b);

        // +Z
        face[0] = new Vector3(b.x + 0f, b.y + 0f, b.z + 1f);
        face[1] = new Vector3(b.x + 1f, b.y + 0f, b.z + 1f);
        face[2] = new Vector3(b.x + 1f, b.y + 1f, b.z + 1f);
        face[3] = new Vector3(b.x + 0f, b.y + 1f, b.z + 1f);
        DrawFaceEdgeQuads(face, chunk, b);

        // -Z
        face[0] = new Vector3(b.x + 1f, b.y + 0f, b.z + 0f);
        face[1] = new Vector3(b.x + 0f, b.y + 0f, b.z + 0f);
        face[2] = new Vector3(b.x + 0f, b.y + 1f, b.z + 0f);
        face[3] = new Vector3(b.x + 1f, b.y + 1f, b.z + 0f);
        DrawFaceEdgeQuads(face, chunk, b);
    }

    // b is local block position (used to compute block center)
    void DrawFaceEdgeQuads(Vector3[] faceLocalCorners, Transform chunk, Vector3Int b)
    {
        // compute local normal from the supplied local winding and transform to world
        Vector3 localNormal = Vector3
            .Cross(faceLocalCorners[1] - faceLocalCorners[0], faceLocalCorners[3] - faceLocalCorners[0]).normalized;
        Vector3 faceNormal = chunk.TransformDirection(localNormal).normalized;

        // transform corners to world
        Vector3[] worldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++) worldCorners[i] = chunk.TransformPoint(faceLocalCorners[i]);

        Vector3 faceCenterWorld = (worldCorners[0] + worldCorners[2]) * 0.5f;
        Vector3 blockCenterWorld = chunk.TransformPoint(new Vector3(b.x + 0.5f, b.y + 0.5f, b.z + 0.5f));

        // Ensure normal points outward from block center. If it points inward, flip it.
        if (Vector3.Dot(faceNormal, faceCenterWorld - blockCenterWorld) < 0f)
        {
            faceNormal = -faceNormal;
        }

        // Debug: check near-plane / clipping
        float camToFace = Vector3.Distance(cam.transform.position, faceCenterWorld);
        float near = cam.nearClipPlane;
        if (debugMode && camToFace <= near + nearPlaneEps)
        {
            Debug.LogWarning(
                $"[Outline][NearPlane] faceCenter too close: dist={camToFace:F5} near={near:F5} block={currentLocalBlock} frame={frameCounter}");
            if (drawDebugGizmos)
            {
                Debug.DrawLine(cam.transform.position, faceCenterWorld, Color.yellow, 0.1f);
            }
        }

        // Backface culling
        if (cullBackfaces)
        {
            // Vector from face to camera
            Vector3 toCam = cam.transform.position - faceCenterWorld;

            // If camera is within ~1° behind the plane, still show (avoid flicker on edges)
            const float angleThreshold = -0.05f;

            if (Vector3.Dot(faceNormal, toCam) < angleThreshold)
            {
                // camera is behind face plane → hide this face
                if (debugMode)
                    Debug.Log(
                        $"[Outline][Cull] Backface culled for face at {faceCenterWorld} block={currentLocalBlock} frame={frameCounter}");
                return;
            }
        }

        // Quick per-face occlusion (optional)
        if (testOcclusion)
        {
            Vector3 dirF = faceCenterWorld - cam.transform.position;
            float distF = dirF.magnitude;
            if (distF > 1e-6f)
            {
                dirF /= distF;
                if (Physics.Raycast(cam.transform.position, dirF, out RaycastHit hF, distF - 0.001f, blockLayer.value))
                {
                    // occluded at face center -> skip whole face
                    if (debugMode)
                        Debug.Log(
                            $"[Outline][OccludedFace] face center occluded by {hF.collider?.name} at dist {hF.distance:F4} (faceDist {distF:F4}) frame={frameCounter}");
                    if (drawDebugGizmos)
                        Debug.DrawLine(cam.transform.position, hF.point, Color.red, 0.1f);
                    return;
                }
            }
        }

        // Per-edge processing
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = worldCorners[i];
            Vector3 bCorner = worldCorners[(i + 1) % 4];
            Vector3 edgeDir = (bCorner - a);
            float elen = edgeDir.magnitude;
            if (elen <= 1e-6f) continue;
            edgeDir /= elen;

            // in-plane perpendicular (edgeNormal) = cross(edgeDir, faceNormal)
            Vector3 edgeNormal = Vector3.Cross(edgeDir, faceNormal).normalized;

            // ensure edgeNormal points outward relative to block center
            Vector3 mid = (a + bCorner) * 0.5f;
            if (Vector3.Dot(edgeNormal, mid - blockCenterWorld) < 0f) edgeNormal = -edgeNormal;

            // default: draw this edge
            bool drawEdge = true;

            // --- per-edge occlusion check (RaycastAll + tolerance) ---
            if (perEdgeOcclusion && !forceDisablePerEdgeOcclusion)
            {
                const float outwardOffset = 0.004f; // tweakable
                const float hitTolerance = 0.002f; // ignore hits extremely close to samplePoint
                Vector3 samplePoint = mid + edgeNormal * outwardOffset;
                Vector3 dir = samplePoint - cam.transform.position;
                float dist = dir.magnitude;

                if (dist > 1e-6f)
                {
                    dir /= dist;
                    RaycastHit[] hits =
                        Physics.RaycastAll(cam.transform.position, dir, dist - 0.0001f, blockLayer.value);
                    if (hits != null && hits.Length > 0)
                    {
                        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
                        bool occluded = false;

                        if (logOcclusionHits && debugMode)
                        {
                            Debug.Log($"[Outline][RayAll] hits {hits.Length} for edge mid={mid} frame={frameCounter}");
                        }

                        foreach (var h in hits)
                        {
                            // ignore hits extremely close to samplePoint (likely z-fighting/self-hit)
                            if (Vector3.Distance(h.point, samplePoint) < hitTolerance)
                                continue;

                            // If hit is from different collider -> it occludes (closest other object)
                            if (h.collider == null)
                            {
                                occluded = true;
                                break;
                            }

                            // If hit is from another object (different chunk) -> occlude immediately
                            if (h.collider.transform != chunk)
                            {
                                occluded = true;
                                if (debugMode && logOcclusionHits)
                                    Debug.Log(
                                        $"[Outline][EdgeOcclude] occluded by OTHER collider {h.collider.name} dist={h.distance:F5} edgeMid={mid} frame={frameCounter}");
                                break;
                            }

                            // h.collider is same chunk: ensure hit is noticeably *before* samplePoint (not a precision self-hit)
                            // and its face actually faces the camera.
                            // Only treat as occluding if it is sufficiently closer than samplePoint.
                            const float
                                minOccluderGap =
                                    0.005f; // hit must be at least this much closer than samplePoint to be an occluder
                            if (h.distance < dist - minOccluderGap)
                            {
                                float faceDot = Vector3.Dot(h.normal.normalized, -dir);
                                const float faceThreshold = 0.01f; // tiny threshold to tolerate precision
                                if (faceDot > faceThreshold)
                                {
                                    occluded = true;
                                    if (debugMode && logOcclusionHits)
                                        Debug.Log(
                                            $"[Outline][EdgeOcclude] occluded by same-chunk face (faceDot={faceDot:F4}) distance={h.distance:F5} edgeMid={mid} frame={frameCounter}");
                                    break;
                                }
                                else
                                {
                                    // same-chunk hit but face not facing camera — ignore
                                    continue;
                                }
                            }
                            else
                            {
                                // hit is from same chunk but essentially at/after samplePoint -> ignore as self-hit/precision
                                continue;
                            }
                        }


                        if (occluded)
                        {
                            drawEdge = false;
                            if (drawDebugGizmos)
                            {
                                Debug.DrawLine(cam.transform.position, samplePoint, Color.magenta, 0.05f);
                                Debug.DrawLine(samplePoint, mid, Color.magenta, 0.05f);
                            }
                        }
                    }
                }
            }

            if (!drawEdge) continue;

            float half = Mathf.Max(0.00001f, lineWidth * 0.5f);

            // QUAD vertices (on face plane)
            Vector3 v0 = a - edgeNormal * half;
            Vector3 v1 = a + edgeNormal * half;
            Vector3 v2 = bCorner + edgeNormal * half;
            Vector3 v3 = bCorner - edgeNormal * half;

            // Debug drawing: show edge mid and normals
            if (drawDebugGizmos)
            {
                Vector3 midPt = (v0 + v2) * 0.5f;
                Debug.DrawLine(a, bCorner, Color.cyan, 0.05f);
                Debug.DrawLine(midPt, midPt + faceNormal * 0.05f, Color.green, 0.05f);
                Debug.DrawLine(midPt, midPt + edgeNormal * 0.05f, Color.blue, 0.05f);
            }

            GL.Vertex(v0);
            GL.Vertex(v1);
            GL.Vertex(v2);
            GL.Vertex(v3);

            // record for analysis
            if (debugMode && (frameCounter % 15 == 0))
            {
                lastFaceCenter = faceCenterWorld;
            }
        }
    }


    bool IsCameraInsideBlockWorld(Vector3Int localBlockPos, Transform chunk)
    {
        Vector3 worldMin = chunk.TransformPoint(new Vector3(localBlockPos.x, localBlockPos.y, localBlockPos.z));
        Vector3 worldMax =
            chunk.TransformPoint(new Vector3(localBlockPos.x + 1f, localBlockPos.y + 1f, localBlockPos.z + 1f));
        Vector3 p = cam.transform.position;
        const float eps = 0.001f;
        bool insideX = p.x > Mathf.Min(worldMin.x, worldMax.x) + eps && p.x < Mathf.Max(worldMin.x, worldMax.x) - eps;
        bool insideY = p.y > Mathf.Min(worldMin.y, worldMax.y) + eps && p.y < Mathf.Max(worldMin.y, worldMax.y) - eps;
        bool insideZ = p.z > Mathf.Min(worldMin.z, worldMax.z) + eps && p.z < Mathf.Max(worldMin.z, worldMax.z) - eps;
        return insideX && insideY && insideZ;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    // Optional: draw summary gizmo at face center to inspect in Scene view
    void OnDrawGizmos()
    {
        if (!debugMode || !drawDebugGizmos) return;
        if (!hasTarget || targetChunkTransform == null) return;

        Vector3 blockCenterWorld = targetChunkTransform.TransformPoint(new Vector3(currentLocalBlock.x + 0.5f,
            currentLocalBlock.y + 0.5f, currentLocalBlock.z + 0.5f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(blockCenterWorld, Vector3.one * 1.02f);

        // draw a line from cam to lastFaceCenter
        if (lastFaceCenter != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(cam.transform.position, lastFaceCenter);
            Gizmos.DrawSphere(lastFaceCenter, 0.02f);
        }
    }
}