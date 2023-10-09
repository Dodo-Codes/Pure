﻿namespace Pure.Particles;

/// <summary>
/// Represents a base class for managing particles that can be inherited and overridden to catch events.
/// </summary>
public class ParticleManager
{
    /// <summary>
    /// Gets or sets a value indicating whether the particle manager has paused the updates
    /// of the particles.
    /// </summary>
    public bool IsPaused
    {
        get;
        set;
    }
    /// <summary>
    /// Gets the number of particles currently in the particle manager.
    /// </summary>
    public int Count
    {
        get => particles.Count;
    }

    /// <summary>
    /// Spawns a specified number of particles into the particle manager.
    /// </summary>
    /// <param name="amount">The number of particles to spawn.</param>
    public void Spawn(int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            var p = new Particle(default, default);
            particles.Add(p);
            OnSpawn(p);
        }
    }
    /// <summary>
    /// Updates all particles in the particle manager.
    /// </summary>
    /// <param name="deltaTime">The time since the last update.</param>
    public void Update(float deltaTime)
    {
        if (IsPaused)
            return;

        var toRemove = new List<Particle>();
        foreach (var p in particles)
        {
            var rad = MathF.PI / 180 * p.Movement.angle;
            var (dirX, dirY) = (MathF.Cos(rad), MathF.Sin(rad));
            var (x, y) = p.Position;

            p.Age -= deltaTime;
            p.Position = (x + dirX * p.Movement.speed, y + dirY * p.Movement.speed);

            if (p.Age <= 0)
            {
                toRemove.Add(p);
                continue;
            }

            OnUpdate(p);
        }

        foreach (var p in toRemove)
            particles.Remove(p);
    }

    /// <summary>
    /// Called when a particle is spawned into the particle manager.
    /// </summary>
    /// <param name="particle">The particle that was spawned.</param>
    protected virtual void OnSpawn(Particle particle)
    {
    }
    /// <summary>
    /// Called when a particle is updated in the particle manager.
    /// </summary>
    /// <param name="particle">The particle that was updated.</param>
    protected virtual void OnUpdate(Particle particle)
    {
    }

    /// <returns>
    /// An array of particles in the particle manager.</returns>
    public Particle[] ToArray()
    {
        return this;
    }
    /// <returns>
    /// An array of particle bundle tuples in the particle manager.</returns>
    public (float x, float y, uint color)[] ToPointsBundle()
    {
        var result = new (float x, float y, uint color)[particles.Count];
        for (var i = 0; i < particles.Count; i++)
            result[i] = particles[i];

        return result;
    }

    /// <summary>
    /// Implicitly converts an array of particles to a base particle manager instance.
    /// </summary>
    /// <param name="particles">The array of particles to convert.</param>
    /// <returns>A new base particle manager instance with the given 
    /// particles added to it.</returns>
    public static implicit operator ParticleManager(Particle[] particles)
    {
        var manager = new ParticleManager();
        foreach (var p in particles)
            manager.particles.Add(p);

        return manager;
    }
    /// <summary>
    /// Implicitly converts a base particle manager instance to an array of particles.
    /// </summary>
    /// <param name="manager">The base particle manager instance to convert.</param>
    /// <returns>An array of particles contained in the given base particle manager instance.</returns>
    public static implicit operator Particle[](ParticleManager manager)
    {
        return manager.particles.ToArray();
    }

#region Backend
    private readonly List<Particle> particles = new();
#endregion
}