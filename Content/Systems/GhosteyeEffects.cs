using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using static SpectesGaze.Content.Systems.DomainRenderer;

namespace SpectesGaze.Content.Systems
{
	// 只记录一次性演出。命中、状态和冷却仍由技能自身结算。
	internal sealed class GhosteyeEffects : ModSystem
	{
		private const int MaxEffects = 112;
		private const int MaxRifts = 6;
		private const int MaxParticles = 224;
		private const int FragmentSize = 32;
		private const int FragmentCount = 8;
		private static readonly List<Effect> Effects = new(MaxEffects);
		private static readonly List<Droplet> Droplets = new(MaxParticles);
		private static readonly Random VisualRandom = new();
		private static readonly Color DriedBlood = new(85, 13, 25);
		private static readonly Color WetBlood = new(180, 26, 45);
		private static Texture2D fragments;
		private static int activeRifts;

		private enum Shape { Wound, Tendril, Rift }

		private struct Effect
		{
			public Shape Shape;
			public Vector2 Position;
			public Vector2 Target;
			public Color Color;
			public float StartSize;
			public float EndSize;
			public float Angle;
			public int Variant;
			public int Age;
			public int Lifetime;
		}

		private struct Droplet
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public Color Color;
			public float Size;
			public float Stretch;
			public float Angle;
			public float Spin;
			public float Gravity;
			public int Variant;
			public int Age;
			public int Lifetime;
		}

		public override void OnWorldUnload() => Clear();

		public override void Unload()
		{
			Clear();
			Texture2D texture = fragments;
			fragments = null;
			if (texture != null) Main.QueueMainThreadAction(texture.Dispose);
		}

		private static void Clear()
		{
			Effects.Clear();
			Droplets.Clear();
			activeRifts = 0;
		}

		public override void PostUpdateEverything()
		{
			for (int i = Effects.Count - 1; i >= 0; i--)
			{
				Effect effect = Effects[i];
				effect.Age++;
				if (effect.Age < effect.Lifetime)
				{
					Effects[i] = effect;
					continue;
				}

				if (effect.Shape == Shape.Rift) activeRifts--;
				Effects.RemoveAt(i);
			}

			for (int i = Droplets.Count - 1; i >= 0; i--)
			{
				Droplet drop = Droplets[i];
				drop.Age++;
				if (drop.Age >= drop.Lifetime)
				{
					Droplets[i] = Droplets[^1];
					Droplets.RemoveAt(Droplets.Count - 1);
					continue;
				}

				drop.Velocity = new Vector2(drop.Velocity.X * 0.973f, Math.Min(8.5f, drop.Velocity.Y + drop.Gravity));
				drop.Position += drop.Velocity;
				drop.Angle += drop.Spin;
				Droplets[i] = drop;
			}
		}

		internal static void Open(Vector2 center, float radius)
		{
			Rift(center, radius * 0.14f, radius, Crimson, 34);
			Burst(center, WetBlood, 19, 7f);
		}

		internal static void Close(Vector2 center, float radius, bool overflowed)
		{
			Rift(center, radius, 12f, overflowed ? Ember : Blood, 26);
			if (overflowed) Burst(center, WetBlood, 19, 8f);
		}

		internal static void Freeze(Vector2 center, float radius)
		{
			Rift(center, 30f, radius, Still, 46);
			Wound(center + new Vector2(5f, -9f), 33f, 106f, Still, 47);
		}

		internal static void Overlap(Vector2 center, float radius)
		{
			Rift(center, radius * 0.33f, radius * 0.92f, Crimson, 40);
			Rift(center + new Vector2(-21f, 8f), radius * 0.93f, radius * 0.54f, Blood, 32);
			Burst(center, WetBlood, 18, 7f);
		}

		internal static void AwakenDomain(Vector2 center, float radius)
		{
			Rift(center, radius * 0.22f, radius, Pale, 50);
			Wound(center, 25f, 104f, Blood, 38);
			Burst(center, WetBlood, 16, 5f);
		}

