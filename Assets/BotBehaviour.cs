using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BotBehaviour : MonoBehaviour
{
    public enum BotTeam
    {
        Ally,
        Enemy
    }

    public enum BotRole
    {
        Standard,
        Tank
    }

    private static readonly List<BotBehaviour> ActiveBots = new List<BotBehaviour>();
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Identity")]
    [SerializeField] private BotTeam team = BotTeam.Enemy;
    [SerializeField] private BotRole role = BotRole.Standard;
    [SerializeField] private bool useRolePresetStats = false;

    [Header("Combat")]
    [SerializeField] private float maxHealth = 700f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackDamage = 50f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float detectionRange = 16f;
    [SerializeField] private float preferredDistance = 6f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float splashRadius = 2.5f;
    [SerializeField] private float splashMultiplier = 0.3f;

    [Header("Movement")]
    [SerializeField] private PatrolPath patrolPath;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointReachDistance = 0.75f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    [Header("Visuals")]
    [SerializeField] private bool applyTeamColors = true;
    [SerializeField] private Color allyBodyColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] private Color enemyBodyColor = new Color(0.93f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color allyHeadColor = new Color(0.98f, 0.9f, 0.48f, 1f);
    [SerializeField] private Color enemyHeadColor = new Color(1f, 0.45f, 0.48f, 1f);
    [SerializeField] private float tankScaleMultiplier = 1.22f;

    private BotBehaviour currentTarget;
    private CharacterController characterController;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock propertyBlock;
    private float attackTimer;
    private float currentHealth;
    private int patrolIndex;
    private bool isDead;
    private Vector3 baseScale = Vector3.one;

    public BotTeam Team => team;
    public BotRole Role => role;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    public event Action<float, float, bool> HealthChanged;

    private void Reset()
    {
        CacheVisualRenderers();
        baseScale = transform.localScale;
        ApplyRolePreset();
        ApplyTeamVisuals();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackRange = Mathf.Max(0.5f, attackRange);
        attackDamage = Mathf.Max(0f, attackDamage);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        detectionRange = Mathf.Max(attackRange, detectionRange);
        preferredDistance = Mathf.Clamp(preferredDistance, 1f, attackRange);
        splashRadius = Mathf.Max(0f, splashRadius);
        splashMultiplier = Mathf.Clamp01(splashMultiplier);
        tankScaleMultiplier = Mathf.Max(1f, tankScaleMultiplier);
        RefreshPatrolPoints();
        CacheVisualRenderers();
        if (!Application.isPlaying)
        {
            baseScale = transform.localScale;
        }
        ApplyTeamVisuals();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        propertyBlock = new MaterialPropertyBlock();
        RefreshPatrolPoints();
        CacheVisualRenderers();
        baseScale = transform.localScale;

        if (useRolePresetStats)
        {
            ApplyRolePreset();
        }
        else
        {
            currentHealth = maxHealth;
        }

        ApplyTeamVisuals();
        NotifyHealthChanged();
    }

    private void OnEnable()
    {
        if (!ActiveBots.Contains(this))
        {
            ActiveBots.Add(this);
        }

        BotHealthBar.EnsureFor(this);
        NotifyHealthChanged();
    }

    private void OnDisable()
    {
        ActiveBots.Remove(this);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        attackTimer -= Time.deltaTime;

        if (currentTarget == null || currentTarget.IsDead || currentTarget.Team == team)
        {
            currentTarget = FindNearestEnemy(detectionRange);
            if (currentTarget == null && !HasPatrolRoute())
            {
                currentTarget = FindNearestEnemy(Mathf.Infinity);
            }
        }

        if (currentTarget == null)
        {
            Patrol();
            return;
        }

        var distanceToTarget = FlatDistance(transform.position, currentTarget.transform.position);
        FaceTowards(currentTarget.transform.position);

        if (distanceToTarget <= attackRange && HasLineOfSight(currentTarget))
        {
            if (distanceToTarget < preferredDistance * 0.65f && role != BotRole.Tank)
            {
                MoveAwayFrom(currentTarget.transform.position);
            }

            TryAttack();
            return;
        }

        MoveTowards(currentTarget.transform.position);
    }

    [ContextMenu("Apply Role Preset")]
    public void ApplyRolePreset()
    {
        if (role == BotRole.Tank)
        {
            maxHealth = 600f;
            moveSpeed = 2.4f;
            attackRange = 9f;
            attackDamage = 50f;
            attackCooldown = 1f;
            detectionRange = 18f;
            preferredDistance = 5f;
            splashRadius = 3f;
            splashMultiplier = 0.3f;
        }
        else if (team == BotTeam.Ally)
        {
            maxHealth = 300f;
            moveSpeed = 3.8f;
            attackRange = 10f;
            attackDamage = 50f;
            attackCooldown = 1f;
            detectionRange = 18f;
            preferredDistance = 7f;
            splashRadius = 0f;
            splashMultiplier = 0f;
        }
        else
        {
            maxHealth = 300f;
            moveSpeed = 3.4f;
            attackRange = 9f;
            attackDamage = 50f;
            attackCooldown = 1f;
            detectionRange = 16f;
            preferredDistance = 6f;
            splashRadius = 0f;
            splashMultiplier = 0f;
        }

        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxHealth : currentHealth, 0f, maxHealth);
        ApplyTeamVisuals();
        NotifyHealthChanged();
    }

    public void ConfigureRuntime(BotTeam configuredTeam, BotRole configuredRole, Transform[] configuredPatrolPoints = null, PatrolPath configuredPatrolPath = null, bool applyPresetStats = true)
    {
        team = configuredTeam;
        role = configuredRole;
        patrolPath = configuredPatrolPath;
        patrolPoints = configuredPatrolPoints;
        useRolePresetStats = applyPresetStats;
        patrolIndex = 0;
        isDead = false;
        currentTarget = null;

        if (applyPresetStats)
        {
            currentHealth = 0f;
            ApplyRolePreset();
        }
        else
        {
            currentHealth = maxHealth;
            NotifyHealthChanged();
        }

        CacheVisualRenderers();
        ApplyTeamVisuals();
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        NotifyHealthChanged();
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
        {
            return;
        }

        AntiHealStatus antiHeal = GetComponent<AntiHealStatus>();
        float finalAmount = antiHeal != null ? amount * antiHeal.HealingMultiplier : amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth + finalAmount);
        NotifyHealthChanged();
    }

    private BotBehaviour FindNearestEnemy(float searchRange)
    {
        BotBehaviour bestTarget = null;
        var bestDistance = searchRange;

        for (var i = 0; i < ActiveBots.Count; i++)
        {
            var candidate = ActiveBots[i];
            if (candidate == null || candidate == this || candidate.IsDead || candidate.Team == team)
            {
                continue;
            }

            var distance = FlatDistance(transform.position, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private bool HasLineOfSight(BotBehaviour target)
    {
        var origin = transform.position + Vector3.up * 1.1f;
        var destination = target.transform.position + Vector3.up * 1.1f;

        if (!Physics.Linecast(origin, destination, out var hit, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.transform == target.transform || hit.transform.IsChildOf(target.transform);
    }

    private void TryAttack()
    {
        if (attackTimer > 0f || currentTarget == null || currentTarget.IsDead)
        {
            return;
        }

        currentTarget.TakeDamage(attackDamage);
        attackTimer = attackCooldown;

        if (role != BotRole.Tank || splashRadius <= 0f)
        {
            return;
        }

        for (var i = 0; i < ActiveBots.Count; i++)
        {
            var candidate = ActiveBots[i];
            if (candidate == null || candidate == this || candidate == currentTarget || candidate.IsDead || candidate.Team == team)
            {
                continue;
            }

            if (FlatDistance(currentTarget.transform.position, candidate.transform.position) <= splashRadius)
            {
                candidate.TakeDamage(attackDamage * splashMultiplier);
            }
        }
    }

    private void Patrol()
    {
        RefreshPatrolPoints();
        if (!HasPatrolRoute())
        {
            return;
        }

        for (var attempts = 0; attempts < patrolPoints.Length; attempts++)
        {
            var patrolTarget = patrolPoints[patrolIndex];
            if (patrolTarget != null)
            {
                MoveTowards(patrolTarget.position);
                FaceTowards(patrolTarget.position);

                if (FlatDistance(transform.position, patrolTarget.position) <= patrolPointReachDistance)
                {
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                }

                return;
            }

            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
    }

    private bool HasPatrolRoute()
    {
        RefreshPatrolPoints();
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void MoveTowards(Vector3 worldPosition)
    {
        var target = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
        var direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var step = direction.normalized * moveSpeed * Time.deltaTime;

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(step);
            return;
        }

        transform.position = transform.position + step;
    }

    private void MoveAwayFrom(Vector3 worldPosition)
    {
        var away = transform.position - worldPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = -transform.forward;
        }

        var destination = transform.position + away.normalized * preferredDistance * 0.5f;
        MoveTowards(destination);
    }

    private void FaceTowards(Vector3 worldPosition)
    {
        var direction = worldPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void Die()
    {
        isDead = true;
        currentTarget = null;
        NotifyHealthChanged();
        gameObject.SetActive(false);
    }

    private void CacheVisualRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ApplyTeamVisuals()
    {
        if (!applyTeamColors)
        {
            return;
        }

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            CacheVisualRenderers();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        var bodyColor = team == BotTeam.Ally ? allyBodyColor : enemyBodyColor;
        var headColor = team == BotTeam.Ally ? allyHeadColor : enemyHeadColor;

        if (role == BotRole.Tank)
        {
            bodyColor = Color.Lerp(bodyColor, Color.black, 0.18f);
            headColor = Color.Lerp(headColor, Color.white, 0.08f);
        }

        transform.localScale = role == BotRole.Tank ? baseScale * tankScaleMultiplier : baseScale;

        for (var i = 0; i < cachedRenderers.Length; i++)
        {
            var rendererComponent = cachedRenderers[i];
            if (rendererComponent == null)
            {
                continue;
            }

            var color = rendererComponent.transform.name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0
                ? headColor
                : bodyColor;

            rendererComponent.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            rendererComponent.SetPropertyBlock(propertyBlock);
        }
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(currentHealth, maxHealth, isDead);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = team == BotTeam.Ally ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (role == BotRole.Tank && splashRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, splashRadius);
        }
    }

    private void RefreshPatrolPoints()
    {
        if (patrolPath == null)
        {
            return;
        }

        patrolPoints = patrolPath.GetPoints();
    }
}


