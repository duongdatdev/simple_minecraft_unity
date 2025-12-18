// Assets/Scripts/Debug/QuadTest.cs
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class QuadTest : MonoBehaviour
{
    void Start()
    {
        Mesh m = new Mesh();
        Vector3[] verts = new Vector3[]
        {
            new Vector3(0,0,0),
            new Vector3(1,0,0),
            new Vector3(1,1,0),
            new Vector3(0,1,0)
        };
        int[] tris = new int[]
        {
            0,1,2,   // triangle 1
            0,2,3    // triangle 2
        };
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0,0),
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1)
        };
        m.vertices = verts;
        m.triangles = tris;
        m.uv = uvs;
        m.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = m;
    }
}