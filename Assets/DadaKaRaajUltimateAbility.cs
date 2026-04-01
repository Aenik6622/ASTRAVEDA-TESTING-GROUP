using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Abilities/Dada Ka Raaj Ultimate")]
[DisallowMultipleComponent]
public class DadaKaRaajUltimateAbility : Ability
{
    private enum ControlMode
    {
        Drive,
        Minigun
    }

    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Camera playerCamera;

    [Header("Input")]
    [SerializeField] private KeyCode activateKey = KeyCode.Q;
    [SerializeField] private KeyCode driveModeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode minigunModeKey = KeyCode.Alpha2;

    [Header("Ultimate")]
    [SerializeField] private float ultimateDuration = 8f;
    [SerializeField] private float jeepSpawnForwardOffset = 1.8f;

    [Header("Jeep Driving")]
    [SerializeField] private float jeepMoveSpeed = 14f;
    [SerializeField] private float jeepReverseSpeed = 8f;
    [SerializeField] private float jeepTurnSpeed = 95f;
    [SerializeField] private float jeepAcceleration = 11f;

    [Header("Minigun")]
    [SerializeField] private float minigunDamagePerShot = 11f;
    [SerializeField] private float minigunFireRate = 18f;
    [SerializeField] private float minigunRange = 140f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private Color tracerColor = new Color(1f, 0.82f, 0.22f, 1f);
    [SerializeField] private float tracerLifetime = 0.08f;

    private WeaponLoadout weaponLoadout;
    private Transform cachedParent;
    private GameObject jeepRoot;
    private Transform seatAnchor;
    private Transform turretPivot;
    private Transform muzzleAnchor;
    private float activeUntilTime;
    private float nextMinigunShotTime;
    private float currentDriveSpeed;
    private bool isUltimateActive;
    private ControlMode controlMode = ControlMode.Drive;

    private Canvas hudCanvas;
    private Image hudChargeFill;
    private Text hudTitle;
    private Text hudModeText;
    private Text hudHintText;
    private Image driveModePanel;
    private Image minigunModePanel;
    private Text driveModeLabel;
    private Text minigunModeLabel;

    public bool IsMovementOverridden => isUltimateActive;
    public override string AbilityDisplayName => "Dada ka Raaj";
    public override string AbilityBindingLabel => "Q | 1 Drive | 2 Gun";
    public override string AbilityHudExtra => owner == null ? string.Empty : "Charge " + Mathf.RoundToInt(GetChargePercent()) + "%";
    public override string AbilityStatusText
    {
        get
        {
            if (isUltimateActive)
            {
                float timeLeft = Mathf.Max(0f, activeUntilTime - Time.time);
                return controlMode == ControlMode.Drive
                    ? "DRIVE MODE " + timeLeft.ToString("0.0s")
                    : "MINIGUN MODE " + timeLeft.ToString("0.0s");
            }

            return owner != null && owner.HasFullUltimateCharge()
                ? "ULT READY"
                : "CHARGING " + Mathf.RoundToInt(GetChargePercent()) + "%";
        }
    }

    public override Color AbilityStatusColor
    {
        get
        {
            if (isUltimateActive)
            {
                return controlMode == ControlMode.Drive
                    ? new Color(0.62f, 0.94f, 1f)
                    : new Color(1f, 0.88f, 0.38f);
            }

            return owner != null && owner.HasFullUltimateCharge()
                ? new Color(1f, 0.9f, 0.35f)
                : new Color(0.74f, 0.8f, 0.92f);
        }
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponent<BaseCharacter>();
        }

        if (controller == null)
        {
            controller = GetComponent<CharacterController>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        weaponLoadout = GetComponent<WeaponLoadout>();
        cooldown = 0f;
        AbilityHudOverlay.EnsureFor(gameObject);
        EnsureHud();
    }

    private void Update()
    {
        if (Input.GetKeyDown(activateKey) && !isUltimateActive)
        {
            TryUse();
        }

        if (!isUltimateActive)
        {
            UpdateHud(false);
            return;
        }

        if (Time.time >= activeUntilTime)
        {
            EndUltimate();
            UpdateHud(false);
            return;
        }

        if (Input.GetKeyDown(driveModeKey))
        {
            controlMode = ControlMode.Drive;
        }

        if (Input.GetKeyDown(minigunModeKey))
        {
            controlMode = ControlMode.Minigun;
        }

        UpdateMountedPlayerPose();

        if (controlMode == ControlMode.Drive)
        {
            UpdateDriveMode();
        }
        else
        {
            UpdateMinigunMode();
        }

        UpdateHud(true);
    }

