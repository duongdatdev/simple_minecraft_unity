using UnityEngine;
using UnityEditor;
using MinecraftGPT.Entities;

public class ZombieCreator : EditorWindow
{
    [MenuItem("MinecraftGPT/Create Zombie Prefab")]
    public static void CreateZombie()
    {
        GameObject zombie = new GameObject("Zombie");
        
        // Add Components
        ZombieController controller = zombie.AddComponent<ZombieController>();
        ZombieVisuals visuals = zombie.AddComponent<ZombieVisuals>();
        
        CapsuleCollider collider = zombie.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 1, 0);
        collider.height = 2f;
        collider.radius = 0.5f;

        Rigidbody rb = zombie.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 1f;

        // Select the new object
        Selection.activeGameObject = zombie;
        
        Debug.Log("Zombie GameObject created! Please assign the Zombie Skin texture to the ZombieVisuals component.");
    }

    [MenuItem("MinecraftGPT/Create Zombie Spawner")]
    public static void CreateZombieSpawner()
    {
        GameObject spawner = new GameObject("ZombieSpawner");
        ZombieSpawner script = spawner.AddComponent<ZombieSpawner>();
        
        // Try to find player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) script.player = player.transform;

        Selection.activeGameObject = spawner;
        Debug.Log("ZombieSpawner created! Please assign the 'Zombie Prefab' in the Inspector.");
    }
}
