using UnityEngine;

public abstract class WeaponAbility : Ability
{
    [SerializeField] private int weaponSlot;
    [SerializeField] private string equipLabel = "1";

    public int WeaponSlot => weaponSlot;
    public bool IsEquipped { get; private set; }
    public bool IsWeaponAbility => true;

    public override string AbilityBindingLabel => equipLabel;
    public override string AbilityStatusText => IsEquipped ? base.AbilityStatusText : "HOLSTERED";
    public override Color AbilityStatusColor => IsEquipped ? base.AbilityStatusColor : new Color(0.65f, 0.65f, 0.72f);

    protected virtual void OnEnable()
    {
        WeaponLoadout loadout = GetComponent<WeaponLoadout>();
        if (loadout != null)
        {
            loadout.RegisterWeapon(this);
        }
    }

    public void InitializeWeaponSlot(int slot, string label)
    {
        weaponSlot = slot;
        equipLabel = label;
    }

    public void SetEquipped(bool equipped)
    {
        if (IsEquipped == equipped)
        {
            return;
        }

        IsEquipped = equipped;
        OnEquippedChanged(equipped);
    }

    protected virtual void OnEquippedChanged(bool equipped)
    {
    }
}
