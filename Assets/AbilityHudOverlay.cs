using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHudOverlay : MonoBehaviour
{
    private static AbilityHudOverlay instance;

    private Ability[] abilities;
    private readonly List<AbilityHudEntry> entries = new List<AbilityHudEntry>();
    private BaseCharacter ownerCharacter;
    private Image playerHealthFill;
    private Text playerHealthLabel;

    private sealed class AbilityHudEntry
    {
        public Ability ability;
        public Text label;
    }

    private const float IconCropTopBias = 0.12f;

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
        ownerCharacter = owner.GetComponent<BaseCharacter>();
        Ability[] foundAbilities = owner.GetComponents<Ability>();
        if (foundAbilities == null || foundAbilities.Length == 0)
        {
            abilities = null;
            Rebuild();
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

        CreatePlayerHealthPanel();

        if (abilities == null || abilities.Length == 0)
        {
            entries.Clear();
            return;
        }

        entries.Clear();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        int rightIndex = 0;

        for (int i = 0; i < abilities.Length; i++)
        {
            Ability ability = abilities[i];
            if (ability == null)
            {
                continue;
            }

            bool bottomLeft = ability is WeaponAbility;
            Vector2 anchorMin = bottomLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            Vector2 anchorMax = anchorMin;
            Vector2 pivot = bottomLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            Vector2 anchoredPosition = bottomLeft
                ? new Vector2(22f, 22f + ((GetWeaponIndex(ability)) * 116f))
                : new Vector2(-22f, 22f + (rightIndex * 116f));

            if (!bottomLeft)
            {
                rightIndex++;
            }

            GameObject panelObject = new GameObject(ability.AbilityDisplayName + " HUD");
            panelObject.transform.SetParent(transform, false);
            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.08f, 0.11f, 0.82f);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = anchorMin;
            panelRect.anchorMax = anchorMax;
            panelRect.pivot = pivot;
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = new Vector2(420f, 94f);

            Image icon = null;
            string iconPath = ability.AbilityHudIconPath;
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                Sprite sprite = LoadSprite(iconPath);
                if (sprite != null)
                {
                    GameObject iconObject = new GameObject("Icon");
                    iconObject.transform.SetParent(panelObject.transform, false);
                    icon = iconObject.AddComponent<Image>();
                    icon.sprite = sprite;
                    icon.preserveAspect = true;

                    RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0f, 0.5f);
                    iconRect.anchorMax = new Vector2(0f, 0.5f);
                    iconRect.pivot = new Vector2(0f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(12f, 0f);
                    iconRect.sizeDelta = new Vector2(76f, 76f);
                }
            }

            float labelLeft = icon != null ? 94f : 16f;

            GameObject labelObject = new GameObject(ability.AbilityDisplayName + " Label");
            labelObject.transform.SetParent(panelObject.transform, false);

            Text label = labelObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.color = Color.white;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(labelLeft, 12f);
            labelRect.offsetMax = new Vector2(-14f, -12f);

            entries.Add(new AbilityHudEntry
            {
                ability = ability,
                label = label
            });
        }
    }

    private void Update()
    {
        UpdatePlayerHealthPanel();

        if (abilities == null || entries.Count == 0)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            AbilityHudEntry entry = entries[i];
            if (entry == null || entry.label == null)
            {
                continue;
            }

            Ability ability = entry.ability;
            if (ability == null)
            {
                entry.label.text = "Missing Ability";
                entry.label.color = Color.gray;
                continue;
            }

            string binding = string.IsNullOrWhiteSpace(ability.AbilityBindingLabel) ? ability.AbilityDisplayName : ability.AbilityBindingLabel;
            string extra = string.IsNullOrWhiteSpace(ability.AbilityHudExtra) ? string.Empty : "  |  " + ability.AbilityHudExtra;

            entry.label.text =
                "<b>" + ability.AbilityDisplayName + "</b>\n" +
                binding + extra + "\n" +
                "Status: " + ability.AbilityStatusText;
            entry.label.color = ability.AbilityStatusColor;
        }
    }

    private static Sprite LoadSprite(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return null;
        }

        byte[] fileBytes = File.ReadAllBytes(absolutePath);
        if (fileBytes == null || fileBytes.Length == 0)
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(fileBytes))
        {
            Object.Destroy(texture);
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Rect cropRect = GetIconCropRect(texture.width, texture.height);
        return Sprite.Create(texture, cropRect, new Vector2(0.5f, 0.5f), 100f);
    }

    private int GetWeaponIndex(Ability ability)
    {
        WeaponAbility weapon = ability as WeaponAbility;
        return weapon != null ? weapon.WeaponSlot : 0;
    }

    private static Rect GetIconCropRect(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return new Rect(0f, 0f, Mathf.Max(1, width), Mathf.Max(1, height));
        }

        if (height > width)
        {
            float squareSize = width;
            float y = Mathf.Clamp((height - squareSize) * (1f - IconCropTopBias), 0f, height - squareSize);
            return new Rect(0f, y, squareSize, squareSize);
        }

        if (width > height)
        {
            float squareSize = height;
            float x = (width - squareSize) * 0.5f;
            return new Rect(x, 0f, squareSize, squareSize);
        }

        return new Rect(0f, 0f, width, height);
    }

    private void CreatePlayerHealthPanel()
    {
        if (ownerCharacter == null)
        {
            playerHealthFill = null;
            playerHealthLabel = null;
            return;
        }

        GameObject panelObject = new GameObject("Player Health HUD");
        panelObject.transform.SetParent(transform, false);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.07f, 0.08f, 0.11f, 0.88f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 20f);
        panelRect.sizeDelta = new Vector2(360f, 52f);

        GameObject fillObject = new GameObject("Health Fill");
        fillObject.transform.SetParent(panelObject.transform, false);
        playerHealthFill = fillObject.AddComponent<Image>();
        playerHealthFill.color = new Color(0.16f, 0.92f, 0.38f, 1f);
        playerHealthFill.type = Image.Type.Filled;
        playerHealthFill.fillMethod = Image.FillMethod.Horizontal;

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(6f, 6f);
        fillRect.offsetMax = new Vector2(-6f, -6f);

        GameObject labelObject = new GameObject("Health Label");
        labelObject.transform.SetParent(panelObject.transform, false);
        playerHealthLabel = labelObject.AddComponent<Text>();
        playerHealthLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playerHealthLabel.fontSize = 22;
        playerHealthLabel.alignment = TextAnchor.MiddleCenter;
        playerHealthLabel.color = Color.white;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void UpdatePlayerHealthPanel()
    {
        if (ownerCharacter == null || playerHealthFill == null || playerHealthLabel == null)
        {
            return;
        }

        float normalizedHealth = Mathf.Approximately(ownerCharacter.maxHealth, 0f) ? 0f : Mathf.Clamp01(ownerCharacter.health / ownerCharacter.maxHealth);
        playerHealthFill.fillAmount = normalizedHealth;
        playerHealthFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.16f, 0.92f, 0.38f), normalizedHealth);
        playerHealthLabel.text = "HP " + Mathf.CeilToInt(ownerCharacter.health) + " / " + Mathf.CeilToInt(ownerCharacter.maxHealth);
    }
}
