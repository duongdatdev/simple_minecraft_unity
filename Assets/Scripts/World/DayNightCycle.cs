using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    public float dayDuration = 600f; // Seconds for a full day
    [Range(0, 1)] public float timeOfDay = 0.25f; // 0.25 = Morning, 0.5 = Noon

    [Header("Celestial Bodies")]
    public Light sun;
    public Light moon;

    [Header("Lighting")]
    public float maxSunIntensity = 1f;
    public float maxMoonIntensity = 0.5f;
    public Color dayAmbient = new Color(0.8f, 0.8f, 0.8f);
    public Color nightAmbient = new Color(0.1f, 0.1f, 0.2f);

    void Update()
    {
        // Advance time
        timeOfDay += Time.deltaTime / dayDuration;
        if (timeOfDay >= 1f) timeOfDay -= 1f;

        // Calculate rotation angle (0.25 = 0 degrees/sunrise)
        float angle = (timeOfDay * 360f) - 90f;

        // Rotate Sun
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(angle, -30f, 0f);
            
            // Simple toggle for sun
            if (timeOfDay > 0.2f && timeOfDay < 0.8f)
            {
                sun.enabled = true;
                sun.intensity = maxSunIntensity;
            }
            else
            {
                sun.enabled = false;
            }
        }

        // Rotate Moon (Opposite to Sun)
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(angle + 180f, -30f, 0f);

            // Simple toggle for moon
            if (timeOfDay < 0.25f || timeOfDay > 0.75f)
            {
                moon.enabled = true;
                moon.intensity = maxMoonIntensity;
            }
            else
            {
                moon.enabled = false;
            }
        }

        // Update Ambient Light
        bool isDay = timeOfDay > 0.25f && timeOfDay < 0.75f;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, isDay ? dayAmbient : nightAmbient, Time.deltaTime * 0.5f);

        // Set global sun intensity for shader
        float intensity = 0.1f; // Night minimum
        if (sun != null && sun.enabled)
        {
            intensity = sun.intensity;
        }
        Shader.SetGlobalFloat("_SunIntensity", intensity);

        // Also set a small ambient floor so caves/overhangs are never pitch-black
        // Use a conservative default (~2 / 15 = 0.133). This keeps shadowed areas dim but visible.
        Shader.SetGlobalFloat("_AmbientFloor", 0.13f);
    }
}
