// Assets/Scripts/Debug/SingleCubeTest.cs
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SingleCubeTest : MonoBehaviour
{
    void Start()
    {
        Mesh m = new Mesh();
        Vector3 p000 = new Vector3(0,0,0);
        Vector3 p100 = new Vector3(1,0,0);
        Vector3 p110 = new Vector3(1,1,0);
        Vector3 p010 = new Vector3(0,1,0);
        Vector3 p001 = new Vector3(0,0,1);
        Vector3 p101 = new Vector3(1,0,1);
        Vector3 p111 = new Vector3(1,1,1);
        Vector3 p011 = new Vector3(0,1,1);

        Vector3[] verts = new Vector3[24];
        Vector2[] uvs = new Vector2[24];
        int[] tris = new int[36];

        int vi = 0, ti = 0;
        void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            verts[vi+0] = a; verts[vi+1] = b; verts[vi+2] = c; verts[vi+3] = d;
            uvs[vi+0] = new Vector2(0,0); uvs[vi+1] = new Vector2(1,0);
            uvs[vi+2] = new Vector2(1,1); uvs[vi+3] = new Vector2(0,1);

            tris[ti+0] = vi+0; tris[ti+1] = vi+1; tris[ti+2] = vi+2;
            tris[ti+3] = vi+0; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
            vi += 4; ti += 6;
        }

        // add faces (winding outward)
        AddFace(p000, p010, p110, p100); // Back (-Z)
        AddFace(p101, p111, p011, p001); // Front (+Z)
        AddFace(p010, p011, p111, p110); // Top (+Y)
        AddFace(p000, p100, p101, p001); // Bottom (-Y)
        AddFace(p001, p011, p010, p000); // Left (-X)
        AddFace(p100, p110, p111, p101); // Right (+X)

        m.vertices = verts;
        m.triangles = tris;
        m.uv = uvs;
        m.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = m;
    }
}