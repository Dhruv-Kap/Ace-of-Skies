using UnityEngine;

public interface IDamageable
{
    /// <summary>
    /// Apply damage to this object.
    /// </summary>
    /// <param name="damageAmount">How much damage to take.</param>
    void TakeDamage(float damageAmount);

    /// <summary>
    /// Apply damage to this object with killer tracking.
    /// </summary>
    /// <param name="damageAmount">How much damage to take.</param>
    /// <param name="damageDealer">Who caused the damage (for kill tracking).</param>
    void TakeDamage(float damageAmount, GameObject damageDealer);

    /// <summary>
    /// Returns true if the object is dead/destroyed.
    /// </summary>
    bool IsDead { get; }
}