using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    public float maxMana = 100f;
    public float mana;

    public float regenRate = 10f;

    void Start()
    {
        mana = maxMana;
    }

    void Update()
    {
        if (mana < maxMana)
        {
            mana += regenRate * Time.deltaTime;
        }
    }

    public bool UseMana(float amount)
    {
        if (mana < amount)
            return false;

        mana -= amount;
        return true;
    }
}