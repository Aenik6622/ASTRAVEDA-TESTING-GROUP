using UnityEngine;

public class BaseCharacter : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health;

    [Header("Resources")]
    public ManaSystem manaSystem;
    public CooldownSystem cooldownSystem;

    [Header("Ultimate")]
    public float maxUltimateCharge = 100f;
    public float ultimateCharge;
    public float passiveUltimateChargePerSecond = 5f;
    public float ultimateChargePerDamageDealt = 1f;

    private float movementBuffMultiplier = 1f;
    private float ammoEfficiencyMultiplier = 1f;
    private float movementBuffTimer;
    private float ammoBuffTimer;

    public float CurrentMovementMultiplier => movementBuffMultiplier;
    public float CurrentAmmoEfficiencyMultiplier => ammoEfficiencyMultiplier;

    protected virtual void Start()
    {
        health = health > 0f ? Mathf.Clamp(health, 0f, maxHealth) : maxHealth;
        ultimateCharge = 0f;
    }

    protected virtual void Update()
    {
        if (passiveUltimateChargePerSecond <= 0f)
        {
        }
        else
        {
            AddUltimateCharge(passiveUltimateChargePerSecond * Time.deltaTime);
        }

        if (movementBuffTimer > 0f)
        {
            movementBuffTimer = Mathf.Max(0f, movementBuffTimer - Time.deltaTime);
            if (movementBuffTimer <= 0f)
            {
                movementBuffMultiplier = 1f;
            }
        }

        if (ammoBuffTimer > 0f)
        {
            ammoBuffTimer = Mathf.Max(0f, ammoBuffTimer - Time.deltaTime);
            if (ammoBuffTimer <= 0f)
            {
                ammoEfficiencyMultiplier = 1f;
            }
        }
    }

    public virtual void TakeDamage(float damage)
    {
        health -= damage;

        if (health <= 0)
        {
            Die();
        }
    }

    public virtual void Heal(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        AntiHealStatus antiHeal = GetComponent<AntiHealStatus>();
        float finalAmount = antiHeal != null ? amount * antiHeal.HealingMultiplier : amount;
        health = Mathf.Min(maxHealth, health + finalAmount);
    }

    public virtual void ResetRoundState()
    {
        health = maxHealth;
        ultimateCharge = 0f;
        movementBuffMultiplier = 1f;
        ammoEfficiencyMultiplier = 1f;
        movementBuffTimer = 0f;
        ammoBuffTimer = 0f;
    }

    public virtual void AddUltimateCharge(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        ultimateCharge = Mathf.Clamp(ultimateCharge + amount, 0f, maxUltimateCharge);
    }

    public virtual void RegisterDamageDealt(float damageDealt)
    {
        if (damageDealt <= 0f || ultimateChargePerDamageDealt <= 0f)
        {
            return;
        }

        AddUltimateCharge(damageDealt * ultimateChargePerDamageDealt);
    }

    public virtual bool HasFullUltimateCharge()
    {
        return ultimateCharge >= maxUltimateCharge;
    }

    public virtual bool TrySpendUltimateCharge(float amount)
    {
        if (ultimateCharge < amount)
        {
            return false;
        }

        ultimateCharge -= amount;
        return true;
    }

    public virtual void ApplyMovementBuff(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
        {
            return;
        }

        movementBuffMultiplier = Mathf.Max(movementBuffMultiplier, multiplier);
        movementBuffTimer = Mathf.Max(movementBuffTimer, duration);
    }

    public virtual void ApplyAmmoEfficiencyBuff(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
        {
            return;
        }

        ammoEfficiencyMultiplier = Mathf.Max(ammoEfficiencyMultiplier, multiplier);
        ammoBuffTimer = Mathf.Max(ammoBuffTimer, duration);
    }

    protected virtual void Die()
    {
        Debug.Log(name + " died");
    }
}