		internal static void EchoHit(Vector2 center, Vector2 size, bool awakened)
		{
			if (Main.dedServ || Effects.Count >= MaxEffects - 26 || !NearScreen(center)) return;

			float reach = Math.Clamp(Math.Max(size.X, size.Y) * 0.40f, 15f, 47f);
			for (int i = 0; i < 2; i++)
			{
				Vector2 offset = new(Range(-14f, 15f), Range(-14f, 12f));
				Wound(center + offset, reach * Range(1.4f, 2f), reach * 0.8f, awakened ? Pale : WetBlood, VisualRandom.Next(11, 18));
			}
			Burst(center, DriedBlood, 3, 4f);
		}

		internal static void Rebirth(Vector2 center)
		{
			Wound(center + new Vector2(8f, -9f), 89f, 14f, Pale, 22);
			Wound(center - new Vector2(11f, 3f), 24f, 124f, Blood, 35);
			Burst(center, WetBlood, 18, 6f);
		}

		internal static void Restart(Vector2 center)
		{
			Wound(center + new Vector2(-7f, -12f), 89f, 15f, Violet, 26);
			Wound(center, 24f, 105f, Blood, 32);
			Burst(center, WetBlood, 17, 4f);
			for (int i = 0; i < 4; i++)
			{
				Vector2 basePoint = center + new Vector2(Range(-25f, 22f), Range(15f, 27f));
				Tendril(basePoint, basePoint + new Vector2(Range(-25f, 14f), Range(-106f, -52f)), Blood, 25, 7f);
			}
		}

		internal static float DrawRewind(Vector2 center, float radius, float progress, float time, Rectangle view)
		{
			float opening = MathHelper.SmoothStep(0f, 1f, progress / 0.16f);
			float closing = MathHelper.SmoothStep(0f, 1f, (progress - 0.78f) / 0.22f);
			float strength = opening * (1f - closing);

			float edgeRadius = MathHelper.SmoothStep(radius, 32f, progress);
			DrawTornEdge(center, edgeRadius, time - progress * 6f, new Color(168, 48, 82), strength, view);
			float tear = MathHelper.Lerp(96f, 23f, progress);
			DrawFlesh(center + new Vector2(9f, -3f), new Vector2(tear, tear * 0.46f), WetBlood * (0.45f * strength), -0.37f, 3);

			if (closing > 0f)
			{
				float flare = MathF.Sin(MathHelper.Pi * closing);
				DrawFlesh(center + new Vector2(-6f, 5f), new Vector2(24f + closing * 80f, 12f + closing * 22f),
					Light(Ember, 0.34f * flare), 0.19f, 6);
			}

			return strength;
		}

		internal static void DrawRewindEcho(Vector2 from, Vector2 to, float strength)
		{
			Vector2 drift = to - from;
			float distance = drift.Length();
			if (distance < 1f || strength <= 0f) return;

			// 长距离位移只留下分离的残片，避免一条血线贯穿屏幕。
			float angle = drift.ToRotation();
			DrawFlesh(to, new Vector2(Math.Min(distance + 11f, 44f), 7f + 8f * strength),
				WetBlood * (strength * 0.51f), angle + 0.11f, 4);
			if (distance <= 88f)
			{
				Vector2 point = Vector2.Lerp(from, to, 0.60f) + new Vector2(-2f, 3f);
				DrawFlesh(point, new Vector2(Math.Min(distance * 0.55f, 21f), 4.5f), Light(Blood, strength * 0.37f), angle - 0.18f, 2);
			}
		}

		internal static void Teleport(Vector2 from, Vector2 to)
		{
			Wound(from, 34f, 10f, Blood, 23);
			Wound(to, 10f, 60f, WetBlood, 26);
			Tendril(from, to, WetBlood, 17, 8f);
			Burst(to, WetBlood, 11, 3f);
		}

