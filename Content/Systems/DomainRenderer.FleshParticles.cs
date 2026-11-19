using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private sealed class FleshParticles(int capacity)
		{
			private readonly List<Particle> particles = new(capacity);
			public bool IsEmpty => particles.Count == 0;
			public bool IsFull => particles.Count >= capacity;

			private struct Particle
			{
				public Vector2 Position;
				public Vector2 Velocity;
				public float Size;
				public float Rotation;
				public float Spin;
				public float Opacity;
				public Color Color;
				public int Variant;
				public int Age;
				public int Lifetime;
			}

			public void Clear() => particles.Clear();

			public void Spawn(Vector2 position, Vector2 velocity, float size, float spin, Color tint, int variant,
				int lifetime, float opacity = 1f)
			{
				if (IsFull) return;
				particles.Add(new Particle
				{
					Position = position,
					Velocity = velocity,
					Size = size,
					Rotation = velocity.ToRotation(),
					Spin = spin,
					Opacity = opacity,
					Color = tint,
					Variant = variant,
					Lifetime = lifetime
				});
			}

			public void Update()
			{
				for (int i = particles.Count - 1; i >= 0; i--)
				{
					Particle particle = particles[i];
					if (++particle.Age >= particle.Lifetime)
					{
						particles[i] = particles[^1];
						particles.RemoveAt(particles.Count - 1);
						continue;
					}

					particle.Position += particle.Velocity;
					particle.Velocity *= 0.95f;
					particle.Rotation += particle.Spin;
					particles[i] = particle;
				}
			}

			public void Draw(Rectangle view)
			{
				foreach (Particle particle in particles)
				{
					if (!view.Contains(particle.Position.ToPoint())) continue;
					float life = particle.Age / (float)particle.Lifetime;
					float fade = (1f - life) * (1f - life) * particle.Opacity;
					Vector2 size = new Vector2(particle.Size * (1.75f - life * 0.67f), particle.Size * (0.54f + life * 0.26f));
					GhosteyeEffects.DrawFlesh(particle.Position, size, particle.Color * (fade * 0.88f), particle.Rotation, particle.Variant);
				}
			}
		}
	}
}
