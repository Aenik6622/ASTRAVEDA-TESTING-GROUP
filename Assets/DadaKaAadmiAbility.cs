using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Abilities/Dada Ka Aadmi Ability")]
[DisallowMultipleComponent]
public class DadaKaAadmiAbility : Ability
{
    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private Transform summonOrigin;

    [Header("Input")]
    [SerializeField] private KeyCode activateKey = KeyCode.E;

    [Header("Charge Cost")]
    [SerializeField, Range(0.05f, 1f)] private float ultimateChargeCostFraction = 0.25f;

    [Header("Summon")]
    [SerializeField] private float summonDuration = 10f;
    [SerializeField] private float summonSpawnForwardOffset = 2.5f;
    [SerializeField] private float summonAttackRange = 18f;
    [SerializeField] private float summonDamagePerShot = 18f;
    [SerializeField] private float summonFireRate = 3f;

    [Header("Ally Buff Aura")]
    [SerializeField] private float buffRadius = 8f;
    [SerializeField, Range(0.1f, 1f)] private float additionalAmmoPercent = 0.5f;
    [SerializeField] private float movementSpeedMultiplier = 1.2f;
    [SerializeField] private float buffRefreshDuration = 0.3f;

    [Header("Visuals")]
    [SerializeField] private Color summonBodyColor = new Color(0.19f, 0.29f, 0.22f, 1f);
    [SerializeField] private Color summonAccentColor = new Color(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private Color auraColor = new Color(0.2f, 1f, 0.72f, 0.18f);
    [SerializeField] private Color tracerColor = new Color(1f, 0.92f, 0.32f, 1f);

    private DadaKaAadmiSummonRuntime activeSummon;
    private Canvas hudCanvas;
    private Image durationFill;
    private Text titleLabel;
    private Text statusLabel;
    private Text buffLabel;

    public override string AbilityDisplayName => "Dada ka Aadmi";
    public override string AbilityBindingLabel => "E | Summon Guard";
    public override string AbilityHudExtra => "Cost " + Mathf.RoundToInt(ultimateChargeCostFraction * 100f) + "% ult";
    public override string AbilityStatusText
    {
        get
        {
            if (activeSummon != null)
            {
                return "ACTIVE " + activeSummon.RemainingLifetime.ToString("0.0s");
            }

            return HasEnoughUltimateCharge() ? "READY" : "CHARGING " + Mathf.RoundToInt(GetChargePercent()) + "%";
        }
    }

    public override Color AbilityStatusColor => activeSummon != null
        ? new Color(0.36f, 1f, 0.78f)
        : HasEnoughUltimateCharge() ? new Color(1f, 0.9f, 0.35f) : new Color(0.72f, 0.78f, 0.9f);

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponent<BaseCharacter>();
        }

        if (summonOrigin == null)
        {
            summonOrigin = transform;
        }

