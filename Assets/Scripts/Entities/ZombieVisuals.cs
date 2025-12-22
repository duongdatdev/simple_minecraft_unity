using UnityEngine;
using System.Collections.Generic;

namespace MinecraftGPT.Entities
{
    [ExecuteInEditMode]
    public class ZombieVisuals : MonoBehaviour
    {
        public Texture2D skin;
        private Material material;

        private void OnValidate()
        {
            if (skin != null)
            {
                // Rebuild if skin changes in editor
                Start();
            }
        }

        private void Start()
        {
            if (skin == null)
            {
                Debug.LogError("ZombieVisuals: No skin texture assigned!");
                return;
            }

            // Create a material for the skin
            // Using Standard shader, but Unlit/Texture might be better for pure Minecraft look
            material = new Material(Shader.Find("Standard"));
            material.mainTexture = skin;
            material.SetFloat("_Glossiness", 0f); // Not shiny

            BuildModel();
        }

        private void BuildModel()
        {
            // Find or create "Model" container to avoid destroying other children
            Transform modelTransform = transform.Find("Model");
            if (modelTransform != null)
            {
                if (Application.isPlaying) Destroy(modelTransform.gameObject);
                else DestroyImmediate(modelTransform.gameObject);
            }

            GameObject modelContainer = new GameObject("Model");
            modelContainer.transform.SetParent(transform, false);
            modelContainer.transform.localPosition = Vector3.zero;
            modelContainer.transform.localRotation = Quaternion.identity;

            // Standard Minecraft dimensions (approximate in Unity units)
            // 1 pixel = 0.0625 units (1/16)
            float pixelSize = 0.0625f;

            // Head (8x8x8)
            CreatePart(modelContainer.transform, "Head", new Vector3(0, 24 * pixelSize, 0), new Vector3(8, 8, 8) * pixelSize, 
                0, 0, 8, 8, 8);

            // Body (8x12x4)
            CreatePart(modelContainer.transform, "Body", new Vector3(0, 12 * pixelSize, 0), new Vector3(8, 12, 4) * pixelSize, 
                16, 16, 8, 12, 4);

            // Left Arm (4x12x4)
            CreatePart(modelContainer.transform, "LeftArm", new Vector3(-6 * pixelSize, 12 * pixelSize, 0), new Vector3(4, 12, 4) * pixelSize, 
                32, 48, 4, 12, 4);

            // Right Arm (4x12x4)
            CreatePart(modelContainer.transform, "RightArm", new Vector3(6 * pixelSize, 12 * pixelSize, 0), new Vector3(4, 12, 4) * pixelSize, 
                40, 16, 4, 12, 4);

            // Left Leg (4x12x4)
            CreatePart(modelContainer.transform, "LeftLeg", new Vector3(-2 * pixelSize, 0, 0), new Vector3(4, 12, 4) * pixelSize, 
                16, 48, 4, 12, 4);

            // Right Leg (4x12x4)
            CreatePart(modelContainer.transform, "RightLeg", new Vector3(2 * pixelSize, 0, 0), new Vector3(4, 12, 4) * pixelSize, 
                0, 16, 4, 12, 4);
        }

        private void CreatePart(Transform parent, string name, Vector3 position, Vector3 size, int u, int v, int width, int height, int depth)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position + new Vector3(0, size.y / 2, 0); // Pivot at bottom? Or center?
            // Let's pivot at center for now, but adjust position so 'position' is the bottom center of the part
            
            MeshFilter mf = part.AddComponent<MeshFilter>();
            MeshRenderer mr = part.AddComponent<MeshRenderer>();
            mr.material = material;

            mf.mesh = GenerateCubeMesh(size, u, v, width, height, depth);
        }

        private Mesh GenerateCubeMesh(Vector3 size, int u, int v, int width, int height, int depth)
        {
            Mesh mesh = new Mesh();

            float w = size.x / 2f;
            float h = size.y / 2f;
            float d = size.z / 2f;

            Vector3[] vertices = new Vector3[]
            {
                // Front
                new Vector3(-w, -h,  d), new Vector3( w, -h,  d), new Vector3( w,  h,  d), new Vector3(-w,  h,  d),
                // Back
                new Vector3( w, -h, -d), new Vector3(-w, -h, -d), new Vector3(-w,  h, -d), new Vector3( w,  h, -d),
                // Top
                new Vector3(-w,  h,  d), new Vector3( w,  h,  d), new Vector3( w,  h, -d), new Vector3(-w,  h, -d),
                // Bottom
                new Vector3(-w, -h, -d), new Vector3( w, -h, -d), new Vector3( w, -h,  d), new Vector3(-w, -h,  d),
                // Left
                new Vector3(-w, -h, -d), new Vector3(-w, -h,  d), new Vector3(-w,  h,  d), new Vector3(-w,  h, -d),
                // Right
                new Vector3( w, -h,  d), new Vector3( w, -h, -d), new Vector3( w,  h, -d), new Vector3( w,  h,  d),
            };

            // UV Mapping for Minecraft Skin
            // Texture size assumed 64x64
            float texW = 64f;
            float texH = 64f;

            // Helper to get UV rect
            // u, v are top-left coordinates in the skin texture (standard format)
            // But Unity UV is bottom-left.
            // Also, the layout is:
            // Top: (u+depth, v) size (width, depth)
            // Bottom: (u+depth+width, v) size (width, depth)
            // Right: (u, v+depth) size (depth, height)
            // Front: (u+depth, v+depth) size (width, height)
            // Left: (u+depth+width, v+depth) size (depth, height)
            // Back: (u+depth+width+depth, v+depth) size (width, height)
            
            // Wait, standard layout:
            // [Right] [Front] [Left] [Back]
            // [Top] [Bottom]
            
            // Let's define UVs for each face
            Vector2[] uvs = new Vector2[24];

            // Front Face
            SetFaceUVs(uvs, 0, u + depth, v + depth, width, height, texW, texH);
            // Back Face
            SetFaceUVs(uvs, 4, u + depth + width + depth, v + depth, width, height, texW, texH);
            // Top Face
            SetFaceUVs(uvs, 8, u + depth, v, width, depth, texW, texH);
            // Bottom Face
            SetFaceUVs(uvs, 12, u + depth + width, v, width, depth, texW, texH);
            // Left Face
            SetFaceUVs(uvs, 16, u + depth + width, v + depth, depth, height, texW, texH);
            // Right Face
            SetFaceUVs(uvs, 20, u, v + depth, depth, height, texW, texH);

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2, // Front
                4, 6, 5, 4, 7, 6, // Back
                8, 10, 9, 8, 11, 10, // Top
                12, 14, 13, 12, 15, 14, // Bottom
                16, 18, 17, 16, 19, 18, // Left
                20, 22, 21, 20, 23, 22  // Right
            };

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();

            return mesh;
        }

        private void SetFaceUVs(Vector2[] uvs, int startIndex, int u, int v, int width, int height, float texW, float texH)
        {
            // Convert to 0-1 range
            // Invert V because texture coordinates usually start top-left in skin editors, but Unity is bottom-left
            float xMin = u / texW;
            float xMax = (u + width) / texW;
            float yMax = 1f - (v / texH);
            float yMin = 1f - ((v + height) / texH);

            uvs[startIndex] = new Vector2(xMin, yMin);     // Bottom Left
            uvs[startIndex + 1] = new Vector2(xMax, yMin); // Bottom Right
            uvs[startIndex + 2] = new Vector2(xMax, yMax); // Top Right
            uvs[startIndex + 3] = new Vector2(xMin, yMax); // Top Left
        }
    }
}
