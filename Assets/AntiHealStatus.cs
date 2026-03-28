using UnityEngine;

[DisallowMultipleComponent]
public class AntiHealStatus : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float antiHealPercentage;
    [SerializeField] private float remainingDuration;
    [SerializeField] private Color glowColor = new Color(0.65f, 0.15f, 1f, 1f);
    [SerializeField] private float glowIntensity = 2.2f;

    private Renderer[] renderers;
    private Material[][] instancedMaterials;
    private Color[][] originalBaseColors;
    private Color[][] originalEmissionColors;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public float AntiHealPercentage => antiHealPercentage;
    public float RemainingDuration => remainingDuration;
    public float HealingMultiplier => 1f - antiHealPercentage;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        CacheRendererMaterials();
        ApplyVisuals(true);
    }

    public void Apply(float percentage, float duration)
    {
        antiHealPercentage = Mathf.Clamp01(Mathf.Max(antiHealPercentage, percentage));
        remainingDuration = Mathf.Max(remainingDuration, duration);
        ApplyVisuals(true);
    }

    private void Update()
    {
        remainingDuration -= Time.deltaTime;
        ApplyVisuals(true);
        if (remainingDuration <= 0f)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        ApplyVisuals(false);
    }

    private void ApplyVisuals(bool active)
    {
        if (renderers == null || instancedMaterials == null)
        {
            return;
        }

        Color emission = active ? glowColor * glowIntensity : Color.black;
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = instancedMaterials[i];
            if (materials == null)
            {
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                Color baseColor = originalBaseColors[i][j];
                Color boostedBase = active ? Color.Lerp(baseColor, glowColor, 0.45f) : baseColor;
                Color originalEmission = originalEmissionColors[i][j];
                Color targetEmission = active ? MaxColor(originalEmission, emission) : originalEmission;

                if (material.HasProperty(BaseColorId))
                {
                    material.SetColor(BaseColorId, boostedBase);
                }

                if (material.HasProperty(ColorId))
                {
                    material.SetColor(ColorId, boostedBase);
                }

                if (material.HasProperty(EmissionColorId))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor(EmissionColorId, targetEmission);
                }
            }
        }
    }

    private void CacheRendererMaterials()
    {
        instancedMaterials = new Material[renderers.Length][];
        originalBaseColors = new Color[renderers.Length][];
        originalEmissionColors = new Color[renderers.Length][];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            instancedMaterials[i] = materials;
            originalBaseColors[i] = new Color[materials.Length];
            originalEmissionColors[i] = new Color[materials.Length];

            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(BaseColorId))
                {
                    originalBaseColors[i][j] = material.GetColor(BaseColorId);
                }
                else if (material.HasProperty(ColorId))
                {
                    originalBaseColors[i][j] = material.GetColor(ColorId);
                }
                else
                {
                    originalBaseColors[i][j] = Color.white;
                }

                originalEmissionColors[i][j] = material.HasProperty(EmissionColorId)
                    ? material.GetColor(EmissionColorId)
                    : Color.black;
            }
        }
    }

    private static Color MaxColor(Color a, Color b)
    {
        return new Color(
            Mathf.Max(a.r, b.r),
            Mathf.Max(a.g, b.g),
            Mathf.Max(a.b, b.b),
            Mathf.Max(a.a, b.a));
    }
}
