using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helper: add PlayerArm and PlayerCombat to the selected Camera in the Hierarchy.
/// Menu: Tools > MinecraftGPT > Add Player Arm & Combat to Selected Camera
/// </summary>
public static class AddPlayerToolsToCamera
{
    [MenuItem("Tools/MinecraftGPT/Add Player Arm & Combat to Selected Camera", priority = 101)]
    public static void AddToSelectedCamera()
    {
        if (Selection.activeGameObject == null)
        {
            EditorUtility.DisplayDialog("Add Player Tools", "Please select a Camera GameObject in the Hierarchy.", "OK");
            return;
        }

        GameObject go = Selection.activeGameObject;
        Camera cam = go.GetComponent<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Add Player Tools", "Selected object is not a Camera. Select the Camera GameObject (child of Player).", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(go, "Add Player Tools");

        // Add PlayerArm if missing
        PlayerArm arm = go.GetComponent<PlayerArm>();
        if (arm == null)
        {
            arm = Undo.AddComponent<PlayerArm>(go);
        }

        // Add PlayerCombat if missing and configure references
        PlayerCombat combat = go.GetComponent<PlayerCombat>();
        if (combat == null)
        {
            combat = Undo.AddComponent<PlayerCombat>(go);
            combat.cameraTransform = go.transform;
            combat.playerArm = arm;

            // try to find PlayerController in parent hierarchy
            PlayerController pc = go.GetComponentInParent<PlayerController>();
            if (pc != null) combat.player = pc;
        }

        EditorUtility.SetDirty(go);
        EditorUtility.DisplayDialog("Add Player Tools", "PlayerArm and PlayerCombat were added/configured on the selected Camera.", "OK");
    }

    [MenuItem("Tools/MinecraftGPT/Add Player Arm & Combat to Selected Camera", true)]
    public static bool AddToSelectedCamera_Validate()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Camera>() != null;
    }
}