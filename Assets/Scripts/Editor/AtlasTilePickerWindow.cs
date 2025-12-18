// Assets/Editor/AtlasTilePickerWindow.cs
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Atlas Tile Picker Window with both outer scroll (for whole window) and inner scroll (for preview).
/// Robust overlay drawing: grid & highlight are drawn in the same GUI-local coordinates as the texture,
/// and fixed viewport padding issue so grid always lines up with the image.
/// </summary>
public class AtlasTilePickerWindow : EditorWindow
{
    private Texture2D atlas;
    private int tilePixelW = 16;
    private int tilePixelH = 16;

    // computed
    private int tilesPerRow;
    private int tilesPerColumn;

    // preview controls
    private float previewScale = 1.0f;
    private Vector2 previewScroll = Vector2.zero;
    private float previewMaxHeight = 600f;

    // outer scroll for whole window
    private Vector2 mainScroll = Vector2.zero;

    // picked tile
    private int pickedX = -1;
    private int pickedYImage = -1;
    private int pickedYBottom = -1;

    // asset creation
    private BlockType selectedBlockType = BlockType.Grass;
    private bool setTop = true;
    private bool setSide = true;
    private bool setBottom = true;
    private string saveFolder = "Assets/Resources/BlockTextures";

    [MenuItem("Tools/Atlas Tile Picker")]
    public static void ShowWindow()
    {
        var wnd = GetWindow<AtlasTilePickerWindow>("Atlas Tile Picker");
        wnd.minSize = new Vector2(480, 320);
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        EditorGUILayout.LabelField("Atlas Tile Picker", EditorStyles.boldLabel);

        float oldScale = previewScale;

        EditorGUI.BeginChangeCheck();
        atlas = (Texture2D)EditorGUILayout.ObjectField("Atlas Texture", atlas, typeof(Texture2D), false);
        tilePixelW = Mathf.Max(1, EditorGUILayout.IntField("Tile Pixel Width", tilePixelW));
        tilePixelH = Mathf.Max(1, EditorGUILayout.IntField("Tile Pixel Height", tilePixelH));
        previewScale = EditorGUILayout.Slider("Preview Scale", previewScale, 0.25f, 4.0f);
        if (EditorGUI.EndChangeCheck())
        {
            if (!Mathf.Approximately(oldScale, previewScale))
            {
                float scaleFactor = previewScale / Mathf.Max(0.00001f, oldScale);
                previewScroll *= scaleFactor;
            }
            pickedX = pickedYImage = pickedYBottom = -1;
            Repaint();
        }

        if (atlas == null)
        {
            EditorGUILayout.HelpBox("Assign your atlas texture (e.g. terrain.png).", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        // compute tiles per row/column
        tilesPerRow = Mathf.Max(1, atlas.width / Mathf.Max(1, tilePixelW));
        tilesPerColumn = Mathf.Max(1, atlas.height / Mathf.Max(1, tilePixelH));
        EditorGUILayout.LabelField($"Atlas size: {atlas.width} x {atlas.height} px   Tiles: {tilesPerRow} x {tilesPerColumn}");

        EditorGUILayout.Space();

        float previewWidth = atlas.width * previewScale;
        float previewHeight = atlas.height * previewScale;
        float viewHeight = Mathf.Min(previewHeight, previewMaxHeight);

        // Reserve space for inner scroll view.
        Rect scrollViewContainer = GUILayoutUtility.GetRect(position.width - 20, viewHeight, GUILayout.ExpandWidth(false));

        // clamp previewScroll to valid range
        previewScroll = ClampPreviewScroll(previewScroll, previewWidth, previewHeight, scrollViewContainer);

        // begin inner scroll
        previewScroll = GUI.BeginScrollView(scrollViewContainer, previewScroll, new Rect(0, 0, previewWidth, previewHeight), false, false);

        // draw background + texture (content-local coords)
        Rect drawRect = new Rect(0, 0, previewWidth, previewHeight);
        EditorGUI.DrawRect(drawRect, new Color(0.12f, 0.12f, 0.12f));
        if (Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(drawRect, atlas, ScaleMode.StretchToFill, false);
        }

        // mouse handling: convert window mouse to content-local coords
        // Inside BeginScrollView, Event.current.mousePosition is already in content-local coords.
        Vector2 mouseInContent = Event.current.mousePosition;

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Rect contentRect = new Rect(0, 0, previewWidth, previewHeight);
            // Check if mouse is within the visible viewport to avoid clicks outside the scroll area
            Rect viewportRect = new Rect(previewScroll.x, previewScroll.y, scrollViewContainer.width, scrollViewContainer.height);

            if (contentRect.Contains(mouseInContent) && viewportRect.Contains(mouseInContent))
            {
                // convert to atlas pixel coords by dividing by previewScale
                float pixelXF = mouseInContent.x / Mathf.Max(0.0001f, previewScale);
                float pixelYFromTopF = mouseInContent.y / Mathf.Max(0.0001f, previewScale);

                int pixelX = Mathf.Clamp(Mathf.FloorToInt(pixelXF), 0, atlas.width - 1);
                int pixelYFromTop = Mathf.Clamp(Mathf.FloorToInt(pixelYFromTopF), 0, atlas.height - 1);

                int tileX = pixelX / tilePixelW;
                int tileYImage = pixelYFromTop / tilePixelH;
                int tileYBottom = tilesPerColumn - 1 - tileYImage;

                pickedX = tileX;
                pickedYImage = tileYImage;
                pickedYBottom = tileYBottom;

                Repaint();
                Event.current.Use();
            }
        }

        // draw grid and highlight
        if (Event.current.type == EventType.Repaint)
        {
            // Đang ở content space của ScrollView rồi, cứ vẽ với (0,0)
            DrawGridAndHighlightInContentSpace(new Rect(0, 0, previewWidth, previewHeight), previewScale);
        }

        GUI.EndScrollView();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Picked tile (bottom-left origin):");
        EditorGUILayout.LabelField($"tileX = {pickedX}   tileY_bottom = {pickedYBottom}");
        EditorGUILayout.LabelField($"(image origin tileY_top = {pickedYImage})");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Create BlockTextureData asset from picked tile", EditorStyles.boldLabel);
        selectedBlockType = (BlockType)EditorGUILayout.EnumPopup("BlockType", selectedBlockType);
        setTop = EditorGUILayout.Toggle("Set Top", setTop);
        setSide = EditorGUILayout.Toggle("Set Side", setSide);
        setBottom = EditorGUILayout.Toggle("Set Bottom", setBottom);
        saveFolder = EditorGUILayout.TextField("Save Folder", saveFolder);

        using (new EditorGUI.DisabledScope(pickedX < 0))
        {
            if (GUILayout.Button("Create BlockTextureData Asset"))
            {
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);
                string fileName = $"{selectedBlockType}_Block.asset";
                string path = Path.Combine(saveFolder, fileName);
                path = path.Replace("\\", "/");

                BlockTextureData existing = AssetDatabase.LoadAssetAtPath<BlockTextureData>(path);
                BlockTextureData so;
                if (existing != null)
                {
                    so = existing;
                    Undo.RecordObject(so, "Update BlockTextureData");
                }
                else
                {
                    so = ScriptableObject.CreateInstance<BlockTextureData>();
                }

                so.blockType = selectedBlockType;
                Vector2Int tile = new Vector2Int(pickedX, pickedYBottom);
                if (setTop) so.up = tile;
                if (setBottom) so.down = tile;
                if (setSide) 
                {
                    so.front = tile;
                    so.back = tile;
                    so.left = tile;
                    so.right = tile;
                }

                if (existing == null)
                {
                    AssetDatabase.CreateAsset(so, path);
                    Debug.Log($"Created {path} with tile ({pickedX},{pickedYBottom}) for {selectedBlockType}");
                }
                else
                {
                    EditorUtility.SetDirty(so);
                    Debug.Log($"Updated {path} with tile ({pickedX},{pickedYBottom}) for {selectedBlockType}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Notes:\n- Tile coords use BOTTOM-LEFT origin (0,0).\n- Outer scroll allows you to scroll entire window when content is taller than the window.\n- If preview looks blurry, set texture Filter Mode = Point and Compression = None.", MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.EndScrollView();
    }

    // Draw grid lines and highlight using content-local coordinates (0..contentRect.width/height).
    // This function runs inside GUI.BeginGroup(contentOnScreen) so drawing maps exactly to pixel positions on screen.
    private void DrawGridAndHighlightInContentSpace(Rect contentRect, float previewScale)
    {
        float cellW = tilePixelW * previewScale;
        float cellH = tilePixelH * previewScale;

        float thickness = Mathf.Max(1f, 1f * EditorGUIUtility.pixelsPerPoint);

        Color gridCol = new Color(1f, 1f, 1f, 0.12f);
        Color pickFill = new Color(0f, 1f, 0f, 0.12f);
        Color pickOutline = new Color(0f, 1f, 0f, 0.9f);

        // vertical grid lines
        for (int i = 0; i <= tilesPerRow; i++)
        {
            float x = i * cellW - (thickness * 0.5f);
            Rect lineRect = new Rect(x, 0, thickness, contentRect.height);
            EditorGUI.DrawRect(lineRect, gridCol);
        }

        // horizontal grid lines
        for (int j = 0; j <= tilesPerColumn; j++)
        {
            float y = j * cellH - (thickness * 0.5f);
            Rect lineRect = new Rect(0, y, contentRect.width, thickness);
            EditorGUI.DrawRect(lineRect, gridCol);
        }

        // highlight picked tile (pickedYImage counted from top)
        if (pickedX >= 0 && pickedYImage >= 0)
        {
            int col = pickedX;
            int rowFromTop = pickedYImage;

            float px = col * cellW;
            float py = rowFromTop * cellH;

            Rect fillRect = new Rect(px, py, cellW, cellH);
            EditorGUI.DrawRect(fillRect, pickFill);

            EditorGUI.DrawRect(new Rect(px, py - thickness * 0.5f, cellW, thickness), pickOutline);
            EditorGUI.DrawRect(new Rect(px, py + cellH - thickness * 0.5f, cellW, thickness), pickOutline);
            EditorGUI.DrawRect(new Rect(px - thickness * 0.5f, py, thickness, cellH), pickOutline);
            EditorGUI.DrawRect(new Rect(px + cellW - thickness * 0.5f, py, thickness, cellH), pickOutline);
        }
    }

    private Vector2 ClampPreviewScroll(Vector2 scroll, float contentWidth, float contentHeight, Rect viewportRect)
    {
        float maxX = Mathf.Max(0f, contentWidth - viewportRect.width);
        float maxY = Mathf.Max(0f, contentHeight - viewportRect.height);

        scroll.x = Mathf.Clamp(scroll.x, 0f, maxX);
        scroll.y = Mathf.Clamp(scroll.y, 0f, maxY);
        return scroll;
    }
}
