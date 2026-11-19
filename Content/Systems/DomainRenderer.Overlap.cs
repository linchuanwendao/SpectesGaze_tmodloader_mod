using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using SpectesGaze.Content.Players;
using static SpectesGaze.Content.Systems.FleshNoise;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private const int OverlapEntryTicks = 24;
		private static readonly Color OverlapSoot = new(11, 2, 16);
		private static readonly Color OverlapRed = new(255, 38, 68);
		private static readonly Color OverlapPurple = new(168, 67, 255);
		private static Texture2D overlapCoreMask;

		private readonly byte[] overlapAge = new byte[Main.maxPlayers];
		private readonly FleshParticles overlapAsh = new(240);
		private readonly SparkParticles overlapSparks = new(240);
		private readonly Random overlapRandom = new();
		private bool hasOverlapForms;

		private void UpdateOverlapEffects()
		{
			overlapAsh.Update();
			overlapSparks.Update();
			hasOverlapForms = false;

			for (int i = 0; i < overlapAge.Length; i++)
			{
				Player player = Main.player[i];
				if (player == null || !player.active || !player.GetModPlayer<GhosteyePlayer>().OverlapActive)
				{
					overlapAge[i] = 0;
					continue;
				}

				hasOverlapForms = true;
				bool entering = overlapAge[i] == 0;
				if (overlapAge[i] < OverlapEntryTicks) overlapAge[i]++;
				if (!GhosteyeEffects.NearScreen(player.Center)) continue;

				Lighting.AddLight(player.Center, 0.69f, 0.10f, 0.25f);
				if (entering || Main.GameUpdateCount % 2 == 0) EmitOverlapParticles(player.Center, player.velocity, entering);
			}
		}

		private void EmitOverlapParticles(Vector2 center, Vector2 movement, bool entering)
		{
			int count = entering ? 30 : 5;
			for (int i = 0; i < count; i++)
			{
				Vector2 radial = (overlapRandom.NextSingle() * MathHelper.TwoPi).ToRotationVector2();
				Vector2 tangent = new(-radial.Y, radial.X);
				float radius = 19f + overlapRandom.NextSingle() * 25f;
				Vector2 position = center + radial * radius;
				Vector2 velocity = radial * (entering ? 4f + overlapRandom.NextSingle() * 5f : 0.8f)
					+ tangent * (1f + overlapRandom.NextSingle() * 3f) + movement * 0.12f;

				if (i % 3 == 0)
				{
					overlapAsh.Spawn(position, velocity * 0.72f, 11f + overlapRandom.NextSingle() * 21f,
						overlapRandom.NextSingle() * 0.16f - 0.08f, OverlapSoot, overlapRandom.Next(8),
						overlapRandom.Next(22, 44), 0.93f);
				}
				else
				{
					Color color = i % 3 == 1 ? OverlapRed : OverlapPurple;
					overlapSparks.Spawn(position, velocity, color, 1.5f + overlapRandom.NextSingle() * 2.8f,
						0.98f, overlapRandom.Next(16, 33));
				}
			}
		}

		private void DrawOverlapEffects(Rectangle view, float time)
		{
			for (int i = 0; i < overlapAge.Length; i++)
			{
				if (overlapAge[i] == 0) continue;
				Player player = Main.player[i];
				if (!player.active || !view.Contains(player.Center.ToPoint())
					|| !player.GetModPlayer<GhosteyePlayer>().OverlapActive) continue;
				DrawOverlapForm(player.Center, player.velocity, time, overlapAge[i], i);
			}

			overlapAsh.Draw(view);
			overlapSparks.Draw(view);
		}

		private static void DrawOverlapForm(Vector2 center, Vector2 movement, float time, int age, int owner)
		{
			float phase = time * 5.6f + owner * 1.67f;
			float entry = 1f - (age / (float)OverlapEntryTicks);
			float pulse = 1f + 0.09f * MathF.Sin(phase * 1.08f) + 0.16f * entry;
			float stretch = Math.Min(movement.Length() * 0.47f, 15f);
			Vector2 size = new Vector2(90f + stretch, 91f - stretch * 0.33f) * pulse;
			float turn = MathF.Sin(phase * 0.44f) * 0.5f;

			DrawGlow(center, size * 2.35f, Light(OverlapPurple, 0.31f), -phase * 0.21f);
			DrawGlow(center, size * 2.02f, Light(OverlapRed, 0.55f), phase * 0.26f);
			DrawOverlapTendrils(center, size, phase, owner);
			DrawOverlapMask(center, size * 1.12f, OverlapSoot, turn);
			DrawOverlapMask(center, size, new Color(119, 6, 35), -turn * 2f);
			DrawOverlapMask(center + phase.ToRotationVector2() * 3.5f, size * 0.78f, new Color(38, 2, 27), phase * 0.21f);
			DrawOverlapMask(center, size * 0.65f, OverlapSoot, -phase * 0.24f);

			for (int i = 0; i < 7; i++)
			{
				float angle = phase * 0.72f + i * (MathHelper.TwoPi / 7f);
				uint seed = (uint)(owner * 47 + i * 7 + 1);
				float fray = 0.5f + Perlin(i * 0.67f, phase * 0.31f, seed) * 0.5f;
				Vector2 radial = angle.ToRotationVector2();
				Vector2 point = center + radial * size * (0.29f + fray * 0.13f);
				Color tint = i % 3 == 0 ? OverlapPurple : OverlapRed;
				GhosteyeEffects.DrawFlesh(point, new Vector2(61f, 30f) * (0.68f + fray * 0.56f),
					Light(tint, 0.44f + fray * 0.32f), angle + 1.04f, i);
			}

			DrawOverlapArc(center, size * 0.57f, phase, turn + 0.52f, OverlapRed);
			DrawOverlapArc(center, size * 0.62f, -phase * 1.16f, turn - 0.94f, OverlapPurple);
			DrawOverlapArc(center, size * 0.45f, phase * 1.38f + 2.2f, turn + 1.48f, Ember);

			// 开场留一圈外扩的余光，常驻时只保留缓慢呼吸的光团。
			if (entry > 0f)
				DrawOverlapArc(center, size * MathHelper.Lerp(1.15f, 0.46f, entry), phase, turn,
					Color.Lerp(OverlapRed, Pale, entry) * entry);
		}

		private static void DrawOverlapTendrils(Vector2 center, Vector2 size, float phase, int owner)
		{
			const int strands = 5;
			const int segments = 7;
			for (int i = 0; i < strands; i++)
			{
				uint seed = (uint)(owner * 97 + i * 43 + 11);
				float angle = (i + Unit(seed) * 0.54f) * MathHelper.TwoPi / strands
					+ MathF.Sin(phase * 0.29f + i) * 0.66f;
				Vector2 radial = angle.ToRotationVector2();
				Vector2 tangent = new(-radial.Y, radial.X);
				float extension = 0.68f + 0.35f * MathF.Sin(phase * 0.91f + i * 2.1f);
				Vector2 root = center + radial * size * 0.34f;
				Vector2 previous = root;
				Color tint = i % 2 == 0 ? OverlapRed : OverlapPurple;
				for (int j = 1; j <= segments; j++)
				{
					float u = j / (float)segments;
					float curl = Perlin(u * 4.9f, phase * 0.55f, seed) * u * 20f;
					Vector2 point = root + radial * size * (extension * u) + tangent * curl;
					float opacity = 1f - (j - 1f) / segments;
					DrawSoftLine(previous, point, 14f * opacity, Light(tint, opacity * 0.25f));
					DrawSoftLine(previous, point, 2.9f * opacity, Light(tint, opacity * 0.78f));
					previous = point;
				}
			}
		}

		private static void DrawOverlapArc(Vector2 center, Vector2 size, float phase, float tilt, Color tint)
		{
			const int segments = 17;
			Vector2 previous = center;
			for (int i = 0; i <= segments; i++)
			{
				float progress = i / (float)segments;
				float angle = phase + progress * 3.85f;
				float flare = 1f + 0.09f * MathF.Sin(angle * 4f + phase);
				Vector2 point = center + (angle.ToRotationVector2() * new Vector2(size.X, size.Y * 0.59f) * flare).RotatedBy(tilt);
				if (i > 0)
				{
					float fade = MathF.Sin(MathHelper.Pi * (i - 0.5f) / segments);
					DrawSoftLine(previous, point, 3f + fade * 7f, Light(tint, fade * 0.36f));
					DrawSoftLine(previous, point, 0.9f + fade * 1.6f, Light(tint, fade * 0.91f));
				}
				previous = point;
			}
		}

		private static void DrawOverlapMask(Vector2 center, Vector2 size, Color color, float angle)
		{
			overlapCoreMask ??= CreateOverlapMask();
			Main.spriteBatch.Draw(overlapCoreMask, center - Main.screenPosition, null, color, angle,
				overlapCoreMask.Size() * 0.5f, size / overlapCoreMask.Size(), SpriteEffects.None, 0f);
		}

		private static Texture2D CreateOverlapMask()
		{
			const int size = 128;
			Texture2D texture = new(Main.instance.GraphicsDevice, size, size);
			Color[] pixels = new Color[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					Vector2 point = new Vector2(x, y) * (2f / (size - 1)) - Vector2.One;
					float noise = Perlin(point.X * 4.2f, point.Y * 4.2f, 194u) * 0.17f;
					float radius = point.Length() + noise;
					float alpha = MathHelper.Clamp((0.92f - radius) / 0.22f, 0f, 1f);
					pixels[y * size + x] = Color.White * alpha;
				}
			}
			texture.SetData(pixels);
			return texture;
		}
	}
}