        cooldown = 0f;
        AbilityHudOverlay.EnsureFor(gameObject);
        EnsureHud();
    }

    private void Update()
    {
        if (Input.GetKeyDown(activateKey))
        {
            TryUse();
        }

        if (activeSummon == null)
        {
            UpdateHud(false);
            return;
        }

        if (!activeSummon.IsAlive)
        {
            activeSummon = null;
            UpdateHud(false);
            return;
        }

        UpdateHud(true);
    }

    public override bool CanUse()
    {
        return activeSummon == null && owner != null && HasEnoughUltimateCharge();
    }

    protected override void Activate()
    {
        if (owner == null)
        {
            return;
        }

        float chargeCost = GetChargeCost();
        if (!owner.TrySpendUltimateCharge(chargeCost))
        {
            return;
        }

        Vector3 spawnPosition = summonOrigin.position + (summonOrigin.forward * summonSpawnForwardOffset);
        spawnPosition.y = Mathf.Max(0.1f, summonOrigin.position.y);

        GameObject summonObject = new GameObject("DadaKaAadmiSummon");
        summonObject.transform.position = spawnPosition;
        summonObject.transform.rotation = Quaternion.Euler(0f, summonOrigin.eulerAngles.y, 0f);

        activeSummon = summonObject.AddComponent<DadaKaAadmiSummonRuntime>();
        activeSummon.Initialize(
            owner,
            summonDuration,
            summonAttackRange,
            summonDamagePerShot,
            summonFireRate,
            buffRadius,
            additionalAmmoPercent,
            movementSpeedMultiplier,
            buffRefreshDuration,
            summonBodyColor,
            summonAccentColor,
            auraColor,
            tracerColor);
    }

    private float GetChargeCost()
    {
        return owner != null ? owner.maxUltimateCharge * ultimateChargeCostFraction : 0f;
    }

    private bool HasEnoughUltimateCharge()
    {
        return owner != null && owner.ultimateCharge >= GetChargeCost();
    }

    private float GetChargePercent()
    {
        if (owner == null || owner.maxUltimateCharge <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(owner.ultimateCharge / owner.maxUltimateCharge) * 100f;
    }

    private void EnsureHud()
    {
        GameObject canvasObject = new GameObject("DadaKaAadmiHUD");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 1235;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = CreatePanel("SummonPanel", hudCanvas.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(360f, 150f), new Color(0.05f, 0.09f, 0.1f, 0.9f));
        titleLabel = CreateText("Title", root.transform, font, 24, TextAnchor.UpperLeft, Color.white, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(-16f, 28f));
        statusLabel = CreateText("Status", root.transform, font, 18, TextAnchor.UpperLeft, new Color(0.85f, 0.94f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -46f), new Vector2(-16f, 24f));
        buffLabel = CreateText("Buff", root.transform, font, 17, TextAnchor.UpperLeft, new Color(0.74f, 1f, 0.86f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -72f), new Vector2(-16f, 48f));

        GameObject back = CreatePanel("DurationBack", root.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(16f, 18f), new Vector2(-32f, 16f), new Color(0.12f, 0.17f, 0.2f, 1f));
        GameObject fill = CreatePanel("DurationFill", back.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f), new Color(0.22f, 1f, 0.76f, 1f));
        durationFill = fill.GetComponent<Image>();

        UpdateHud(false);
    }

    private void UpdateHud(bool summonActive)
    {
        if (hudCanvas == null)
        {
            return;
        }

        hudCanvas.enabled = true;
        titleLabel.text = "DADA KA AADMI";

        if (!summonActive || activeSummon == null)
        {
            statusLabel.text = HasEnoughUltimateCharge()
                ? "Ready to deploy. Spend 25% ultimate charge."
                : "Charging: " + Mathf.RoundToInt(GetChargePercent()) + "% / " + Mathf.RoundToInt(ultimateChargeCostFraction * 100f) + "% required";
            buffLabel.text = "+" + Mathf.RoundToInt(additionalAmmoPercent * 100f) + "% ammo to nearby allies\n+" + Mathf.RoundToInt((movementSpeedMultiplier - 1f) * 100f) + "% move speed aura";
            if (durationFill != null)
            {
                durationFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
                durationFill.color = new Color(1f, 0.84f, 0.24f, 1f);
            }

            return;
        }

        float lifetimeRatio = Mathf.Clamp01(activeSummon.RemainingLifetime / Mathf.Max(0.01f, summonDuration));
        statusLabel.text = "Summon online | Shooting " + activeSummon.CurrentTargetLabel;
        buffLabel.text = "Gives +" + Mathf.RoundToInt(additionalAmmoPercent * 100f) + "% reserve ammo once\nAura: +" + Mathf.RoundToInt((movementSpeedMultiplier - 1f) * 100f) + "% speed | Radius " + buffRadius.ToString("0.#") + "m";
        if (durationFill != null)
        {
            durationFill.rectTransform.sizeDelta = new Vector2(328f * lifetimeRatio, 0f);
            durationFill.color = lifetimeRatio > 0.35f ? new Color(0.22f, 1f, 0.76f, 1f) : new Color(1f, 0.58f, 0.26f, 1f);
        }
    }

    private GameObject CreatePanel(string panelName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject panel = new GameObject(panelName);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return panel;
    }

    private Text CreateText(string name, Transform parent, Font font, int fontSize, TextAnchor anchor, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text label = textObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.offsetMin = new Vector2(rect.offsetMin.x, rect.offsetMin.y);
        rect.offsetMax = new Vector2(rect.offsetMax.x, rect.offsetMax.y);
        rect.sizeDelta = sizeDelta;
        return label;
    }
}

