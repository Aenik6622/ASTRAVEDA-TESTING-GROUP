using UnityEngine;

[AddComponentMenu("Abilities/Biotic Rifle Ability")]
[DisallowMultipleComponent]
public class BioticRifleAbility : WeaponAbility
{
    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private Transform fireOrigin;
    [SerializeField] private Transform aimOrigin;

    [Header("Input")]
    [SerializeField] private int activationMouseButton = 0;
    [SerializeField] private KeyCode reloadKey = KeyCode.R;

    [Header("Rifle")]
    [SerializeField] private float damagePerShot = 16f;
    [SerializeField] private float range = 75f;
    [SerializeField] private float spreadAngle = 1.2f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool damageOwner;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 24;
    [SerializeField] private int startingReserveAmmo = 120;
    [SerializeField] private int maxReserveAmmo = 120;
    [SerializeField] private float reloadDuration = 1.2f;

    [Header("Anti Heal")]
    [SerializeField, Range(0f, 1f)] private float antiHealPercentage = 0.5f;
    [SerializeField] private float antiHealDuration = 3.5f;

    [Header("View Model")]
    [SerializeField] private bool createViewModel = true;
    [SerializeField] private Vector3 weaponLocalPosition = new Vector3(0.28f, -0.22f, 0.9f);
    [SerializeField] private Vector3 weaponLocalEuler = new Vector3(4f, 186f, 1f);
    [SerializeField] private Vector3 fireKickPosition = new Vector3(0f, 0.01f, -0.09f);
    [SerializeField] private Vector3 fireKickEuler = new Vector3(-5f, 0f, 1.5f);
    [SerializeField] private Vector3 reloadPositionOffset = new Vector3(-0.03f, -0.09f, -0.1f);
    [SerializeField] private Vector3 reloadEulerOffset = new Vector3(10f, 10f, 8f);
    [SerializeField] private float swayAmount = 0.012f;
    [SerializeField] private float swayRotation = 2.5f;
    [SerializeField] private float animationSharpness = 14f;

    [Header("Shot Visuals")]
    [SerializeField] private Color tracerColor = new Color(0.45f, 1f, 0.82f);
    [SerializeField] private float tracerSpeed = 95f;
    [SerializeField] private float tracerLifetime = 0.08f;
    [SerializeField] private Vector3 tracerScale = new Vector3(0.016f, 0.016f, 0.24f);

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

    public override string AbilityDisplayName => "Biotic Rifle";
    public override string AbilityHudExtra => "Ammo " + ammoInMagazine + "/" + reserveAmmo + "  |  Anti-Heal " + Mathf.RoundToInt(antiHealPercentage * 100f) + "% HS";
    public override string AbilityHudIconPath => @"C:/Users/Admin/Pictures/rifle.jpeg";
    public override string AbilityStatusText => isReloading ? "RELOADING" : ammoInMagazine <= 0 && reserveAmmo <= 0 ? "EMPTY" : base.AbilityStatusText;
    public override Color AbilityStatusColor => isReloading ? new Color(0.65f, 0.88f, 1f) : ammoInMagazine <= 0 && reserveAmmo <= 0 ? Color.gray : base.AbilityStatusColor;

    private void Awake()
    {
        InitializeWeaponSlot(0, "1 | LMB");

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

        cooldown = cooldown <= 0f ? 0.11f : cooldown;
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

        if (!IsEquipped)
        {
            return;
        }

        if (Input.GetKeyDown(reloadKey))
        {
            StartReload();
        }

        if (!Input.GetMouseButton(activationMouseButton))
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

    protected override void Activate()
    {
        ammoInMagazine = Mathf.Max(0, ammoInMagazine - 1);
        animationPositionOffset += fireKickPosition;
        animationEulerOffset += fireKickEuler;

        Vector3 origin = fireOrigin != null ? fireOrigin.position : transform.position;
        Camera aimCamera = cachedCamera != null ? cachedCamera : Camera.main;
        cachedCamera = aimCamera;
        Vector3 direction = GetAimDirection(origin, aimCamera);
        Vector3 start = muzzleAnchor != null ? muzzleAnchor.position : origin;
        Vector3 end = start + (direction * range);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            TryDamage(hit);
        }

        SpawnTracerVisual(start, end);
    }

