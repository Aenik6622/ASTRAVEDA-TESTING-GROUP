using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown = 5f;
    protected float lastUseTime;

    public float CooldownRemaining => Mathf.Max(0f, (lastUseTime + cooldown) - Time.time);
    public virtual string AbilityDisplayName => GetType().Name;
    public virtual string AbilityBindingLabel => string.Empty;

    public virtual bool CanUse()
    {
        return Time.time >= lastUseTime + cooldown;
    }

    public void TryUse()
    {
        if (CanUse())
        {
            Activate();
            lastUseTime = Time.time;
        }
    }

    protected abstract void Activate();
}
