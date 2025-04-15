// IDamageable.cs
using UnityEngine;

public interface IDamageable
{
    void Damage(float damageAmount, GameObject damageSource);
    bool HasTakenDamage { get; set; }
}
