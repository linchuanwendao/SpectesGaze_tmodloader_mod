using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private sealed class SparkParticles(int capacity)
		{
			private static readonly Color SparkCool = new(122, 16, 49);
			private readonly List<Spark> sparks = new(capacity);
			public bool IsFull => sparks.Count >= capacity;
			public bool IsEmpty => sparks.Count == 0;

			private struct Spark
			{
				public Vector2 Position;
				public Vector2 Tail;
				public Vector2 Velocity;
				public Color Tint;
				public float Width;
				public float Opacity;
				public int Age;
				public int Lifetime;
			}

			public void Clear() => sparks.Clear();

			public void Spawn(Vector2 position, Vector2 velocity, Color tint, float width, float opacity, int lifetime)
			{
				if (IsFull) return;
				sparks.Add(new Spark
				{
					Position = position,
					Tail = position,
					Velocity = velocity,
					Tint = tint,
					Width = width,
					Opacity = opacity,
					Lifetime = lifetime
				});
			}

			public void Update()
			{
				for (int i = sparks.Count - 1; i >= 0; i--)
				{
					Spark spark = sparks[i];
					if (++spark.Age >= spark.Lifetime)
					{
						sparks.RemoveAt(i);
						continue;
					}

					spark.Tail = Vector2.Lerp(spark.Tail, spark.Position, 0.19f);
					spark.Velocity *= spark.Age < spark.Lifetime / 3 ? 1.018f : 0.94f;
					spark.Velocity.Y -= 0.026f;
					spark.Position += spark.Velocity;
					sparks[i] = spark;
				}
			}

			public void Draw(Rectangle view)
			{
				foreach (Spark spark in sparks)
				{
					if (spark.Age == 0 || !Visible(spark.Tail, spark.Position, view)) continue;

					float remaining = 1f - spark.Age / (float)spark.Lifetime;
					float opacity = Math.Min(1f, spark.Age / 5f) * remaining * remaining * spark.Opacity;
					float width = spark.Width * (0.35f + 0.65f * remaining);
					Color trail = Color.Lerp(SparkCool, spark.Tint, remaining);
					Color core = Color.Lerp(trail, Pale, remaining * 0.62f);
					DrawGlow(spark.Position, new Vector2(width * 8f, width * 6f), Light(trail, opacity * 0.62f),
						spark.Velocity.ToRotation());
					DrawSoftLine(spark.Tail, spark.Position, width * 3.4f, Light(trail, opacity * 0.48f));
					DrawSoftLine(Vector2.Lerp(spark.Tail, spark.Position, 0.42f), spark.Position, width, Light(core, opacity));
				}
			}
		}
	}
}
