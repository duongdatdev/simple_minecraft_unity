using UnityEngine;

public class GameManager : MonoBehaviour
{
    public WorldGenerator worldGenerator;

    private void Start()
    {
        Debug.Log("Game initialized.");
        if (worldGenerator != null)
        {
            worldGenerator.enabled = true;
        }
    }
}