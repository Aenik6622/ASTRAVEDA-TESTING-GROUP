using System.Collections.Generic;
using UnityEngine;

public class CooldownSystem : MonoBehaviour
{
    private Dictionary<string, float> cooldowns = new Dictionary<string, float>();

    public bool IsReady(string abilityName)
    {
        if (!cooldowns.ContainsKey(abilityName))
            return true;

        return Time.time >= cooldowns[abilityName];
    }

    public void StartCooldown(string abilityName, float duration)
    {
        cooldowns[abilityName] = Time.time + duration;
    }
}