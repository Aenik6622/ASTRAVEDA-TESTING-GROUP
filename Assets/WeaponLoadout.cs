using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponLoadout : MonoBehaviour
{
    private readonly List<WeaponAbility> weapons = new List<WeaponAbility>();
    private int activeSlot;
    private bool loadoutLocked;

    private void Awake()
    {
        WeaponAbility[] foundWeapons = GetComponents<WeaponAbility>();
        for (int i = 0; i < foundWeapons.Length; i++)
        {
            RegisterWeapon(foundWeapons[i]);
        }
    }

    private void Start()
    {
        EquipSlot(activeSlot);
    }

    private void Update()
    {
        if (loadoutLocked)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            EquipSlot(0);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            EquipSlot(1);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f || weapons.Count <= 1)
        {
            return;
        }

        int direction = scroll > 0f ? -1 : 1;
        int nextIndex = (activeSlot + direction + weapons.Count) % weapons.Count;
        EquipSlot(nextIndex);
    }

    public void RegisterWeapon(WeaponAbility weapon)
    {
        if (weapon == null || weapons.Contains(weapon))
        {
            return;
        }

        weapons.Add(weapon);
        weapons.Sort((a, b) => a.WeaponSlot.CompareTo(b.WeaponSlot));
    }

    public void SetLoadoutLocked(bool locked)
    {
        loadoutLocked = locked;

        if (weapons.Count == 0)
        {
            return;
        }

        if (locked)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                weapons[i].SetEquipped(false);
            }

            return;
        }

        EquipSlot(activeSlot);
    }

    private void EquipSlot(int slot)
    {
        if (weapons.Count == 0)
        {
            return;
        }

        activeSlot = Mathf.Clamp(slot, 0, weapons.Count - 1);
        for (int i = 0; i < weapons.Count; i++)
        {
            weapons[i].SetEquipped(i == activeSlot);
        }
    }
}
