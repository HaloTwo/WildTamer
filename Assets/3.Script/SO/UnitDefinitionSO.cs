using UnityEngine;

[CreateAssetMenu(fileName = "UnitDefinition", menuName = "Game/Unit Definition")]
public class UnitDefinitionSO : ScriptableObject
{
    public GameObject prefab;

    public float moveSpeed = 2f;
    public float maxHP = 20f;

    public float attackDamage = 5f;
    public float attackRange = 1.2f;
    public float attackCooldown = 0.8f;

    [Header("Projectile (optional)")]
    public GameObject projectilePrefab; 
    public float projectileFlightTime = 0.25f;
    public float projectileArcHeight = 0.4f;
}