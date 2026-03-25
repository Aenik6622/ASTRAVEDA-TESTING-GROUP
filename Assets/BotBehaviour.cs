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
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointReachDistance = 0.75f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private BotBehaviour currentTarget;
    private CharacterController characterController;
    private float currentHealth;
    private float attackTimer;
    private int patrolIndex;
    private bool isDead;

    public BotTeam Team => team;
    public BotRole Role => role;
    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Reset()
    {
        ApplyRolePreset();
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
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (useRolePresetStats)
        {
            ApplyRolePreset();
        }
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        if (!ActiveBots.Contains(this))
        {
            ActiveBots.Add(this);
        }
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
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0f)
        {
            Die();
        }
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
        gameObject.SetActive(false);
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
}
