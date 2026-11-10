using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using static SpectesGaze.Content.Systems.DomainRenderer;

namespace SpectesGaze.Content.Systems
{
	// 只记录一次性演出。命中、状态和冷却仍由技能自身结算。
	internal sealed class GhosteyeEffects : ModSystem
	{
		private const int MaxEffects = 384;
		private static readonly List<Effect> Effects = new(MaxEffects);

		private enum Shape { Ring, Spark, Thread, Rift, FreezeVeil }

		private struct Effect
		{
			public Shape Shape;
			public Vector2 Position;
			public Vector2 Target;
			public Color Color;
			public float StartSize;
			public float EndSize;
			public int Age;
			public int Lifetime;
		}

		public override void OnWorldUnload() => Effects.Clear();
		public override void Unload() => Effects.Clear();

		public override void PostUpdateEverything()
		{
			for (int i = Effects.Count - 1; i >= 0; i--)
			{
				Effect effect = Effects[i];
				effect.Age++;
				if (effect.Age >= effect.Lifetime)
				{
					Effects.RemoveAt(i);
					continue;
				}
				Effects[i] = effect;
			}
		}

		internal static void Open(Vector2 center, float radius)
		{
			Add(Shape.Rift, center, center, radius * 0.14f, radius, Crimson, 34);
			Burst(center, Blood, 22, 7f);
		}

		internal static void Close(Vector2 center, float radius, bool overflowed)
		{
			Add(Shape.Rift, center, center, radius, 12f, overflowed ? Ember : Blood, 26);
			if (overflowed) Burst(center, Crimson, 23, 8f);
		}

		internal static void Freeze(Vector2 center, float radius)
		{
			Add(Shape.FreezeVeil, center, center, radius, radius, new Color(44, 51, 73), 84);
			Add(Shape.Rift, center, center, 30f, radius, Still, 46);
		}

		internal static void Restart(Vector2 center)
		{
			Ring(center, 146f, 12f, Violet, 26);
			Ring(center, 28f, 130f, Pale, 39);
			Burst(center, Ember, 18, 4f);
			for (int i = -2; i <= 2; i++)
			{
				Vector2 basePoint = center + new Vector2(i * 13f, 24f);
				Thread(basePoint, basePoint - Vector2.UnitY * (115f - 19f * Math.Abs(i)), Pale, 29, 10f);
			}
		}

		internal static float DrawRewind(Vector2 center, float radius, float progress, float time, Rectangle view)
		{
			float opening = MathHelper.SmoothStep(0f, 1f, progress / 0.16f);
			float closing = MathHelper.SmoothStep(0f, 1f, (progress - 0.78f) / 0.22f);
			float strength = opening * (1f - closing);

			DrawGlow(center, new Vector2(radius * 1.8f), new Color(38, 5, 24) * (0.35f * strength));
			float edgeRadius = MathHelper.SmoothStep(radius, 32f, progress);
			DrawTornEdge(center, edgeRadius, time - progress * 6f, new Color(182, 62, 118), strength, view);
			DrawRing(center, MathHelper.Lerp(78f, 18f, progress), Light(Blood, 0.5f * strength));

			if (closing > 0f)
			{
				DrawRing(center, MathHelper.Lerp(20f, 92f, closing),
					Light(Ember, 0.56f * MathF.Sin(MathHelper.Pi * closing)));
			}

			return strength;
		}

		internal static void DrawRewindEcho(Vector2 from, Vector2 to, float strength)
		{
			Vector2 drift = to - from;
			float distance = drift.Length();
			if (distance < 1f || strength <= 0f) return;

			// 历史中的远距离瞬移用分离的残影表现，避免血线横穿整屏。
			Color tint = Light(new Color(183, 47, 94), strength * 0.77f);
			if (distance <= 96f) DrawLine(from, to, 12f * strength, tint);
			DrawGlow(to, new Vector2(Math.Min(distance + 13f, 50f), 13f + 11f * strength), tint, drift.ToRotation());
		}

		internal static void Teleport(Vector2 from, Vector2 to)
		{
			Ring(from, 40f, 8f, Crimson, 24);
			Ring(to, 9f, 57f, Ember, 27);
			Thread(from, to, Crimson, 19, 28f);
			Thread(from, to, Pale, 11, 5f);
			Burst(to, Ember, 12, 3f);
		}

		internal static void Gaze(Vector2 from, Vector2 to)
		{
			Thread(from, to, Crimson, 16, 6f);
			Ring(to, 10f, 38f, Ember, 21);
		}

		internal static void Awaken(Vector2 center)
		{
			Ring(center, 18f, 110f, Crimson, 34);
			Burst(center, Ember, 22, 5f);
		}

		internal static void Impact(Vector2 center, bool beam)
		{
			// 连续命中给玩家的施法演出留出空间。
			if (Main.dedServ || Effects.Count >= MaxEffects - 64) return;

			Ring(center, 5f, beam ? 34f : 25f, Crimson, 17);
			Burst(center, Ember, beam ? 4 : 8, beam ? 4f : 3f);
		}

		private static void Ring(Vector2 center, float start, float end, Color color, int lifetime) =>
			Add(Shape.Ring, center, center, start, end, color, lifetime);

		private static void Thread(Vector2 from, Vector2 to, Color color, int lifetime, float width) =>
			Add(Shape.Thread, from, to, width, 1f, color, lifetime);

		private static void Burst(Vector2 center, Color color, int count, float speed)
		{
			for (int i = 0; i < count; i++)
			{
				Vector2 travel = (i * 2.39996f).ToRotationVector2() * speed * (5 + i % 7 * 2);
				Add(Shape.Spark, center, center + travel, 9f + i % 6 * 3f, 2f,
					i % 5 == 0 ? Pale : color, 15 + i % 8 * 2);
			}
		}

		private static void Add(Shape shape, Vector2 from, Vector2 to, float start, float end, Color color, int lifetime)
		{
			if (Main.dedServ || Effects.Count >= MaxEffects) return;

			Effects.Add(new Effect
			{
				Shape = shape,
				Position = from,
				Target = to,
				StartSize = start,
				EndSize = end,
				Color = color,
				Lifetime = lifetime
			});
		}

		internal static void Draw(float time, Rectangle view)
		{
			foreach (Effect effect in Effects)
			{
				float progress = effect.Age / (float)effect.Lifetime;
				float remaining = 1f - progress;
				float eased = 1f - remaining * remaining * remaining;
				float size = MathHelper.Lerp(effect.StartSize, effect.EndSize, eased);
				Color color = Light(effect.Color, 0.8f * remaining * remaining);
				switch (effect.Shape)
				{
					case Shape.Ring:
						DrawRing(effect.Position, size, color);
						break;
					case Shape.Spark:
						Vector2 point = Vector2.Lerp(effect.Position, effect.Target, eased);
						DrawGlow(point, new Vector2(size, size * 0.32f), color,
							(effect.Target - effect.Position).ToRotation());
						break;
					case Shape.Thread:
						DrawLine(effect.Position, effect.Target, size, color);
						break;
					case Shape.Rift:
						DrawTornEdge(effect.Position, size, time, effect.Color, remaining * remaining, view);
						break;
					case Shape.FreezeVeil:
						float opacity = Math.Min(1f, effect.Age / 4f + 0.25f) * remaining;
						DrawGlow(effect.Position, new Vector2(size * 2f), effect.Color * (0.41f * opacity));
						DrawGlow(effect.Position, new Vector2(290f), Light(Still, 0.19f * opacity * remaining));
						break;
				}
			}
		}
	}
}
