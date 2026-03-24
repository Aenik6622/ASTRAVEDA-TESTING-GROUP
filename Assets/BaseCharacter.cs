using UnityEngine;

public class BaseCharacter : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health;

    public ManaSystem manaSystem;
    public CooldownSystem cooldownSystem;

    protected virtual void Start()
    {
        health = maxHealth;
    }

    public virtual void TakeDamage(float damage)
    {
        health -= damage;

        if (health <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        Debug.Log(name + " died");
    }
}