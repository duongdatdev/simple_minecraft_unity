using UnityEngine;

public static class BiomeColor
{
    private static Texture2D grassColorMap;
    private static Texture2D foliageColorMap;
    private static Color[] colorPixels;
    private static Color[] foliagePixels;
    private static int width;
    private static int height;
    private static int foliageWidth;
    private static int foliageHeight;

    public static void Initialize()
    {
        if (grassColorMap != null && foliageColorMap != null) return;

        // Load from Resources/Textures/grass.png (or just "grass" if in root of Resources)
        // User said "grass.png", usually implies a file.
        // We'll try loading "BlockTextures/grass" or just "grass"
        if (grassColorMap == null)
        {
            grassColorMap = Resources.Load<Texture2D>("grass"); 
            if (grassColorMap == null) grassColorMap = Resources.Load<Texture2D>("BlockTextures/grass");
            
            if (grassColorMap != null)
            {
                if (!grassColorMap.isReadable)
                {
                    Debug.LogWarning("grass.png is not readable. Please enable Read/Write in import settings.");
                }
                else
                {
                    colorPixels = grassColorMap.GetPixels();
                    width = grassColorMap.width;
                    height = grassColorMap.height;
                }
            }
        }

        if (foliageColorMap == null)
        {
            foliageColorMap = Resources.Load<Texture2D>("foliage");
            if (foliageColorMap == null) foliageColorMap = Resources.Load<Texture2D>("BlockTextures/foliage");

            if (foliageColorMap != null)
            {
                if (!foliageColorMap.isReadable)
                {
                    Debug.LogWarning("foliage.png is not readable. Please enable Read/Write in import settings.");
                }
                else
                {
                    foliagePixels = foliageColorMap.GetPixels();
                    foliageWidth = foliageColorMap.width;
                    foliageHeight = foliageColorMap.height;
                }
            }
        }
    }

    public static Color GetGrassColor(float temperature, float humidity)
    {
        if (colorPixels == null) 
        {
            Initialize();
            if (colorPixels == null) return new Color(0.5f, 1f, 0.5f); // Fallback green
        }

        // Clamp 0..1
        temperature = Mathf.Clamp01(temperature);
        humidity = Mathf.Clamp01(humidity);

        // Minecraft foliage map is usually a triangle.
        // x = temperature, y = humidity * temperature
        // But simple mapping: x = (1-temp), y = humidity
        // Let's just do simple UV mapping for now.
        
        int x = Mathf.FloorToInt((1f - temperature) * (width - 1));
        int y = Mathf.FloorToInt(humidity * (height - 1)); // often humidity * temp in MC

        // Safety
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);

        return colorPixels[y * width + x];
    }

    public static Color GetFoliageColor(float temperature, float humidity)
    {
        if (foliagePixels == null) 
        {
            Initialize();
            if (foliagePixels == null) 
            {
                // Fallback if no foliage texture found. 
                // Use grass color if available, otherwise hardcoded green.
                if (colorPixels != null) return GetGrassColor(temperature, humidity);
                return new Color(0.28f, 0.7f, 0.16f); // Fallback leaf green
            }
        }

        // Clamp 0..1
        temperature = Mathf.Clamp01(temperature);
        humidity = Mathf.Clamp01(humidity);

        int x = Mathf.FloorToInt((1f - temperature) * (foliageWidth - 1));
        int y = Mathf.FloorToInt(humidity * (foliageHeight - 1)); 

        // Safety
        x = Mathf.Clamp(x, 0, foliageWidth - 1);
        y = Mathf.Clamp(y, 0, foliageHeight - 1);

        return foliagePixels[y * foliageWidth + x];
    }
}
