using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BotHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] private bool showAtFullHealth = false;

    private static Canvas overlayCanvas;

    private BotBehaviour bot;
    private Transform anchor;
    private RectTransform rootRect;
    private Image fillImage;
    private RectTransform fillRect;
    private Camera activeCamera;
    private float cachedHealthNormalized = 1f;

    private void Awake()
    {
        if (bot == null)
        {
            Initialize(GetComponent<BotBehaviour>());
        }
    }

    public static void EnsureFor(BotBehaviour targetBot)
    {
        if (targetBot == null || targetBot.GetComponent<BotHealthBar>() != null)
        {
            return;
        }

        targetBot.gameObject.AddComponent<BotHealthBar>().Initialize(targetBot);
    }

    private void Initialize(BotBehaviour targetBot)
    {
        if (targetBot == null || rootRect != null)
        {
            return;
        }

        bot = targetBot;
        anchor = FindHeadAnchor();
        EnsureOverlayCanvas();

        GameObject rootObject = new GameObject(bot.name + " Health Bar");
        rootObject.transform.SetParent(overlayCanvas.transform, false);
        rootRect = rootObject.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(84f, 12f);

        Image background = rootObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(rootObject.transform, false);
        fillImage = fillObject.AddComponent<Image>();
        fillImage.color = bot.Team == BotBehaviour.BotTeam.Ally ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.25f, 0.25f);

        fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        bot.HealthChanged -= HandleHealthChanged;
        bot.HealthChanged += HandleHealthChanged;
        RefreshVisuals();
    }

    private void LateUpdate()
    {
        if (bot == null || rootRect == null || fillImage == null)
        {
            return;
        }

        activeCamera = GetActiveCamera();
        if (activeCamera == null)
        {
            rootRect.gameObject.SetActive(false);
            return;
        }

        Transform follow = anchor != null ? anchor : bot.transform;
        Vector3 screenPoint = activeCamera.WorldToScreenPoint(follow.position + worldOffset);
        bool visible = screenPoint.z > 0f;

        RefreshVisuals(visible);

        if (!rootRect.gameObject.activeSelf)
        {
            return;
        }

        rootRect.position = screenPoint;
    }

    private void OnDestroy()
    {
        if (bot != null)
        {
            bot.HealthChanged -= HandleHealthChanged;
        }

        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth, bool dead)
    {
        cachedHealthNormalized = Mathf.Approximately(maxHealth, 0f) ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        RefreshVisuals();
    }

    private void RefreshVisuals(bool visible = true)
    {
        if (rootRect == null || fillImage == null || fillRect == null || bot == null)
        {
            return;
        }

        fillRect.anchorMax = new Vector2(cachedHealthNormalized, 1f);
        rootRect.gameObject.SetActive(!bot.IsDead && visible && (showAtFullHealth || cachedHealthNormalized < 0.999f));
    }

    private Transform FindHeadAnchor()
    {
        Transform[] children = bot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "Head")
            {
                return children[i];
            }
        }

        return bot.transform;
    }

    private static void EnsureOverlayCanvas()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Bot Health Bar Overlay");
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 1200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private static Camera GetActiveCamera()
    {
        Camera main = Camera.main;
        if (main != null && main.enabled)
        {
            return main;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
            {
                return cameras[i];
            }
        }

        return null;
    }
}



