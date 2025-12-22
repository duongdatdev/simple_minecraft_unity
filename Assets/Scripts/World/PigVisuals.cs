using UnityEngine;

public class PigVisuals : MonoBehaviour
{
    public Material pigMaterial;

    void Start()
    {
        // Disabled: using prefab/model supplied by artist. Remove this component to avoid primitive generator running.
        Destroy(this);
        return;
    }

    void BuildModel()
    {
        // Create container
        GameObject model = new GameObject("Model");
        model.transform.SetParent(transform, false);

        // Body
        CreatePart(model.transform, new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.6f, 1.3f), "Body");

        // Head
        CreatePart(model.transform, new Vector3(0, 1f, 0.8f), new Vector3(0.6f, 0.6f, 0.6f), "Head");

        // Legs
        CreatePart(model.transform, new Vector3(-0.3f, 0.3f, 0.5f), new Vector3(0.25f, 0.6f, 0.25f), "LegFL");
        CreatePart(model.transform, new Vector3(0.3f, 0.3f, 0.5f), new Vector3(0.25f, 0.6f, 0.25f), "LegFR");
        CreatePart(model.transform, new Vector3(-0.3f, 0.3f, -0.5f), new Vector3(0.25f, 0.6f, 0.25f), "LegBL");
        CreatePart(model.transform, new Vector3(0.3f, 0.3f, -0.5f), new Vector3(0.25f, 0.6f, 0.25f), "LegBR");
    }

    void CreatePart(Transform parent, Vector3 pos, Vector3 scale, string name)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        
        if (pigMaterial != null)
            part.GetComponent<Renderer>().material = pigMaterial;
        else
            part.GetComponent<Renderer>().material.color = new Color(1f, 0.7f, 0.7f); // Pink
    }
}
