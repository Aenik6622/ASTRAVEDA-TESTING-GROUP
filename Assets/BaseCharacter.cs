using UnityEngine;

public class BaseCharacter : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health;

    public ManaSystem manaSystem;
    public CooldownSystem cooldownSystem;

    protected virtual void Start()
    {
        health = health > 0f ? Mathf.Clamp(health, 0f, maxHealth) : maxHealth;
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

    protected virtual void Die()
    {
        Debug.Log(name + " died");
    }
}