    public override bool CanUse()
    {
        return !isUltimateActive && owner != null && owner.HasFullUltimateCharge();
    }

    protected override void Activate()
    {
        if (owner == null || !owner.TrySpendUltimateCharge(owner.maxUltimateCharge))
        {
            return;
        }

        SpawnJeep();
        MountPlayer();
        weaponLoadout?.SetLoadoutLocked(true);
        currentDriveSpeed = 0f;
        nextMinigunShotTime = 0f;
        controlMode = ControlMode.Drive;
        isUltimateActive = true;
        activeUntilTime = Time.time + Mathf.Max(1f, ultimateDuration);
        UpdateHud(true);
    }

    private void SpawnJeep()
    {
        if (jeepRoot != null)
        {
            Destroy(jeepRoot);
        }

        Vector3 spawnPosition = transform.position + (transform.forward * jeepSpawnForwardOffset);
        spawnPosition.y = Mathf.Max(0.6f, transform.position.y + 0.15f);

        jeepRoot = new GameObject("DadaKaRaajJeep");
        jeepRoot.transform.position = spawnPosition;
        jeepRoot.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        GameObject chassis = CreatePrimitivePart("Chassis", PrimitiveType.Cube, new Vector3(0f, 0.9f, 0f), new Vector3(2.8f, 0.8f, 4.6f), new Color(0.1f, 0.16f, 0.13f), jeepRoot.transform);
        CreatePrimitivePart("Cabin", PrimitiveType.Cube, new Vector3(0f, 1.65f, -0.25f), new Vector3(2.2f, 1.1f, 2.2f), new Color(0.18f, 0.24f, 0.2f), jeepRoot.transform);
        CreatePrimitivePart("Hood", PrimitiveType.Cube, new Vector3(0f, 1.25f, 1.45f), new Vector3(2.3f, 0.45f, 1.4f), new Color(0.08f, 0.11f, 0.09f), jeepRoot.transform);
        CreatePrimitivePart("BullBar", PrimitiveType.Cube, new Vector3(0f, 0.95f, 2.5f), new Vector3(2.45f, 0.35f, 0.2f), new Color(0.32f, 0.34f, 0.33f), jeepRoot.transform);

        Vector3[] wheelOffsets =
        {
            new Vector3(-1.45f, 0.45f, 1.7f),
            new Vector3(1.45f, 0.45f, 1.7f),
            new Vector3(-1.45f, 0.45f, -1.7f),
            new Vector3(1.45f, 0.45f, -1.7f),
        };

        for (int i = 0; i < wheelOffsets.Length; i++)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel_" + i;
            wheel.transform.SetParent(jeepRoot.transform, false);
            wheel.transform.localPosition = wheelOffsets[i];
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.65f, 0.22f, 0.65f);
            ApplyPartMaterial(wheel, new Color(0.08f, 0.08f, 0.08f));
        }

        seatAnchor = new GameObject("SeatAnchor").transform;
        seatAnchor.SetParent(jeepRoot.transform, false);
        seatAnchor.localPosition = new Vector3(0f, 1.85f, -0.35f);

        turretPivot = new GameObject("TurretPivot").transform;
        turretPivot.SetParent(jeepRoot.transform, false);
        turretPivot.localPosition = new Vector3(0f, 2.2f, -0.2f);

        GameObject turretBase = CreatePrimitivePart("TurretBase", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.95f, 0.18f, 0.95f), new Color(0.2f, 0.22f, 0.24f), turretPivot);
        turretBase.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Transform gunRoot = new GameObject("GunRoot").transform;
        gunRoot.SetParent(turretPivot, false);
        gunRoot.localPosition = new Vector3(0f, 0.28f, 0.4f);

        CreatePrimitivePart("GunHousing", PrimitiveType.Cube, new Vector3(0f, 0f, 0.35f), new Vector3(0.6f, 0.35f, 1.3f), new Color(0.14f, 0.15f, 0.16f), gunRoot);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.18f + (i * 0.12f);
            CreatePrimitivePart("Barrel_" + i, PrimitiveType.Cylinder, new Vector3(x, 0f, 1.1f), new Vector3(0.06f, 0.5f, 0.06f), new Color(0.25f, 0.26f, 0.28f), gunRoot).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        muzzleAnchor = new GameObject("Muzzle").transform;
        muzzleAnchor.SetParent(gunRoot, false);
        muzzleAnchor.localPosition = new Vector3(0f, 0f, 1.65f);

        BoxCollider bodyCollider = chassis.GetComponent<BoxCollider>();
        if (bodyCollider != null)
        {
            bodyCollider.size = new Vector3(1f, 1f, 1f);
        }
    }

    private void MountPlayer()
    {
        if (controller != null)
        {
            controller.enabled = false;
        }

        cachedParent = transform.parent;
        transform.SetParent(seatAnchor, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    private void UpdateMountedPlayerPose()
    {
        if (seatAnchor == null)
        {
            return;
        }

        transform.position = seatAnchor.position;
    }

    private void UpdateDriveMode()
    {
        if (jeepRoot == null)
        {
            return;
        }

        float steer = Input.GetAxisRaw("Horizontal");
        float throttle = Input.GetAxisRaw("Vertical");

        float targetSpeed = throttle > 0f ? throttle * jeepMoveSpeed : throttle * jeepReverseSpeed;
        currentDriveSpeed = Mathf.MoveTowards(currentDriveSpeed, targetSpeed, jeepAcceleration * Time.deltaTime);

        jeepRoot.transform.Rotate(Vector3.up, steer * jeepTurnSpeed * Time.deltaTime, Space.World);
        jeepRoot.transform.position += jeepRoot.transform.forward * currentDriveSpeed * Time.deltaTime;

        if (turretPivot != null)
        {
            Quaternion settleRotation = Quaternion.Euler(0f, 0f, 0f);
            turretPivot.localRotation = Quaternion.Slerp(turretPivot.localRotation, settleRotation, 8f * Time.deltaTime);
        }
    }

    private void UpdateMinigunMode()
    {
        if (turretPivot == null || playerCamera == null)
        {
            return;
        }

        Vector3 aimDirection = playerCamera.transform.forward;
        Vector3 flatAim = aimDirection;
        flatAim.y = 0f;
        if (flatAim.sqrMagnitude <= 0.001f)
        {
            flatAim = jeepRoot.transform.forward;
        }

        Quaternion yawRotation = Quaternion.LookRotation(flatAim.normalized, Vector3.up);
        jeepRoot.transform.rotation = Quaternion.Slerp(jeepRoot.transform.rotation, yawRotation, 5f * Time.deltaTime);

        Vector3 localAim = turretPivot.InverseTransformDirection(aimDirection.normalized);
        float pitch = Mathf.Atan2(localAim.y, localAim.z) * Mathf.Rad2Deg;
        turretPivot.localRotation = Quaternion.Euler(Mathf.Clamp(-pitch, -10f, 35f), 0f, 0f);

        if (!Input.GetMouseButton(0) || Time.time < nextMinigunShotTime)
        {
            return;
        }

        nextMinigunShotTime = Time.time + (1f / Mathf.Max(1f, minigunFireRate));
        FireMinigunShot();
    }

    private void FireMinigunShot()
    {
        if (playerCamera == null)
        {
            return;
        }

        Vector3 origin = muzzleAnchor != null ? muzzleAnchor.position : playerCamera.transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Vector3 impactPoint = origin + (direction * minigunRange);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, minigunRange, hitMask, QueryTriggerInteraction.Ignore))
        {
            impactPoint = hit.point;

            BotBehaviour bot = hit.collider.GetComponentInParent<BotBehaviour>();
            if (bot != null && bot.Team != BotBehaviour.BotTeam.Ally)
            {
                bot.TakeDamage(minigunDamagePerShot);
                owner?.RegisterDamageDealt(minigunDamagePerShot);
            }
            else
            {
                BaseCharacter targetCharacter = hit.collider.GetComponentInParent<BaseCharacter>();
                if (targetCharacter != null && targetCharacter.transform.root != transform.root)
                {
                    targetCharacter.TakeDamage(minigunDamagePerShot);
                    owner?.RegisterDamageDealt(minigunDamagePerShot);
                }
            }
        }

        SpawnTracer(origin, impactPoint);
    }

    private void SpawnTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tracer.name = "MinigunTracer";
        Destroy(tracer.GetComponent<Collider>());
        tracer.transform.position = Vector3.Lerp(start, end, 0.5f);
        tracer.transform.rotation = Quaternion.LookRotation(end - start);
        float distance = Vector3.Distance(start, end);
        tracer.transform.localScale = new Vector3(0.04f, 0.04f, Mathf.Max(0.4f, distance));
        ApplyPartMaterial(tracer, tracerColor);
        Destroy(tracer, tracerLifetime);
    }

    private void EndUltimate()
    {
        isUltimateActive = false;
        currentDriveSpeed = 0f;

        if (transform != null)
        {
            transform.SetParent(cachedParent, true);
        }

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (jeepRoot != null)
        {
            transform.position = jeepRoot.transform.position + (jeepRoot.transform.right * 2.2f);
            Destroy(jeepRoot);
        }

        jeepRoot = null;
        seatAnchor = null;
        turretPivot = null;
        muzzleAnchor = null;
        weaponLoadout?.SetLoadoutLocked(false);
    }

    private GameObject CreatePrimitivePart(string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color, Transform parent)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        ApplyPartMaterial(part, color);
        return part;
    }

    private void ApplyPartMaterial(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader preferredShader = Shader.Find("Universal Render Pipeline/Lit");
        if (preferredShader == null)
        {
            preferredShader = Shader.Find("Standard");
        }

        if (preferredShader == null)
        {
            return;
        }

        Material material = new Material(preferredShader);
        material.color = color;
        renderer.material = material;
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
        GameObject canvasObject = new GameObject("DadaKaRaajHUD");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 1250;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject root = CreateUiPanel("UltimatePanel", hudCanvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(580f, 154f), new Color(0.05f, 0.08f, 0.1f, 0.9f));
        hudTitle = CreateUiText("Title", root.transform, font, 24, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(520f, 28f));
        hudModeText = CreateUiText("Mode", root.transform, font, 20, TextAnchor.UpperCenter, new Color(0.86f, 0.95f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(500f, 24f));
        hudHintText = CreateUiText("Hint", root.transform, font, 16, TextAnchor.UpperCenter, new Color(0.72f, 0.78f, 0.88f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(520f, 20f));

        GameObject chargeBack = CreateUiPanel("ChargeBack", root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(500f, 18f), new Color(0.14f, 0.18f, 0.22f, 0.95f));
        GameObject chargeFill = CreateUiPanel("ChargeFill", chargeBack.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f), new Color(1f, 0.78f, 0.2f, 1f));
        hudChargeFill = chargeFill.GetComponent<Image>();

        driveModePanel = CreateUiPanel("DriveMode", root.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(250f, 42f), new Color(0.15f, 0.19f, 0.24f, 0.9f)).GetComponent<Image>();
        minigunModePanel = CreateUiPanel("MinigunMode", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 14f), new Vector2(250f, 42f), new Color(0.15f, 0.19f, 0.24f, 0.9f)).GetComponent<Image>();
        driveModeLabel = CreateUiText("DriveLabel", driveModePanel.transform, font, 18, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        minigunModeLabel = CreateUiText("GunLabel", minigunModePanel.transform, font, 18, TextAnchor.MiddleCenter, Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        UpdateHud(false);
    }

    private void UpdateHud(bool visible)
    {
        if (hudCanvas == null)
        {
            return;
        }

        float chargeNormalized = owner != null && owner.maxUltimateCharge > 0f
            ? Mathf.Clamp01(owner.ultimateCharge / owner.maxUltimateCharge)
            : 0f;

        hudCanvas.enabled = true;
        hudTitle.text = "DADA KA RAAJ";
        hudModeText.text = visible
            ? "Jeep online  |  " + (controlMode == ControlMode.Drive ? "Drive Control" : "Top Minigun Control")
            : "Ultimate charge: " + Mathf.RoundToInt(chargeNormalized * 100f) + "%";
        hudHintText.text = visible
            ? "Press 1 to drive, 2 to take the minigun, hold LMB to shred"
            : "Press Q at 100% charge to summon the jeep and mount the roof gun";

        if (hudChargeFill != null)
        {
            RectTransform fillRect = hudChargeFill.rectTransform;
            fillRect.sizeDelta = new Vector2(500f * (visible ? Mathf.Clamp01((activeUntilTime - Time.time) / Mathf.Max(1f, ultimateDuration)) : chargeNormalized), 0f);
            hudChargeFill.color = visible ? new Color(1f, 0.44f, 0.2f, 1f) : new Color(1f, 0.78f, 0.2f, 1f);
        }

        Color activeColor = new Color(0.95f, 0.64f, 0.18f, 0.96f);
        Color idleColor = new Color(0.15f, 0.19f, 0.24f, 0.9f);
        if (driveModePanel != null)
        {
            driveModePanel.color = visible && controlMode == ControlMode.Drive ? activeColor : idleColor;
        }

        if (minigunModePanel != null)
        {
            minigunModePanel.color = visible && controlMode == ControlMode.Minigun ? activeColor : idleColor;
        }

        if (driveModeLabel != null)
        {
            driveModeLabel.text = "1  DRIVE JEEP";
        }

        if (minigunModeLabel != null)
        {
            minigunModeLabel.text = "2  TOP MINIGUN";
        }
    }

    private GameObject CreateUiPanel(string panelName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject panelObject = new GameObject(panelName);
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return panelObject;
    }

    private Text CreateUiText(string textName, Transform parent, Font font, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject textObject = new GameObject(textName);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return text;
    }
}