		internal static void Gaze(Vector2 from, Vector2 to)
		{
			Tendril(from, to, Blood, 16, 4f);
			Wound(to, 11f, 27f, WetBlood, 17);
		}

		internal static void Awaken(Vector2 center)
		{
			Wound(center, 18f, 103f, Blood, 32);
			Burst(center, WetBlood, 19, 5f);
		}

		internal static void Impact(Vector2 center, bool beam)
		{
			// 连续命中给玩家的施法演出留出空间。
			if (Main.dedServ || Effects.Count >= MaxEffects - 24 || !NearScreen(center)) return;

			Wound(center, 13f, beam ? 39f : 26f, WetBlood, 15);
			Burst(center, WetBlood, beam ? 5 : 9, beam ? 4f : 3f);
		}

		internal static void EmitBoltTrail(Vector2 center, Vector2 velocity, int age, int identity)
		{
			if (Main.dedServ || Droplets.Count >= MaxParticles || !NearScreen(center)) return;

			float spray = FleshNoise.Unit(unchecked((uint)identity * 463u + (uint)age * 1783u + 4151u));
			if (spray > 0.38f) return;

			Vector2 heading = velocity.SafeNormalize(Vector2.UnitX);
			Vector2 side = new(-heading.Y, heading.X);
			Vector2 scatter = side * Range(-2.4f, 2.1f) - heading * Range(0.8f, 2.4f);
			SpawnDroplet(center - heading * 5f + side * Range(-5f, 5f), scatter,
				Color.Lerp(DriedBlood, WetBlood, Range(0.18f, 0.95f)), Range(4f, 8f), Range(0.66f, 1.22f), VisualRandom.Next(12, 26));
			if (spray < 0.085f)
				SpawnDroplet(center + side * Range(-7f, 7f), scatter * 0.53f, DriedBlood, Range(3f, 5f), 0.66f, 18);
		}

		internal static void EmitBeamSpatter(Vector2 start, Vector2 direction, float length, int age, int identity)
		{
			if (Main.dedServ || Droplets.Count >= MaxParticles || age > 84) return;

			if (age == 1) Burst(start, WetBlood, 8, 4.4f);
			float spray = FleshNoise.Unit(unchecked((uint)identity * 1511u + (uint)age * 509u + 827u));
			if (spray > 0.27f) return;

			Vector2 side = new(-direction.Y, direction.X);
			int count = spray < 0.10f ? 3 : 1;
			for (int i = 0; i < count; i++)
			{
				float offset = Range(-28f, 32f);
				Vector2 position = start + direction * (Range(0.035f, 0.96f) * length) + side * offset;
				if (!NearScreen(position)) continue;

				Vector2 scatter = side * (MathF.Sign(offset) * Range(1.1f, 4.2f)) + direction * Range(-1.8f, 0.8f);
				SpawnDroplet(position, scatter, Color.Lerp(DriedBlood, WetBlood, Range(0.24f, 0.88f)),
					Range(3f, 8f), Range(0.65f, 1.2f), VisualRandom.Next(14, 29));
			}
		}

		private static void Rift(Vector2 center, float start, float end, Color color, int lifetime) =>
			Add(Shape.Rift, center, center, start, end, color, lifetime);

		private static void Wound(Vector2 center, float start, float end, Color color, int lifetime) =>
			Add(Shape.Wound, center, center, start, end, color, lifetime);

		private static void Tendril(Vector2 from, Vector2 to, Color color, int lifetime, float width) =>
			Add(Shape.Tendril, from, to, width, 1f, color, lifetime);

