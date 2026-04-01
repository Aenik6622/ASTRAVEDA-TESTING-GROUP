using UnityEngine;

[AddComponentMenu("Abilities/Desi Katta Shotgun Ability")]
[DisallowMultipleComponent]
public class DesiKattaShotgunAbility : WeaponAbility
{
    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private Transform fireOrigin;
    [SerializeField] private Transform aimOrigin;

    [Header("Input")]
    [SerializeField] private int activationMouseButton = 0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Shotgun")]
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float damagePerPellet = 18f;
    [SerializeField] private float range = 28f;
    [SerializeField] private float spreadAngle = 9f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool damageOwner;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 1;
    [SerializeField] private int startingReserveAmmo = 15;
    [SerializeField] private int maxReserveAmmo = 15;
    [SerializeField] private float reloadDuration = 0.9f;

    [Header("View Model")]
    [SerializeField] private bool createViewModel = true;
    [SerializeField] private Vector3 weaponLocalPosition = new Vector3(0.34f, -0.28f, 0.72f);
    [SerializeField] private Vector3 weaponLocalEuler = new Vector3(8f, 196f, 2f);
    [SerializeField] private Vector3 fireKickPosition = new Vector3(-0.04f, 0.03f, -0.18f);
    [SerializeField] private Vector3 fireKickEuler = new Vector3(-16f, 0f, 6f);
    [SerializeField] private Vector3 reloadPositionOffset = new Vector3(0.02f, -0.12f, -0.08f);
    [SerializeField] private Vector3 reloadEulerOffset = new Vector3(18f, -20f, 14f);
    [SerializeField] private float swayAmount = 0.018f;
    [SerializeField] private float swayRotation = 4f;
    [SerializeField] private float animationSharpness = 12f;

    [Header("Pellet Visuals")]
    [SerializeField] private bool showPelletModels = true;
    [SerializeField] private float pelletVisualSpeed = 65f;
    [SerializeField] private float pelletVisualLifetime = 0.12f;
    [SerializeField] private Vector3 pelletVisualScale = new Vector3(0.022f, 0.022f, 0.18f);
    [SerializeField] private Color pelletVisualColor = new Color(1f, 0.78f, 0.35f);

    private Camera cachedCamera;
    private int ammoInMagazine;
    private int reserveAmmo;
    private bool isReloading;
    private float reloadFinishTime;
    private Transform viewModelRoot;
    private Transform muzzleAnchor;
    private Vector3 baseViewModelLocalPosition;
    private Quaternion baseViewModelLocalRotation;
    private Vector3 animationPositionOffset;
    private Vector3 animationEulerOffset;

    public override string AbilityDisplayName => "Desi Katta";
    public override string AbilityHudExtra => "Ammo " + ammoInMagazine + "/" + reserveAmmo + "  |  Spread " + spreadAngle.ToString("0.#") + " deg";
    public override string AbilityHudIconPath => @"C:/Users/Admin/Pictures/UIicons.jpeg";
    public override string AbilityStatusText => isReloading ? "RELOADING" : ammoInMagazine <= 0 && reserveAmmo <= 0 ? "EMPTY" : base.AbilityStatusText;
    public override Color AbilityStatusColor => isReloading ? new Color(0.65f, 0.88f, 1f) : ammoInMagazine <= 0 && reserveAmmo <= 0 ? Color.gray : base.AbilityStatusColor;

    private void Awake()
    {
        InitializeWeaponSlot(1, "2 | LMB");

        if (owner == null)
        {
            owner = GetComponent<BaseCharacter>();
        }

        if (fireOrigin == null)
        {
            fireOrigin = transform;
        }

        if (aimOrigin == null)
        {
            aimOrigin = fireOrigin;
        }

        cooldown = cooldown <= 0f ? 0.45f : cooldown;
        magazineSize = Mathf.Max(1, magazineSize);
        maxReserveAmmo = Mathf.Max(0, maxReserveAmmo);
        reserveAmmo = Mathf.Clamp(startingReserveAmmo, 0, maxReserveAmmo);
        ammoInMagazine = magazineSize;

        AbilityHudOverlay.EnsureFor(gameObject);
        SetupViewModel();
    }

    private void Update()
    {
        UpdateViewModelAnimation();

        if (isReloading && Time.time >= reloadFinishTime)
        {
            FinishReload();
        }

        if (Input.GetKeyDown(reloadKey))
        {
            StartReload();
        }

        if (!IsEquipped)
        {
            return;
        }

        if (!Input.GetMouseButtonDown(activationMouseButton))
        {
            return;
        }

        if (ammoInMagazine <= 0)
        {
            StartReload();
            return;
        }

        TryUse();

        if (ammoInMagazine <= 0 && reserveAmmo > 0)
        {
            StartReload();
        }
    }

    public override bool CanUse()
    {
        return IsEquipped && !isReloading && ammoInMagazine > 0 && base.CanUse();
    }

    protected override void OnEquippedChanged(bool equipped)
    {
        if (viewModelRoot != null)
        {
            viewModelRoot.gameObject.SetActive(equipped);
        }
    }

