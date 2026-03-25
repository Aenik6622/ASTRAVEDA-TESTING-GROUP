using UnityEngine;
using UnityEngine.UI;

public class AbilityHudOverlay : MonoBehaviour
{
    private static AbilityHudOverlay instance;

    private Ability[] abilities;
    private Text[] labels;

    public static void EnsureFor(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        if (instance == null)
        {
            CreateOverlay();
        }

        if (instance != null)
        {
            instance.Bind(owner);
        }
    }

    private static void CreateOverlay()
    {
        GameObject canvasObject = new GameObject("Ability HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        instance = canvasObject.AddComponent<AbilityHudOverlay>();
    }

    private void Bind(GameObject owner)
    {
        Ability[] foundAbilities = owner.GetComponents<Ability>();
        if (foundAbilities == null || foundAbilities.Length == 0)
        {
            return;
        }

        abilities = foundAbilities;
        Rebuild();
    }

    private void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        if (abilities == null || abilities.Length == 0)
        {
            labels = null;
            return;
        }

        labels = new Text[abilities.Length];

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(transform, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.07f, 0.08f, 0.11f, 0.76f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(22f, 22f);
        panelRect.sizeDelta = new Vector2(320f, 26f + (abilities.Length * 46f));

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        for (int i = 0; i < abilities.Length; i++)
        {
            GameObject labelObject = new GameObject(abilities[i].AbilityDisplayName + " Label");
            labelObject.transform.SetParent(panelObject.transform, false);

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = Color.white;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.offsetMin = new Vector2(16f, -44f - (i * 40f));
            labelRect.offsetMax = new Vector2(-16f, -10f - (i * 40f));

            labels[i] = label;
        }
    }

    private void Update()
    {
        if (abilities == null || labels == null)
        {
            return;
        }

        for (int i = 0; i < abilities.Length && i < labels.Length; i++)
        {
            Ability ability = abilities[i];
            if (labels[i] == null)
            {
                continue;
            }

            if (ability == null)
            {
                labels[i].text = "Missing Ability";
                labels[i].color = Color.gray;
                continue;
            }

            float cooldownRemaining = ability.CooldownRemaining;
            bool ready = cooldownRemaining <= 0.01f && ability.CanUse();
            string status = ready ? "READY" : cooldownRemaining > 0.01f ? cooldownRemaining.ToString("0.0s") : "ACTIVE";
            string binding = string.IsNullOrWhiteSpace(ability.AbilityBindingLabel) ? ability.AbilityDisplayName : ability.AbilityBindingLabel;

            labels[i].text = binding + "  |  " + ability.AbilityDisplayName + "  |  " + status;
            labels[i].color = ready ? new Color(0.82f, 1f, 0.82f) : new Color(1f, 0.78f, 0.55f);
        }
    }
}