		private static void Burst(Vector2 center, Color color, int count, float speed)
		{
			if (Main.dedServ || !NearScreen(center)) return;

			for (int i = 0; i < count && Droplets.Count < MaxParticles; i++)
			{
				float angle = Range(-MathHelper.Pi, MathHelper.Pi);
				Vector2 heading = angle.ToRotationVector2();
				Vector2 velocity = heading * (speed * Range(0.28f, 1.10f));
				velocity.Y -= Range(0.15f, 2f);
				Color tint = Color.Lerp(color, DriedBlood, Range(0f, 0.52f));
				SpawnDroplet(center + heading * Range(0f, 9f), velocity, tint,
					Range(3f, 9f), Range(0.56f, 1.18f), VisualRandom.Next(15, 31));
			}
		}

		private static void SpawnDroplet(Vector2 position, Vector2 velocity, Color color, float size, float stretch, int lifetime)
		{
			if (Droplets.Count >= MaxParticles) return;

			Droplets.Add(new Droplet
			{
				Position = position,
				Velocity = velocity,
				Color = color,
				Size = size,
				Stretch = stretch,
				Angle = velocity.ToRotation() + Range(-0.65f, 0.70f),
				Spin = Range(-0.22f, 0.20f),
				Gravity = Range(0.16f, 0.36f),
				Variant = VisualRandom.Next(FragmentCount),
				Lifetime = lifetime
			});
		}

		private static void Add(Shape shape, Vector2 from, Vector2 to, float start, float end, Color color, int lifetime)
		{
			if (Main.dedServ || Effects.Count >= MaxEffects) return;
			if (shape == Shape.Rift)
			{
				if (activeRifts >= MaxRifts) return;
				activeRifts++;
			}

			Effects.Add(new Effect
			{
				Shape = shape,
				Position = from,
				Target = to,
				StartSize = start,
				EndSize = end,
				Color = color,
				Angle = Range(-MathHelper.Pi, MathHelper.Pi),
				Variant = VisualRandom.Next(FragmentCount),
				Lifetime = lifetime
			});
		}

		private static float Range(float min, float max) => MathHelper.Lerp(min, max, VisualRandom.NextSingle());

		internal static bool NearScreen(Vector2 point)
		{
			Vector2 screen = point - Main.screenPosition;
			return screen.X >= -168f && screen.X <= Main.screenWidth + 168f
				&& screen.Y >= -168f && screen.Y <= Main.screenHeight + 168f;
		}

		internal static void Draw(float time, Rectangle view)
		{
			foreach (Effect effect in Effects)
			{
				float progress = effect.Age / (float)effect.Lifetime;
				float remaining = 1f - progress;
				float eased = 1f - remaining * remaining * remaining;
				float size = MathHelper.Lerp(effect.StartSize, effect.EndSize, eased);
				switch (effect.Shape)
				{
					case Shape.Wound:
						if (!view.Contains(effect.Position.ToPoint())) break;

						DrawFlesh(effect.Position, new Vector2(size, size * 0.42f),
							effect.Color * (0.76f * remaining), effect.Angle + progress * 0.21f, effect.Variant);
						DrawFlesh(effect.Position + new Vector2(-3f, 2f), new Vector2(size * 0.68f, size * 0.29f),
							Light(effect.Color, 0.19f * remaining * remaining), effect.Angle - 0.17f, effect.Variant + 3);
						break;
					case Shape.Tendril:
						DrawTendril(effect, size, time, remaining, view);
						break;
					case Shape.Rift:
						DrawTornEdge(effect.Position, size, time + effect.Angle, effect.Color, remaining * remaining, view);
						break;
				}
			}

			foreach (Droplet drop in Droplets)
			{
				if (!view.Contains(drop.Position.ToPoint())) continue;

				float remaining = 1f - drop.Age / (float)drop.Lifetime;
				float opacity = Math.Min(1f, remaining * 2.2f) * 0.87f;
				float length = drop.Size * (drop.Stretch + drop.Velocity.Length() * 0.12f);
				DrawFlesh(drop.Position, new Vector2(length, drop.Size), drop.Color * opacity, drop.Angle, drop.Variant);
			}
		}

