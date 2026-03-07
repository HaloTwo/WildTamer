using UnityEngine;

[CreateAssetMenu(fileName = "UnitDataSO", menuName = "Game/UnitDataSO")]
public class UnitDataSO : ScriptableObject
{
    public UnitType UnitType;
    public UnitKey UnitKey;

    public float moveSpeed = 2f;
    public float maxHP = 20f;

    public float attackDamage = 5f;
    public float attackRange = 1.2f;
    public float attackCooldown = 0.8f;
    public float tameChance = 0.8f;

}