    protected override void OnEquippedChanged(bool equipped)
    {
        if (viewModelRoot != null)
        {
            viewModelRoot.gameObject.SetActive(equipped);
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
        Vector3 baseDirection;
        if (aimCamera != null)
        {
            Ray aimRay = aimCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            Vector3 targetPoint = aimRay.origin + (aimRay.direction * range);
            if (Physics.Raycast(aimRay, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                targetPoint = hit.point;
            }

            baseDirection = (targetPoint - origin).normalized;
        }
        else
        {
            Transform fallbackAim = aimOrigin != null ? aimOrigin : transform;
            baseDirection = fallbackAim.forward;
        }

        Vector2 spread = Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector3 right = aimCamera != null ? aimCamera.transform.right : aimOrigin.right;
        Vector3 up = aimCamera != null ? aimCamera.transform.up : aimOrigin.up;
        return (baseDirection + (right * spread.x) + (up * spread.y)).normalized;
    }

    private void TryDamage(RaycastHit hit)
    {
        GameObject targetObject = hit.collider != null ? hit.collider.gameObject : null;
        if (targetObject == null)
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

            bot.TakeDamage(damagePerShot);
            if (owner != null)
            {
                owner.RegisterDamageDealt(damagePerShot);
            }
            if (IsHeadshot(hit, bot.transform))
            {
                ApplyAntiHeal(bot.gameObject);
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

        character.TakeDamage(damagePerShot);
        if (owner != null)
        {
            owner.RegisterDamageDealt(damagePerShot);
        }
        if (IsHeadshot(hit, character.transform))
        {
            ApplyAntiHeal(character.gameObject);
        }
    }

    private bool IsHeadshot(RaycastHit hit, Transform root)
    {
        if (hit.collider != null && hit.collider.name.ToLowerInvariant().Contains("head"))
        {
            return true;
        }

        CharacterController controller = root.GetComponent<CharacterController>();
        float characterHeight = controller != null ? controller.height : 2f;
        float headThreshold = root.position.y + (characterHeight * 0.72f);
        return hit.point.y >= headThreshold;
    }

    private void ApplyAntiHeal(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        AntiHealStatus status = target.GetComponent<AntiHealStatus>();
        if (status == null)
        {
            status = target.AddComponent<AntiHealStatus>();
        }

        status.Apply(antiHealPercentage, antiHealDuration);
    }

    private void SetupViewModel()
    {
        if (!createViewModel)
        {
            return;
        }

        Transform parent = aimOrigin != null ? aimOrigin : transform;
        GameObject rootObject = new GameObject("BioticRifleViewModel");
        viewModelRoot = rootObject.transform;
        viewModelRoot.SetParent(parent, false);
        baseViewModelLocalPosition = weaponLocalPosition;
        baseViewModelLocalRotation = Quaternion.Euler(weaponLocalEuler);
        viewModelRoot.localPosition = baseViewModelLocalPosition;
        viewModelRoot.localRotation = baseViewModelLocalRotation;

        CreatePart("Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.22f), new Vector3(0.12f, 0.12f, 0.6f), Vector3.zero, new Color(0.14f, 0.18f, 0.2f));
        CreatePart("Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.58f), new Vector3(0.026f, 0.22f, 0.026f), new Vector3(90f, 0f, 0f), new Color(0.1f, 0.12f, 0.13f));
        CreatePart("Stock", PrimitiveType.Cube, new Vector3(0.02f, -0.03f, -0.12f), new Vector3(0.09f, 0.12f, 0.24f), new Vector3(6f, 0f, -6f), new Color(0.26f, 0.26f, 0.28f));
        CreatePart("Grip", PrimitiveType.Cube, new Vector3(0.04f, -0.11f, 0.05f), new Vector3(0.07f, 0.18f, 0.08f), new Vector3(18f, 0f, -10f), new Color(0.18f, 0.2f, 0.21f));
        CreatePart("BioticTube", PrimitiveType.Cylinder, new Vector3(-0.04f, 0.05f, 0.12f), new Vector3(0.02f, 0.2f, 0.02f), new Vector3(0f, 0f, 90f), new Color(0.18f, 0.7f, 0.6f));
        CreatePart("Head", PrimitiveType.Sphere, new Vector3(0.005f, 0.08f, 0.18f), new Vector3(0.055f, 0.055f, 0.055f), Vector3.zero, new Color(0.85f, 0.95f, 1f));

        GameObject muzzleObject = new GameObject("Muzzle");
        muzzleAnchor = muzzleObject.transform;
        muzzleAnchor.SetParent(viewModelRoot, false);
        muzzleAnchor.localPosition = new Vector3(0f, 0f, 0.78f);
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

        viewModelRoot.localPosition = Vector3.Lerp(viewModelRoot.localPosition, baseViewModelLocalPosition + swayPosition + animationPositionOffset, Time.deltaTime * animationSharpness);
        Quaternion targetRotation = baseViewModelLocalRotation * Quaternion.Euler(swayEuler + animationEulerOffset);
        viewModelRoot.localRotation = Quaternion.Slerp(viewModelRoot.localRotation, targetRotation, Time.deltaTime * animationSharpness);
    }

    private void CreatePart(string partName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(viewModelRoot, false);
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
            renderer.material = CreateTintedMaterial(color, color * 0.5f);
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

    private void SpawnTracerVisual(Vector3 start, Vector3 end)
    {
        Vector3 travel = end - start;
        if (travel.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        GameObject tracerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tracerObject.name = "Biotic Rifle Tracer";
        tracerObject.transform.position = start;
        tracerObject.transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        tracerObject.transform.localScale = tracerScale;

        Collider tracerCollider = tracerObject.GetComponent<Collider>();
        if (tracerCollider != null)
        {
            Destroy(tracerCollider);
        }

        Renderer tracerRenderer = tracerObject.GetComponent<Renderer>();
        if (tracerRenderer != null)
        {
            tracerRenderer.material = CreateTintedMaterial(tracerColor, tracerColor * 1.2f);
            tracerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracerRenderer.receiveShadows = false;
        }

        ProjectileVisual tracer = tracerObject.AddComponent<ProjectileVisual>();
        tracer.Initialize(start, end, tracerSpeed, tracerLifetime);
    }

    private sealed class ProjectileVisual : MonoBehaviour
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
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float distance = Vector3.Distance(startPoint, endPoint);
            if (distance <= 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            float progress = Mathf.Clamp01((elapsed * moveSpeed) / distance);
            transform.position = Vector3.Lerp(startPoint, endPoint, progress);
            if (progress >= 1f || elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
