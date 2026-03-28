using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown = 5f;
    protected float lastUseTime;

    public float CooldownRemaining => Mathf.Max(0f, (lastUseTime + cooldown) - Time.time);
    public virtual string AbilityDisplayName => GetType().Name;
    public virtual string AbilityBindingLabel => string.Empty;
    public virtual string AbilityHudExtra => string.Empty;
    public virtual string AbilityHudIconPath => string.Empty;
    public virtual string AbilityStatusText
    {
        get
        {
            float cooldownRemaining = CooldownRemaining;
            bool ready = cooldownRemaining <= 0.01f && CanUse();
            return ready ? "READY" : cooldownRemaining > 0.01f ? cooldownRemaining.ToString("0.0s") : "ACTIVE";
        }
    }

    public virtual Color AbilityStatusColor
    {
        get
        {
            float cooldownRemaining = CooldownRemaining;
            bool ready = cooldownRemaining <= 0.01f && CanUse();
            return ready ? new Color(0.82f, 1f, 0.82f) : new Color(1f, 0.78f, 0.55f);
        }
    }

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