public class DadaKaAadmiSummonRuntime : MonoBehaviour
{
    private BaseCharacter owner;
    private float lifetime;
    private float attackRange;
    private float damagePerShot;
    private float fireCooldown;
    private float buffRadius;
    private float additionalAmmoPercent;
    private float movementMultiplier;
    private float buffRefreshDuration;
    private float nextShotTime;
    private float spawnTime;
    private string currentTargetLabel = "none";
    private Color tracerTint;
    private readonly HashSet<int> ammoGrantedCharacterIds = new HashSet<int>();

    private Transform muzzleAnchor;
    private Text worldLabel;
    private Canvas worldCanvas;

    public bool IsAlive => this != null && gameObject != null && RemainingLifetime > 0f;
    public float RemainingLifetime => Mathf.Max(0f, (spawnTime + lifetime) - Time.time);
    public string CurrentTargetLabel => currentTargetLabel;

    public void Initialize(
        BaseCharacter sourceOwner,
        float summonLifetime,
        float range,
        float damage,
        float shotsPerSecond,
        float auraRadius,
        float extraAmmoPercent,
        float movementSpeed,
        float auraRefreshDuration,
        Color bodyColor,
        Color accentColor,
        Color auraColor,
        Color tracerColor)
    {
        owner = sourceOwner;
        lifetime = Mathf.Max(0.1f, summonLifetime);
        attackRange = Mathf.Max(1f, range);
        damagePerShot = Mathf.Max(1f, damage);
        fireCooldown = 1f / Mathf.Max(0.1f, shotsPerSecond);
        buffRadius = Mathf.Max(1f, auraRadius);
        additionalAmmoPercent = Mathf.Clamp01(extraAmmoPercent);
        movementMultiplier = Mathf.Max(1f, movementSpeed);
        buffRefreshDuration = Mathf.Max(0.05f, auraRefreshDuration);
        spawnTime = Time.time;
        tracerTint = tracerColor;

        BuildVisuals(bodyColor, accentColor, auraColor);
        BuildWorldUi();
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (RemainingLifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        ApplyBuffs();
        UpdateWorldUi();

        if (Time.time < nextShotTime)
        {
            return;
        }

        if (TryFireAtEnemy())
        {
            nextShotTime = Time.time + fireCooldown;
        }
    }

    private void ApplyBuffs()
    {
        if (owner != null && Vector3.Distance(transform.position, owner.transform.position) <= buffRadius)
        {
            TryGrantAmmo(owner);
            owner.ApplyMovementBuff(movementMultiplier, buffRefreshDuration);
        }

        BaseCharacter[] characters = FindObjectsByType<BaseCharacter>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            BaseCharacter character = characters[i];
            if (character == null || character == owner)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, character.transform.position) <= buffRadius)
            {
                TryGrantAmmo(character);
                character.ApplyMovementBuff(movementMultiplier, buffRefreshDuration);
            }
        }

        BotBehaviour[] bots = FindObjectsByType<BotBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < bots.Length; i++)
        {
            BotBehaviour bot = bots[i];
            if (bot == null || bot.IsDead || bot.Team != BotBehaviour.BotTeam.Ally)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, bot.transform.position) <= buffRadius)
            {
                bot.ApplyMovementBuff(movementMultiplier, buffRefreshDuration);
            }
        }
    }

    private void TryGrantAmmo(BaseCharacter character)
    {
        if (character == null || additionalAmmoPercent <= 0f)
        {
            return;
        }

        int id = character.GetInstanceID();
        if (!ammoGrantedCharacterIds.Add(id))
        {
            return;
        }

        WeaponAbility[] weapons = character.GetComponents<WeaponAbility>();
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].GrantAdditionalAmmoPercent(additionalAmmoPercent);
        }
    }

    private bool TryFireAtEnemy()
    {
        BotBehaviour[] bots = FindObjectsByType<BotBehaviour>(FindObjectsSortMode.None);
        BotBehaviour bestTarget = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < bots.Length; i++)
        {
            BotBehaviour bot = bots[i];
            if (bot == null || bot.IsDead || bot.Team != BotBehaviour.BotTeam.Enemy)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, bot.transform.position);
            if (distance > attackRange || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = bot;
        }

        if (bestTarget == null)
        {
            currentTargetLabel = "none";
            return false;
        }

        currentTargetLabel = bestTarget.name;
        Vector3 aimPoint = bestTarget.transform.position + (Vector3.up * 1.1f);
        Vector3 flatAim = aimPoint - transform.position;
        flatAim.y = 0f;
        if (flatAim.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flatAim.normalized, Vector3.up), 10f * Time.deltaTime);
        }

        bestTarget.TakeDamage(damagePerShot);
        owner?.RegisterDamageDealt(damagePerShot);
        SpawnTracer(muzzleAnchor != null ? muzzleAnchor.position : transform.position + Vector3.up * 1.5f, aimPoint);
        return true;
    }

    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tracer.name = "AadmiTracer";
        Collider collider = tracer.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        tracer.transform.position = Vector3.Lerp(start, end, 0.5f);
        tracer.transform.rotation = Quaternion.LookRotation(end - start);
        tracer.transform.localScale = new Vector3(0.035f, 0.035f, Mathf.Max(0.25f, Vector3.Distance(start, end)));
        Renderer tracerRenderer = tracer.GetComponent<Renderer>();
        if (tracerRenderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = tracerTint;
            tracerRenderer.material = material;
        }

        Destroy(tracer, 0.08f);
    }

    private void BuildVisuals(Color bodyColor, Color accentColor, Color auraColor)
    {
        GameObject root = new GameObject("SummonVisual");
        root.transform.SetParent(transform, false);

        GameObject legs = GameObject.CreatePrimitive(PrimitiveType.Cube);
        legs.name = "Legs";
        legs.transform.SetParent(root.transform, false);
        legs.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        legs.transform.localScale = new Vector3(0.7f, 1.1f, 0.45f);

        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.SetParent(root.transform, false);
        torso.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        torso.transform.localScale = new Vector3(1f, 1.05f, 0.52f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        head.transform.localScale = new Vector3(0.56f, 0.56f, 0.56f);

        GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gun.name = "Gun";
        gun.transform.SetParent(root.transform, false);
        gun.transform.localPosition = new Vector3(0.35f, 1.55f, 0.52f);
        gun.transform.localScale = new Vector3(0.22f, 0.18f, 1.25f);

        muzzleAnchor = new GameObject("Muzzle").transform;
        muzzleAnchor.SetParent(gun.transform, false);
        muzzleAnchor.localPosition = new Vector3(0f, 0f, 0.72f);

        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        aura.name = "Aura";
        aura.transform.SetParent(transform, false);
        aura.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        aura.transform.localScale = new Vector3(buffRadius * 0.2f, 0.02f, buffRadius * 0.2f);
        Collider auraCollider = aura.GetComponent<Collider>();
        if (auraCollider != null)
        {
            Destroy(auraCollider);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material material = new Material(Shader.Find("Standard"));
            if (renderer.gameObject.name == "Head" || renderer.gameObject.name == "Gun")
            {
                material.color = accentColor;
            }
            else if (renderer.gameObject.name == "Aura")
            {
                material.color = auraColor;
            }
            else
            {
                material.color = bodyColor;
            }

            renderer.material = material;
        }
    }

    private void BuildWorldUi()
    {
        GameObject canvasObject = new GameObject("SummonWorldUI");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0f, 3.1f, 0f);

        worldCanvas = canvasObject.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = Camera.main;
        worldCanvas.scaleFactor = 0.01f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(280f, 70f);

        GameObject background = new GameObject("Background");
        background.transform.SetParent(canvasObject.transform, false);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.04f, 0.08f, 0.1f, 0.82f);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(background.transform, false);
        worldLabel = labelObject.AddComponent<Text>();
        worldLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        worldLabel.fontSize = 22;
        worldLabel.alignment = TextAnchor.MiddleCenter;
        worldLabel.color = Color.white;
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);
    }

    private void UpdateWorldUi()
    {
        if (worldCanvas == null || worldLabel == null)
        {
            return;
        }

        if (worldCanvas.worldCamera == null)
        {
            worldCanvas.worldCamera = Camera.main;
        }

        if (worldCanvas.worldCamera != null)
        {
            worldCanvas.transform.rotation = worldCanvas.worldCamera.transform.rotation;
        }

        worldLabel.text = "DADA KA AADMI\n" + RemainingLifetime.ToString("0.0s");
    }
}