    protected override void Activate()
    {
        ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);
        animationPositionOffset += fireKickPosition;
        animationEulerOffset += fireKickEuler;

        Vector3 origin = fireOrigin != null ? fireOrigin.position : transform.position;
        Camera aimCamera = cachedCamera != null ? cachedCamera : Camera.main;
        cachedCamera = aimCamera;

        Vector3 baseDirection = GetAimDirection(origin, aimCamera);
        Vector3 spreadRight = aimCamera != null ? aimCamera.transform.right : aimOrigin.right;
        Vector3 spreadUp = aimCamera != null ? aimCamera.transform.up : aimOrigin.up;

        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector2 spreadOffset = Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
            Vector3 pelletDirection = (baseDirection + (spreadRight * spreadOffset.x) + (spreadUp * spreadOffset.y)).normalized;
            Vector3 pelletStart = muzzleAnchor != null ? muzzleAnchor.position : origin;
            Vector3 pelletEnd = pelletStart + (pelletDirection * range);

            if (Physics.Raycast(origin, pelletDirection, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                pelletEnd = hit.point;
                TryDamage(hit.collider.gameObject, damagePerPellet);
            }

            SpawnPelletVisual(pelletStart, pelletEnd);
        }
    }

    private void StartReload()
    {
        if (isReloading || reserveAmmo <= 0 || ammoInMagazine >= magazineSize)
        {
            return;
        }

        isReloading = true;
        reloadFinishTime = Time.time + reloadDuration;
    }

    private void FinishReload()
    {
        isReloading = false;
        animationPositionOffset += reloadPositionOffset * 0.35f;
        animationEulerOffset += reloadEulerOffset * 0.2f;

        int ammoNeeded = magazineSize - ammoInMagazine;
        if (ammoNeeded <= 0)
        {
            return;
        }

        int ammoToLoad = Mathf.Min(ammoNeeded, reserveAmmo);
        ammoInMagazine += ammoToLoad;
        reserveAmmo -= ammoToLoad;
    }

    public override void GrantAdditionalAmmoPercent(float percent)
    {
        if (percent <= 0f)
        {
            return;
        }

        int bonusAmmo = Mathf.RoundToInt(maxReserveAmmo * percent);
        reserveAmmo = Mathf.Clamp(reserveAmmo + Mathf.Max(1, bonusAmmo), 0, maxReserveAmmo);
    }

    private Vector3 GetAimDirection(Vector3 origin, Camera aimCamera)
    {
        if (aimCamera != null)
        {
            Ray aimRay = aimCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            Vector3 targetPoint = aimRay.origin + (aimRay.direction * range);

            if (Physics.Raycast(aimRay, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }

            return (targetPoint - origin).normalized;
        }

        Transform fallbackAim = aimOrigin != null ? aimOrigin : transform;
        return fallbackAim.forward;
    }

    private void TryDamage(GameObject targetObject, float damage)
    {
        if (damage <= 0f || targetObject == null)
        {
            return;
        }

        BotBehaviour bot = targetObject.GetComponentInParent<BotBehaviour>();
        if (bot != null)
        {
            if (bot.Team == BotBehaviour.BotTeam.Ally)
            {
                return;
            }

            if (!damageOwner && owner != null && bot.transform.root == owner.transform.root)
            {
                return;
            }

            bot.TakeDamage(damage);
            if (owner != null)
            {
                owner.RegisterDamageDealt(damage);
            }
            return;
        }

        BaseCharacter character = targetObject.GetComponentInParent<BaseCharacter>();
        if (character == null)
        {
            return;
        }

        if (!damageOwner && owner != null && character.transform.root == owner.transform.root)
        {
            return;
        }

        character.TakeDamage(damage);
        if (owner != null)
        {
            owner.RegisterDamageDealt(damage);
        }
    }

    private void SetupViewModel()
    {
        if (!createViewModel)
        {
            return;
        }

        Transform parent = aimOrigin != null ? aimOrigin : transform;

        GameObject rootObject = new GameObject("DesiKattaViewModel");
        viewModelRoot = rootObject.transform;
        viewModelRoot.SetParent(parent, false);
        baseViewModelLocalPosition = weaponLocalPosition;
        baseViewModelLocalRotation = Quaternion.Euler(weaponLocalEuler);
        viewModelRoot.localPosition = baseViewModelLocalPosition;
        viewModelRoot.localRotation = baseViewModelLocalRotation;

        CreatePart(
            "Receiver",
            PrimitiveType.Cube,
            viewModelRoot,
            new Vector3(-0.02f, 0.01f, 0.08f),
            new Vector3(0.16f, 0.12f, 0.38f),
            Vector3.zero,
            new Color(0.18f, 0.18f, 0.2f));

        CreatePart(
            "Barrel",
            PrimitiveType.Cylinder,
            viewModelRoot,
            new Vector3(-0.01f, 0.015f, 0.34f),
            new Vector3(0.035f, 0.34f, 0.035f),
            new Vector3(90f, 0f, 0f),
            new Color(0.08f, 0.08f, 0.09f));

        CreatePart(
            "Stock",
            PrimitiveType.Cube,
            viewModelRoot,
            new Vector3(0.02f, -0.015f, -0.14f),
            new Vector3(0.11f, 0.09f, 0.3f),
            new Vector3(10f, 0f, -8f),
            new Color(0.28f, 0.16f, 0.08f));

        CreatePart(
            "Grip",
            PrimitiveType.Cube,
            viewModelRoot,
            new Vector3(0.055f, -0.1f, -0.005f),
            new Vector3(0.08f, 0.18f, 0.08f),
            new Vector3(20f, 0f, -12f),
            new Color(0.23f, 0.12f, 0.05f));

        CreatePart(
            "Foregrip",
            PrimitiveType.Cube,
            viewModelRoot,
            new Vector3(0.035f, -0.08f, 0.17f),
            new Vector3(0.075f, 0.12f, 0.08f),
            new Vector3(16f, 0f, -8f),
            new Color(0.24f, 0.14f, 0.06f));

        GameObject muzzleObject = new GameObject("Muzzle");
        muzzleAnchor = muzzleObject.transform;
        muzzleAnchor.SetParent(viewModelRoot, false);
        muzzleAnchor.localPosition = new Vector3(-0.01f, 0.015f, 0.68f);
        muzzleAnchor.localRotation = Quaternion.identity;
    }

    private void UpdateViewModelAnimation()
    {
        if (viewModelRoot == null)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        Vector3 swayPosition = new Vector3(-mouseX, -mouseY, 0f) * swayAmount;
        Vector3 swayEuler = new Vector3(mouseY, -mouseX, -mouseX) * swayRotation;

        if (isReloading)
        {
            float reloadProgress = 1f - Mathf.Clamp01((reloadFinishTime - Time.time) / Mathf.Max(0.01f, reloadDuration));
            float reloadWave = Mathf.Sin(reloadProgress * Mathf.PI);
            animationPositionOffset = Vector3.Lerp(animationPositionOffset, reloadPositionOffset * reloadWave, Time.deltaTime * animationSharpness);
            animationEulerOffset = Vector3.Lerp(animationEulerOffset, reloadEulerOffset * reloadWave, Time.deltaTime * animationSharpness);
        }
        else
        {
            animationPositionOffset = Vector3.Lerp(animationPositionOffset, Vector3.zero, Time.deltaTime * animationSharpness);
            animationEulerOffset = Vector3.Lerp(animationEulerOffset, Vector3.zero, Time.deltaTime * animationSharpness);
        }

        viewModelRoot.localPosition = Vector3.Lerp(
            viewModelRoot.localPosition,
            baseViewModelLocalPosition + swayPosition + animationPositionOffset,
            Time.deltaTime * animationSharpness);

        Quaternion targetRotation = baseViewModelLocalRotation * Quaternion.Euler(swayEuler + animationEulerOffset);
        viewModelRoot.localRotation = Quaternion.Slerp(viewModelRoot.localRotation, targetRotation, Time.deltaTime * animationSharpness);
    }

    private void CreatePart(string partName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.transform.localRotation = Quaternion.Euler(localEuler);

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateTintedMaterial(color, color * 0.4f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static Material CreateTintedMaterial(Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
        }

        return material;
    }

    private void SpawnPelletVisual(Vector3 start, Vector3 end)
    {
        if (!showPelletModels)
        {
            return;
        }

        Vector3 travel = end - start;
        float distance = travel.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        GameObject pelletObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pelletObject.name = "Shotgun Pellet Visual";
        pelletObject.transform.position = start;
        pelletObject.transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        pelletObject.transform.localScale = pelletVisualScale;

        Collider pelletCollider = pelletObject.GetComponent<Collider>();
        if (pelletCollider != null)
        {
            Destroy(pelletCollider);
        }

        Renderer pelletRenderer = pelletObject.GetComponent<Renderer>();
        if (pelletRenderer != null)
        {
            pelletRenderer.material = CreateTintedMaterial(pelletVisualColor, pelletVisualColor * 1.2f);
            pelletRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pelletRenderer.receiveShadows = false;
        }

        PelletVisual pelletVisual = pelletObject.AddComponent<PelletVisual>();
        pelletVisual.Initialize(start, end, pelletVisualSpeed, pelletVisualLifetime);
    }

    private sealed class PelletVisual : MonoBehaviour
    {
        private Vector3 startPoint;
        private Vector3 endPoint;
        private float moveSpeed;
        private float lifetime;
        private float elapsed;

        public void Initialize(Vector3 start, Vector3 end, float speed, float maxLifetime)
        {
            startPoint = start;
            endPoint = end;
            moveSpeed = Mathf.Max(0.01f, speed);
            lifetime = Mathf.Max(0.01f, maxLifetime);
            transform.position = startPoint;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            Vector3 direction = endPoint - startPoint;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            float normalizedProgress = Mathf.Clamp01((elapsed * moveSpeed) / distance);
            transform.position = Vector3.Lerp(startPoint, endPoint, normalizedProgress);

            if (normalizedProgress >= 1f || elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
