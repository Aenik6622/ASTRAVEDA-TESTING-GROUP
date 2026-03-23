using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public float cooldown = 5f;
    protected float lastUseTime;

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