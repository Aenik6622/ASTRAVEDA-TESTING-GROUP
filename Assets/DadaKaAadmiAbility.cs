using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Abilities/Dada Ka Aadmi Ability")]
[DisallowMultipleComponent]
public class DadaKaAadmiAbility : Ability
{
    private enum EquipState
    {
        Idle,
        Equipped
    }

    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private Transform summonOrigin;
    [SerializeField] private WeaponLoadout weaponLoadout;

    [Header("Input")]
    [SerializeField] private KeyCode activateKey = KeyCode.E;
    [SerializeField] private int confirmMouseButton = 0;
    [SerializeField] private int cancelMouseButton = 1;

    [Header("Charge Cost")]
    [SerializeField, Range(0.05f, 1f)] private float ultimateChargeCostFraction = 0.25f;

    [Header("Summon")]
    [SerializeField] private float summonDuration = 10f;
    [SerializeField] private float summonSpawnForwardOffset = 2.5f;
    [SerializeField] private float summonAttackRange = 18f;
    [SerializeField] private float summonDamagePerShot = 18f;
    [SerializeField] private float summonFireRate = 3f;
    [SerializeField] private LayerMask spawnSurfaceMask = ~0;
    [SerializeField] private float spawnPreviewHeightOffset = 0.05f;

    [Header("Ammo Interaction")]
    [SerializeField] private float buffRadius = 8f;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float ammoInteractCooldown = 5f;
    [SerializeField, Range(0.1f, 1f)] private float additionalAmmoPercent = 0.5f;
    [SerializeField] private KeyCode interactKey = KeyCode.G;

    [Header("Visuals")]
    [SerializeField] private Color summonBodyColor = new Color(0.19f, 0.29f, 0.22f, 1f);
    [SerializeField] private Color summonAccentColor = new Color(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private Color auraColor = new Color(0.2f, 1f, 0.72f, 0.18f);
    [SerializeField] private Color tracerColor = new Color(1f, 0.92f, 0.32f, 1f);
    [SerializeField] private Color previewValidColor = new Color(0.22f, 1f, 0.76f, 0.28f);
    [SerializeField] private Color previewInvalidColor = new Color(1f, 0.36f, 0.26f, 0.28f);

    private DadaKaAadmiSummonRuntime activeSummon;
    private EquipState equipState;
    private Canvas hudCanvas;
    private Image durationFill;
    private Text titleLabel;
    private Text statusLabel;
    private Text buffLabel;
    private GameObject spawnPreview;
    private Renderer spawnPreviewRenderer;
    private Vector3 previewSpawnPosition;
    private bool hasValidPreview;

    public override string AbilityDisplayName => "Dada ka Aadmi";
    public override string AbilityBindingLabel => equipState == EquipState.Equipped ? "LMB Spawn | RMB Cancel" : "E Equip";
    public override string AbilityHudExtra => "Cost " + Mathf.RoundToInt(ultimateChargeCostFraction * 100f) + "% ult";
    public override string AbilityStatusText
    {
        get
        {
            if (activeSummon != null)
            {
                return "ACTIVE " + activeSummon.RemainingLifetime.ToString("0.0s");
            }

            if (equipState == EquipState.Equipped)
            {
                return hasValidPreview ? "PLACEMENT READY" : "BAD PLACEMENT";
            }

            return HasEnoughUltimateCharge() ? "READY" : "CHARGING " + Mathf.RoundToInt(GetChargePercent()) + "%";
        }
    }

    public override Color AbilityStatusColor => activeSummon != null
        ? new Color(0.36f, 1f, 0.78f)
        : equipState == EquipState.Equipped
            ? (hasValidPreview ? new Color(0.36f, 1f, 0.78f) : new Color(1f, 0.45f, 0.3f))
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

        if (weaponLoadout == null)
        {
            weaponLoadout = GetComponent<WeaponLoadout>();
        }

        cooldown = 0f;
        AbilityHudOverlay.EnsureFor(gameObject);
        EnsureSpawnPreview();
    }

