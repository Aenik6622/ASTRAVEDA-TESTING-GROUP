using UnityEngine;

public abstract class GunModule : ScriptableObject
{
    public abstract void ModifyAttack(ref AttackData attack);
}