		private static void DrawTendril(Effect effect, float width, float time, float remaining, Rectangle view)
		{
			Vector2 delta = effect.Target - effect.Position;
			float distance = delta.Length();
			if (distance < 1f) return;

			Vector2 direction = delta / distance;
			Vector2 side = new(-direction.Y, direction.X);
			int count = Math.Clamp((int)(distance / 14f), 3, 16);
			for (int i = 0; i < count; i++)
			{
				float t = (i + 0.49f) / count;
				float offset = FleshNoise.Perlin(i * 0.59f, time * 1.45f, (uint)effect.Variant);
				Vector2 position = effect.Position + delta * t + side * (offset * 18f * MathF.Sin(t * MathHelper.Pi));
				if (!view.Contains(position.ToPoint())) continue;

				float length = Math.Min(distance / count * (0.61f + 0.48f * Math.Abs(offset)), 26f);
				DrawFlesh(position, new Vector2(length, width * (0.5f + 0.40f * Math.Abs(offset))),
					effect.Color * (remaining * (0.46f + 0.22f * Math.Abs(offset))),
					direction.ToRotation() + offset * 0.54f, effect.Variant + i);
			}
		}

		internal static void DrawFlesh(Vector2 position, Vector2 size, Color tint, float angle, int variant = 0)
		{
			fragments ??= CreateFragments();
			int cell = (int)((uint)variant % FragmentCount);
			Rectangle source = new(cell * FragmentSize, 0, FragmentSize, FragmentSize);
			Main.spriteBatch.Draw(fragments, position - Main.screenPosition, source, tint,
				angle, new Vector2(FragmentSize / 2f), size / FragmentSize, SpriteEffects.None, 0f);
		}

		private static Texture2D CreateFragments()
		{
			int width = FragmentSize * FragmentCount;
			Color[] pixels = new Color[width * FragmentSize];
			for (int variant = 0; variant < FragmentCount; variant++)
			{
				uint seed = (uint)variant * 271u + 683u;
				for (int y = 2; y < FragmentSize - 2; y++)
				{
					for (int x = 2; x < FragmentSize - 2; x++)
					{
						float px = (x - 15.5f) / 14f;
						float py = (y - 15.5f) / 14f;
						float curl = 0.21f * FleshNoise.Perlin(px * 2.9f, 2.3f, seed) + px * (variant % 2 == 0 ? 0.17f : -0.24f);
						float thickness = (0.42f + 0.10f * (variant % 3)) * (0.93f - 0.26f * px);
						float across = (py - curl) / thickness;
						float outline = MathF.Sqrt(px * px + across * across);
						if (variant >= 4)
						{
							float tornEdge = MathF.Abs(px) * 0.82f + MathF.Abs(across) * 0.92f;
							float notch = Math.Max(0f, 0.26f - MathF.Abs(px - 0.14f * (variant - 5)));
							outline = MathHelper.Lerp(outline, tornEdge, 0.64f) + notch * Math.Max(0f, across) * 2.1f;
						}
						float fray = FleshNoise.Perlin(px * 5.8f, py * 6.1f, seed + 3u);
						float alpha = MathHelper.Clamp((1f - outline + fray * 0.29f) * 5f, 0f, 1f);

						float grain = FleshNoise.Perlin(px * 12.1f, py * 10.6f, seed + 7u);
						float wet = MathHelper.Clamp((grain + 0.18f - MathF.Abs(py + 0.16f - curl) * 2.6f) * 2.2f, 0f, 1f);
						float value = MathHelper.Clamp(0.52f + grain * 0.34f + wet * 0.46f, 0f, 1f);
						pixels[y * width + variant * FragmentSize + x] = new Color(value * alpha, value * alpha, value * alpha, alpha);
					}
				}
			}

			Texture2D texture = new(Main.instance.GraphicsDevice, width, FragmentSize);
			texture.SetData(pixels);
			return texture;
		}
	}
}