    private void Update()
    {
        if (activeSummon == null)
        {
            if (equipState == EquipState.Idle)
            {
                if (Input.GetKeyDown(activateKey) && activeSummon == null && owner != null && HasEnoughUltimateCharge())
                {
                    EnterEquipState();
                }
            }
            else
            {
                UpdateSpawnPreview();

                if (Input.GetMouseButtonDown(cancelMouseButton))
                {
                    ExitEquipState();
                }
                else if (Input.GetMouseButtonDown(confirmMouseButton) && hasValidPreview)
                {
                    TryUse();
                }
            }
        }

        if (activeSummon == null)
        {
            UpdateHud(equipState == EquipState.Equipped);
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
        return activeSummon == null && equipState == EquipState.Equipped && owner != null && HasEnoughUltimateCharge() && hasValidPreview;
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

        GameObject summonObject = new GameObject("DadaKaAadmiSummon");
        summonObject.transform.position = previewSpawnPosition;
        summonObject.transform.rotation = Quaternion.Euler(0f, summonOrigin.eulerAngles.y, 0f);

        activeSummon = summonObject.AddComponent<DadaKaAadmiSummonRuntime>();
        activeSummon.Initialize(
            owner,
            summonDuration,
            summonAttackRange,
            summonDamagePerShot,
            summonFireRate,
            buffRadius,
            interactDistance,
            ammoInteractCooldown,
            additionalAmmoPercent,
            interactKey,
            summonBodyColor,
            summonAccentColor,
            auraColor,
            tracerColor);
        ExitEquipState();
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
        if (hudCanvas != null)
        {
            return;
        }

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
        bool shouldShowHud = summonActive || equipState == EquipState.Equipped;
        if (!shouldShowHud && hudCanvas == null)
        {
            return;
        }

        EnsureHud();
        if (hudCanvas == null)
        {
            return;
        }

        hudCanvas.enabled = shouldShowHud;
        if (!shouldShowHud)
        {
            return;
        }

        titleLabel.text = "DADA KA AADMI";

        if (!summonActive || activeSummon == null)
        {
            if (equipState == EquipState.Equipped)
            {
                statusLabel.text = hasValidPreview
                    ? "Left click to place the guard."
                    : "Move to a valid spawn point or right click to cancel.";
                buffLabel.text = "Shows spawn point preview\nG near the summon gives +" + Mathf.RoundToInt(additionalAmmoPercent * 100f) + "% reserve ammo";
            }
            else
            {
                statusLabel.text = HasEnoughUltimateCharge()
                    ? "Press E to equip and place the guard."
                    : "Charging: " + Mathf.RoundToInt(GetChargePercent()) + "% / " + Mathf.RoundToInt(ultimateChargeCostFraction * 100f) + "% required";
                buffLabel.text = "Interacting with the summon grants +" + Mathf.RoundToInt(additionalAmmoPercent * 100f) + "% reserve ammo\nAmmo station cooldown: " + ammoInteractCooldown.ToString("0.#") + "s";
            }

            if (durationFill != null)
            {
                durationFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
                durationFill.color = equipState == EquipState.Equipped
                    ? (hasValidPreview ? new Color(0.22f, 1f, 0.76f, 1f) : new Color(1f, 0.58f, 0.26f, 1f))
                    : new Color(1f, 0.84f, 0.24f, 1f);
            }

            return;
        }

        float lifetimeRatio = Mathf.Clamp01(activeSummon.RemainingLifetime / Mathf.Max(0.01f, summonDuration));
        statusLabel.text = "Summon online | Shooting " + activeSummon.CurrentTargetLabel;
        buffLabel.text = "Press G within " + interactDistance.ToString("0.#") + "m for +" + Mathf.RoundToInt(additionalAmmoPercent * 100f) + "% ammo\nAmmo cooldown " + activeSummon.AmmoCooldownRemaining.ToString("0.0s") + " | Radius " + buffRadius.ToString("0.#") + "m";
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

    private void EnterEquipState()
    {
        equipState = EquipState.Equipped;
        weaponLoadout?.SetLoadoutLocked(true);
        EnsureSpawnPreview();
        UpdateSpawnPreview();
    }

    private void ExitEquipState()
    {
        equipState = EquipState.Idle;
        hasValidPreview = false;
        weaponLoadout?.SetLoadoutLocked(false);
        if (spawnPreview != null)
        {
            spawnPreview.SetActive(false);
        }
    }

    private void EnsureSpawnPreview()
    {
        if (spawnPreview != null)
        {
            return;
        }

        spawnPreview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spawnPreview.name = "DadaKaAadmiSpawnPreview";
        spawnPreview.transform.localScale = new Vector3(0.65f, 0.04f, 0.65f);
        Collider previewCollider = spawnPreview.GetComponent<Collider>();
        if (previewCollider != null)
        {
            Destroy(previewCollider);
        }

        spawnPreviewRenderer = spawnPreview.GetComponent<Renderer>();
        if (spawnPreviewRenderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = previewValidColor;
            spawnPreviewRenderer.material = material;
        }

        spawnPreview.SetActive(false);
    }

    private void UpdateSpawnPreview()
    {
        EnsureSpawnPreview();
        if (spawnPreview == null)
        {
            return;
        }

        Vector3 candidatePosition = summonOrigin.position + (summonOrigin.forward * summonSpawnForwardOffset);
        candidatePosition.y = summonOrigin.position.y + 1f;

        if (Physics.Raycast(candidatePosition, Vector3.down, out RaycastHit groundHit, 6f, spawnSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            previewSpawnPosition = groundHit.point;
            previewSpawnPosition.y += spawnPreviewHeightOffset;
            hasValidPreview = true;
        }
        else
        {
            previewSpawnPosition = summonOrigin.position + (summonOrigin.forward * summonSpawnForwardOffset);
            previewSpawnPosition.y = Mathf.Max(0.1f, summonOrigin.position.y);
            hasValidPreview = false;
        }

        spawnPreview.transform.position = previewSpawnPosition;
        spawnPreview.transform.rotation = Quaternion.identity;
        spawnPreview.SetActive(true);

        if (spawnPreviewRenderer != null)
        {
            spawnPreviewRenderer.material.color = hasValidPreview ? previewValidColor : previewInvalidColor;
        }
    }

    private void OnDisable()
    {
        if (spawnPreview != null)
        {
            spawnPreview.SetActive(false);
        }

        equipState = EquipState.Idle;
        weaponLoadout?.SetLoadoutLocked(false);
        if (hudCanvas != null)
        {
            hudCanvas.enabled = false;
        }
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
    private float interactDistance;
    private float ammoInteractCooldown;
    private float additionalAmmoPercent;
    private KeyCode interactKey;
    private float nextShotTime;
    private float spawnTime;
    private float nextAmmoInteractTime;
    private string currentTargetLabel = "none";
    private Color tracerTint;

    private Transform muzzleAnchor;
    private Text worldLabel;
    private Canvas worldCanvas;

    public bool IsAlive => this != null && gameObject != null && RemainingLifetime > 0f;
    public float RemainingLifetime => Mathf.Max(0f, (spawnTime + lifetime) - Time.time);
    public float AmmoCooldownRemaining => Mathf.Max(0f, nextAmmoInteractTime - Time.time);
    public string CurrentTargetLabel => currentTargetLabel;

    public void Initialize(
        BaseCharacter sourceOwner,
        float summonLifetime,
        float range,
        float damage,
        float shotsPerSecond,
        float auraRadius,
        float ammoInteractRange,
        float ammoCooldown,
        float extraAmmoPercent,
        KeyCode ammoInteractKey,
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
        interactDistance = Mathf.Max(1f, ammoInteractRange);
        ammoInteractCooldown = Mathf.Max(0.1f, ammoCooldown);
        additionalAmmoPercent = Mathf.Clamp01(extraAmmoPercent);
        interactKey = ammoInteractKey;
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

        HandleAmmoInteraction();
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

    private void HandleAmmoInteraction()
    {
        if (Time.time < nextAmmoInteractTime || !Input.GetKeyDown(interactKey))
        {
            return;
        }

        BaseCharacter[] characters = FindObjectsByType<BaseCharacter>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            BaseCharacter character = characters[i];
            if (character == null)
            {
                continue;
            }

            if (Vector3.Distance(transform.position, character.transform.position) > interactDistance)
            {
                continue;
            }

            if (TryGrantAmmo(character))
            {
                nextAmmoInteractTime = Time.time + ammoInteractCooldown;
                break;
            }
        }
    }

    private bool TryGrantAmmo(BaseCharacter character)
    {
        if (character == null || additionalAmmoPercent <= 0f)
        {
            return false;
        }

        WeaponAbility[] weapons = character.GetComponents<WeaponAbility>();
        bool grantedAnyAmmo = false;
        for (int i = 0; i < weapons.Length; i++)
        {
            weapons[i].GrantAdditionalAmmoPercent(additionalAmmoPercent);
            grantedAnyAmmo = true;
        }

        return grantedAnyAmmo;
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

        string ammoLine = AmmoCooldownRemaining > 0f
            ? "Ammo cooldown " + AmmoCooldownRemaining.ToString("0.0s")
            : "Press " + interactKey + " for ammo";
        worldLabel.text = "DADA KA AADMI\n" + ammoLine + "\n" + RemainingLifetime.ToString("0.0s");
    }
}
