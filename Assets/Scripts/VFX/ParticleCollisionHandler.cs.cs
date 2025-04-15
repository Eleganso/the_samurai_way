using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleCollisionHandler : MonoBehaviour
{
    private ParticleSystem particleSystem;

    private void Awake()
    {
        // Get the particle system component
        particleSystem = GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            Debug.LogError("ParticleCollisionHandler: No ParticleSystem found on this GameObject!");
        }
    }

    private void OnParticleCollision(GameObject other)
    {
        // Ensure the particle system is valid
        if (particleSystem == null) return;

        // Retrieve collided particles
        ParticleSystem.CollisionModule collisionModule = particleSystem.collision;

        // Handle collision response (Optional: Add effects based on "other" GameObject)
        Debug.Log($"Particle collided with: {other.name}");

        // Add optional effects (e.g., spawn splatter, reduce lifetime)
        HandleCollisionEffects(other);
    }

    private void HandleCollisionEffects(GameObject other)
    {
        // Example: Create a decal or smaller particles on collision
        // Add your custom collision effect logic here if needed

        // Destroy the particle effect after a delay
        Destroy(gameObject, 1f); // Adjust delay as needed
    }
}
