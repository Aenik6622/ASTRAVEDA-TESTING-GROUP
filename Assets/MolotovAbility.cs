using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Abilities/Molotov Ability")]
[DisallowMultipleComponent]
public class MolotovAbility : Ability
{
    [Header("References")]
    [SerializeField] private BaseCharacter owner;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private Transform aimOrigin;

    [Header("Input")]
    [SerializeField] private int activationMouseButton = 1;

    [Header("Throw")]
    [SerializeField] private float throwForce = 18f;
    [SerializeField] private float upwardForce = 4f;
    [SerializeField] private float maxAimDistance = 100f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Header("Explosion")]
    [SerializeField] private float impactDamage = 35f;
    [SerializeField] private float explosionRadius = 4f;
    [SerializeField] private float explosionDamage = 30f;

    [Header("Fire Area")]
    [SerializeField] private float fireDuration = 6f;
    [SerializeField] private float fireRadius = 4.5f;
    [SerializeField] private float fireDamagePerTick = 8f;
    [SerializeField] private float fireTickInterval = 0.5f;

    [Header("Physics")]
    [SerializeField] private float projectileLifetime = 8f;
    [SerializeField] private float projectileRadius = 0.2f;
    [SerializeField] private bool damageOwner;

    private Camera cachedCamera;

    public override string AbilityDisplayName => "Molotov";
    public override string AbilityBindingLabel => "RMB";
    public override string AbilityHudIconPath => @"C:/Users/Admin/Pictures/molly.jpeg";

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponent<BaseCharacter>();
        }

        if (throwOrigin == null)
        {
            throwOrigin = transform;
        }

        if (aimOrigin == null)
        {
            aimOrigin = throwOrigin;
        }

        AbilityHudOverlay.EnsureFor(gameObject);

        if (cooldown <= 0f)
        {
            cooldown = 6f;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(activationMouseButton))
        {
            TryUse();
        }
    }

    protected override void Activate()
    {
        Vector3 spawnPosition = throwOrigin.position + (throwOrigin.forward * 0.6f) + (Vector3.up * 0.15f);
        GameObject projectileObject = new GameObject("Molotov Projectile");
        projectileObject.transform.position = spawnPosition;
        projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.2f, projectileRadius * 2f);

        SphereCollider collider = projectileObject.AddComponent<SphereCollider>();
        collider.radius = projectileRadius;

        Rigidbody rigidbody = projectileObject.AddComponent<Rigidbody>();
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        MolotovProjectile projectile = projectileObject.AddComponent<MolotovProjectile>();
        projectile.Initialize(owner, damageOwner, impactDamage, explosionRadius, explosionDamage, fireDuration, fireRadius, fireDamagePerTick, fireTickInterval, projectileLifetime);
        CreateProjectileVisual(projectileObject.transform);

        Vector3 targetPoint = GetAimPoint();
        Vector3 throwDirection = (targetPoint - spawnPosition).normalized;
        if (throwDirection.sqrMagnitude <= 0.001f)
        {
            throwDirection = aimOrigin.forward;
        }

        Vector3 launchVelocity = (throwDirection * throwForce) + (Vector3.up * upwardForce);
        rigidbody.linearVelocity = launchVelocity;
    }

    private Vector3 GetAimPoint()
    {
        Camera aimCamera = cachedCamera != null ? cachedCamera : Camera.main;
        cachedCamera = aimCamera;

        if (aimCamera != null)
        {
            Ray aimRay = aimCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(aimRay, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return aimRay.origin + (aimRay.direction * maxAimDistance);
        }

        return aimOrigin.position + (aimOrigin.forward * maxAimDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = throwOrigin != null ? throwOrigin : transform;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(origin.position, fireRadius);
    }

    private void CreateProjectileVisual(Transform projectileRoot)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(projectileRoot, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.22f;

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = CreateTintedMaterial(new Color(0.34f, 0.12f, 0.04f), new Color(0.9f, 0.35f, 0.08f));
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

    private sealed class MolotovProjectile : MonoBehaviour
    {
        private BaseCharacter owner;
        private bool damageOwner;
        private float impactDamage;
        private float explosionRadius;
        private float explosionDamage;
        private float fireDuration;
        private float fireRadius;
        private float fireDamagePerTick;
        private float fireTickInterval;
        private bool exploded;

        public void Initialize(BaseCharacter sourceOwner, bool canDamageOwner, float directHitDamage, float blastRadius, float blastDamage, float burnDuration, float burnRadius, float burnDamagePerTick, float burnTickInterval, float lifetime)
        {
            owner = sourceOwner;
            damageOwner = canDamageOwner;
            impactDamage = directHitDamage;
            explosionRadius = blastRadius;
            explosionDamage = blastDamage;
            fireDuration = burnDuration;
            fireRadius = burnRadius;
            fireDamagePerTick = burnDamagePerTick;
            fireTickInterval = burnTickInterval;
            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (exploded)
            {
                return;
            }

            exploded = true;
            TryDamage(collision.collider.gameObject, impactDamage, new HashSet<Component>());
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Explode(hitPoint);
        }

        private void Explode(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
            HashSet<Component> damagedObjects = new HashSet<Component>();
            for (int i = 0; i < hits.Length; i++)
            {
                TryDamage(hits[i].gameObject, explosionDamage, damagedObjects);
            }

            GameObject fireObject = new GameObject("Molotov Fire Area");
            fireObject.transform.position = position;
            MolotovFireArea fireArea = fireObject.AddComponent<MolotovFireArea>();
            fireArea.Initialize(owner, damageOwner, fireRadius, fireDuration, fireDamagePerTick, fireTickInterval);
            Destroy(gameObject);
        }

        private void TryDamage(GameObject targetObject, float damage, HashSet<Component> damagedObjects)
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

                if (damagedObjects.Add(bot))
                {
                    bot.TakeDamage(damage);
                    if (owner != null)
                    {
                        owner.RegisterDamageDealt(damage);
                    }
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

            if (damagedObjects.Add(character))
            {
                character.TakeDamage(damage);
                if (owner != null)
                {
                    owner.RegisterDamageDealt(damage);
                }
            }
        }
    }

    private sealed class MolotovFireArea : MonoBehaviour
    {
        private BaseCharacter owner;
        private bool damageOwner;
        private float radius;
        private float damagePerTick;
        private float tickInterval;
        private float nextTickTime;

        public void Initialize(BaseCharacter sourceOwner, bool canDamageOwner, float fireRadius, float fireDuration, float fireDamagePerTick, float fireTickInterval)
        {
            owner = sourceOwner;
            damageOwner = canDamageOwner;
            radius = fireRadius;
            damagePerTick = fireDamagePerTick;
            tickInterval = Mathf.Max(0.05f, fireTickInterval);

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;

            BuildVisuals();
            Destroy(gameObject, fireDuration);
        }

        private void Update()
        {
            if (Time.time < nextTickTime)
            {
                return;
            }

            nextTickTime = Time.time + tickInterval;

            Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
            HashSet<Component> damagedObjects = new HashSet<Component>();
            for (int i = 0; i < hits.Length; i++)
            {
                TryDamage(hits[i].gameObject, damagedObjects);
            }
        }

        private void TryDamage(GameObject targetObject, HashSet<Component> damagedObjects)
        {
            if (damagePerTick <= 0f || targetObject == null)
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

                if (damagedObjects.Add(bot))
                {
                    bot.TakeDamage(damagePerTick);
                    if (owner != null)
                    {
                        owner.RegisterDamageDealt(damagePerTick);
                    }
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

            if (damagedObjects.Add(character))
            {
                character.TakeDamage(damagePerTick);
                if (owner != null)
                {
                    owner.RegisterDamageDealt(damagePerTick);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.4f);
            Gizmos.DrawSphere(transform.position, radius);
        }

        private void BuildVisuals()
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Fire Ring";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);

            Collider ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null)
            {
                Destroy(ringCollider);
            }

            Renderer ringRenderer = ring.GetComponent<Renderer>();
            if (ringRenderer != null)
            {
                ringRenderer.material = CreateTintedMaterial(new Color(0.95f, 0.32f, 0.08f, 0.8f), new Color(1f, 0.45f, 0.1f));
            }

            for (int i = 0; i < 6; i++)
            {
                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "Flame " + i;
                flame.transform.SetParent(transform, false);

                float angle = (Mathf.PI * 2f * i) / 6f;
                float distance = radius * 0.45f;
                flame.transform.localPosition = new Vector3(Mathf.Cos(angle) * distance, 0.35f, Mathf.Sin(angle) * distance);
                flame.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);

                Collider flameCollider = flame.GetComponent<Collider>();
                if (flameCollider != null)
                {
                    Destroy(flameCollider);
                }

                Renderer flameRenderer = flame.GetComponent<Renderer>();
                if (flameRenderer != null)
                {
                    flameRenderer.material = CreateTintedMaterial(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.55f, 0.18f));
                }
            }

            Light fireLight = gameObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.45f, 0.12f);
            fireLight.range = Mathf.Max(6f, radius * 2.5f);
            fireLight.intensity = 4f;
        }
    }
}
