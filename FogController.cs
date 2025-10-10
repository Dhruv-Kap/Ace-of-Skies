using UnityEngine;

public class FogController : MonoBehaviour
{
    [Header("Fog Settings")]
    public bool enableFog = true;
    public Color fogColor = new Color(0.7f, 0.8f, 0.9f, 1f);
    public FogMode fogMode = FogMode.ExponentialSquared;
    public float fogDensity = 0.01f;

    void Start()
    {
        ApplyFogSettings();
    }

    void ApplyFogSettings()
    {
        RenderSettings.fog = enableFog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
    }

    // Update fog in real-time when you change inspector values
    void OnValidate()
    {
        ApplyFogSettings();
    }
}