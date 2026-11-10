using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Projectiles;

namespace SpectesGaze.Content.Systems
{
	public class DomainRenderer : ModSystem
	{
		internal static readonly Color Crimson = new(247, 36, 79);
		internal static readonly Color Ember = new(255, 109, 129);
		internal static readonly Color Pale = new(255, 224, 219);
		internal static readonly Color Still = new(80, 196, 255);
		internal static readonly Color Violet = new(194, 111, 255);

		private const string EffectPath = "SpectesGaze/Content/Textures/Effects/";
		private const float RingTextureRadius = 0.91f;
		private static Texture2D glow;
		private static Texture2D ring;
		private static Texture2D strip;
		private int domainAge;

		public override void Unload()
		{
			Main.QueueMainThreadAction(() =>
			{
				glow?.Dispose();
				ring?.Dispose();
				strip?.Dispose();
				glow = ring = strip = null;
			});
		}

		public override void OnWorldUnload() => domainAge = 0;

		public override void PostUpdateEverything()
		{
			if (Main.dedServ) return;

			domainAge = HasDomain(Main.LocalPlayer) ? Math.Min(22, domainAge + 1) : 0;
		}

		private static bool HasDomain(Player player)
		{
			if (player == null || !player.active || player.dead) return false;
			GhosteyePlayer eye = player.GetModPlayer<GhosteyePlayer>();
			return eye.Equipped && eye.DomainOn;
		}

		public override void PostDrawTiles()
		{
			if (Main.dedServ) return;

			Player player = Main.LocalPlayer;
			bool hasDomain = HasDomain(player);
			int beamType = ModContent.ProjectileType<GhostBeam>();
			int boltType = ModContent.ProjectileType<GhostBolt>();
			Rectangle view = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
			view.Inflate(192, 192);
			// 同一批次画地形上的特效和长光束，光束起点在画面外时仍能看到扫进来的部分。
			SpriteBatch batch = Main.spriteBatch;
			batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			try
			{
				float time = Main.GlobalTimeWrappedHourly;
				if (hasDomain) DrawDomain(player, time);
				GhosteyeEffects.Draw();
				DrawNpcEffects(time, view);

				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile projectile = Main.projectile[i];
					if (!projectile.active) continue;
					if (projectile.type == beamType) DrawBeam(projectile, time, view);
					else if (projectile.type == boltType && view.Intersects(projectile.Hitbox)) DrawBolt(projectile);
				}
			}
			finally
			{
				batch.End();
			}
		}

		private void DrawDomain(Player player, float time)
		{
			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			Vector2 center = player.Center;
			float fade = MathHelper.SmoothStep(0f, 1f, (domainAge + 1) / 23f);
			float pulse = Wave(time * 2.4f);
			Color red = Color.Lerp(Crimson, Violet, Math.Max(0, tier - 4) * 0.065f);

			// 暗雾用普通透明混合，边界、碎光和粒子才叠亮。
			DrawGlow(center, new Vector2(radius * 2.05f), new Color(82, 8, 29) * (0.24f * fade));
			for (int i = 0; i < 5; i++)
			{
				float angle = time * (i % 2 == 0 ? 0.21f : -0.16f) + i * 2.4f;
				Vector2 offset = angle.ToRotationVector2() * radius * (0.3f + 0.08f * i);
				DrawGlow(center + offset, new Vector2(radius * 0.74f, radius * 0.34f),
					new Color(108, 18, 52) * (0.13f * fade), angle + MathHelper.PiOver2);
			}

			Texture2D border = ModContent.Request<Texture2D>(EffectPath + "DomainRing").Value;
			Stamp(border, center, new Vector2(radius * border.Width / 500f),
				Color.White * (0.22f * fade), time * 0.13f);
			DrawRing(center, radius, Light(red, (0.36f + 0.12f * pulse) * fade));
			DrawRing(center, radius * 0.94f, Light(red, 0.11f * fade));
			DrawOrbit(center, radius, time * 0.31f, 12 + tier * 2, red, fade * 0.75f);
			DrawOrbit(center, radius * 0.90f, -time * 0.22f, 12, red, fade * 0.28f);

			int motes = 18 + 2 * tier;
			for (int i = 0; i < motes; i++)
			{
				float drift = time * 0.32f + i * 2.39996f;
				float depth = (i * 17 % motes + 0.5f) / motes;
				Vector2 point = center + drift.ToRotationVector2() * radius * (0.18f + 0.76f * depth);
				float shine = Wave(time * 3f + i * 1.7f);
				DrawGlow(point, new Vector2(7f + 8f * depth, 2f + 4f * shine),
					Light(i % 5 == 0 ? Pale : red, fade * shine * 0.58f), drift + MathHelper.PiOver2);
			}

			Texture2D core = ModContent.Request<Texture2D>(EffectPath + "DomainCore").Value;
			Vector2 anchor = center + new Vector2(0f, player.height * 0.45f);
			Stamp(core, anchor, new Vector2(110f, 58f) * (1f + 0.14f * pulse),
				Color.White * (0.21f * fade), 0f);

			if (tier < 3) return;
			Texture2D eye = ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{tier}").Value;
			Vector2 eyePos = center + new Vector2(0f, -player.height / 2f - 54f + 5f * MathF.Sin(time * 2f));
			float eyeSize = 44f + tier * 3f + pulse * 3f;
			DrawGlow(eyePos, new Vector2(eyeSize * 2.2f, eyeSize * 1.7f), Light(red, fade * 0.42f));
			DrawOrbit(eyePos, eyeSize * 0.7f, -time * 0.85f, 8, red, fade * 0.52f);
			Stamp(eye, eyePos, new Vector2(eyeSize, eyeSize * (0.9f + pulse * 0.1f)),
				Color.White * fade, MathF.Sin(time * 1.6f) * 0.06f);
		}

		private static void DrawOrbit(Vector2 center, float radius, float phase, int count, Color color, float strength)
		{
			for (int i = 0; i < count; i++)
			{
				float angle = phase + MathHelper.TwoPi * i / count;
				float weight = Wave(i * 2.39996f + phase * 2f);
				Vector2 point = center + angle.ToRotationVector2() * radius;
				Vector2 tangent = (angle + MathHelper.PiOver2).ToRotationVector2();
				float length = 4f + radius * 0.034f * weight;
				DrawLine(point - tangent * length, point + tangent * length, 3f + weight * 5f,
					Light(i % 5 == 0 ? Pale : color, strength * (0.35f + 0.65f * weight)));
			}
		}

		private static void DrawBeam(Projectile projectile, float time, Rectangle view)
		{
			Vector2 start = projectile.Center;
			Vector2 aim = projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 end = start + aim * GhostBeam.MaxLength;
			if (Math.Max(start.X, end.X) < view.Left || Math.Min(start.X, end.X) > view.Right
				|| Math.Max(start.Y, end.Y) < view.Top || Math.Min(start.Y, end.Y) > view.Bottom) return;

			float age = projectile.ai[1];
			float fade = MathHelper.Clamp(Math.Min(age / 4f, (GhostBeam.Duration - age) / 12f), 0f, 1f);
			if (fade <= 0f) return;
			float flare = MathHelper.Clamp(1f - age / 16f, 0f, 1f);
			float width = GhostBeam.Thickness * (0.81f + 0.13f * Wave(time * 25f) + flare * 0.26f);
			DrawLine(start, end, width * 2.5f, Light(Crimson, fade * 0.31f));
			DrawLine(start, end, width * 1.35f, Light(Crimson, fade * 0.66f));
			DrawLine(start, end, width * 0.57f, Light(Ember, fade * 0.84f));
			DrawLine(start, end, width * 0.18f, Light(Pale, fade * 0.94f));

			Vector2 normal = new(-aim.Y, aim.X);
			for (int lane = -1; lane <= 1; lane += 2)
			{
				Vector2 previous = start;
				for (int i = 1; i <= 32; i++)
				{
					float u = i / 32f;
					float spread = MathF.Sin(MathHelper.Pi * u) * width * 0.52f;
					float wave = 0.63f + MathF.Sin(u * 37f - time * 23f + lane) * 0.44f;
					Vector2 point = Vector2.Lerp(start, end, u) + normal * (lane * spread * wave);
					DrawLine(previous, point, 7f + 3f * Wave(u * 41f - time * 34f),
						Light(lane == 1 ? Ember : Violet, fade * 0.70f));
					previous = point;
				}
			}

			for (int i = 0; i < 17; i++)
			{
				float cycle = time * (1.4f + (i % 3) * 0.23f) + i * 0.618f;
				float u = cycle - MathF.Floor(cycle);
				float edge = MathF.Sin(MathHelper.Pi * u);
				Vector2 point = Vector2.Lerp(start, end, u) + normal * (width * 0.75f * MathF.Sin(i * 3.8f) * edge);
				DrawGlow(point, new Vector2(65f + i % 4 * 28f, 4f + i % 3 * 2f),
					Light(i % 3 == 0 ? Pale : Ember, fade * edge * 0.8f), aim.ToRotation());
			}

			Texture2D core = ModContent.Request<Texture2D>(EffectPath + "DomainCore").Value;
			Stamp(core, start, new Vector2(width * 1.25f), Color.White * (fade * 0.78f), time * 4f);
			DrawGlow(start, new Vector2(width * 2.5f), Light(Ember, fade * (0.62f + flare * 0.3f)));
			DrawGlow(end, new Vector2(width * 0.75f), Light(Ember, fade * 0.55f));
		}

		private static void DrawBolt(Projectile projectile)
		{
			Vector2 point = projectile.Center;
			Vector2 halfSize = projectile.Size / 2f;
			for (int i = 0; i < projectile.oldPos.Length; i++)
			{
				if (projectile.oldPos[i] == Vector2.Zero) break;
				Vector2 next = projectile.oldPos[i] + halfSize;
				float weight = 1f - i / (float)projectile.oldPos.Length;
				DrawLine(next, point, 23f * weight, Light(Crimson, 0.64f * weight));
				DrawLine(next, point, 7f * weight, Light(Pale, 0.83f * weight));
				point = next;
			}

			Texture2D bolt = ModContent.Request<Texture2D>(EffectPath + "BeamBullet").Value;
			Stamp(bolt, projectile.Center, new Vector2(42f, 14f), Color.White, projectile.rotation);
			DrawGlow(projectile.Center, new Vector2(43f, 20f), Light(Ember, 0.87f), projectile.rotation);
		}

		private static void DrawNpcEffects(float time, Rectangle view)
		{
			int stillType = ModContent.BuffType<GhostStill>();
			int flameType = ModContent.BuffType<GhostFlame>();
			int slowType = ModContent.BuffType<EyeSlow>();
			int gazeType = ModContent.BuffType<GhastlyGaze>();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || !view.Intersects(npc.Hitbox)) continue;
				float size = Math.Clamp(Math.Max(npc.width, npc.height) * 0.7f, 16f, 94f);
				float phase = time + i * 0.618f;
				Vector2 center = npc.Center;

				if (npc.HasBuff(flameType))
				{
					for (int j = 0; j < 5; j++)
					{
						float rise = phase * 0.83f + j * 0.21f;
						rise -= MathF.Floor(rise);
						Vector2 tongue = center + new Vector2(MathF.Sin(j * 2.4f + rise * 4f) * size * 0.61f,
							size * (0.55f - 1.75f * rise));
						float heat = MathF.Sin(MathHelper.Pi * rise);
						DrawGlow(tongue, new Vector2(14f, 30f) * (0.7f + heat),
							Light(j % 2 == 0 ? Violet : Crimson, heat * 0.75f), MathF.Sin(phase * 4f + j) * 0.5f);
						DrawGlow(tongue, new Vector2(4f, 13f), Light(Ember, heat * 0.85f));
					}
				}

				if (npc.HasBuff(stillType))
				{
					DrawGlow(center, new Vector2(size * 2.9f), new Color(21, 111, 178) * 0.46f);
					DrawRing(center, size, Light(new Color(185, 232, 255), 0.82f));
					DrawOrbit(center, size * 1.18f, i * 2.4f, 8, Still, 0.97f);
				}
				else if (npc.HasBuff(slowType))
				{
					DrawOrbit(center + Vector2.UnitY * size * 0.42f, size * 0.7f, -phase * 1.1f, 6, Violet, 0.66f);
				}

				if (npc.HasBuff(gazeType))
				{
					Vector2 mark = center - Vector2.UnitY * (npc.height * 0.5f + 10f);
					DrawGlow(mark, new Vector2(31f, 12f), Light(Crimson, 0.73f));
					DrawGlow(mark, new Vector2(4f, 11f), Light(Pale, 0.44f + 0.32f * Wave(phase * 5f)));
				}
			}
		}

		internal static float Wave(float phase) => 0.5f + 0.5f * MathF.Sin(phase);

		// AlphaBlend 保留透明底图，同时让发光层只增加亮度。
		internal static Color Light(Color color, float intensity) => (color * intensity) with { A = 0 };

		internal static void DrawGlow(Vector2 center, Vector2 size, Color color, float rotation = 0f)
		{
			glow ??= CreateMask(64, 64, (x, y) => Falloff(MathF.Sqrt(x * x + y * y)));
			Stamp(glow, center, size, color, rotation);
		}

		internal static void DrawRing(Vector2 center, float radius, Color color)
		{
			ring ??= CreateMask(384, 384, (x, y) =>
				Falloff((MathF.Sqrt(x * x + y * y) - RingTextureRadius) / 0.055f));
			Stamp(ring, center, new Vector2(2f * radius / RingTextureRadius), color, 0f);
		}

		internal static void DrawLine(Vector2 from, Vector2 to, float width, Color color)
		{
			Vector2 delta = to - from;
			float length = delta.Length();
			if (length < 0.01f) return;

			strip ??= CreateMask(2, 32, (_, y) => Falloff(y));
			Stamp(strip, (from + to) / 2f, new Vector2(length + Math.Min(width * 0.12f, 2f), width),
				color, delta.ToRotation());
		}

		private static void Stamp(Texture2D texture, Vector2 center, Vector2 size, Color color, float rotation)
		{
			Main.spriteBatch.Draw(texture, center - Main.screenPosition, null, color, rotation,
				texture.Size() / 2f, size / texture.Size(), SpriteEffects.None, 0f);
		}

		private static float Falloff(float distance)
		{
			float edge = MathHelper.Clamp(1f - MathF.Abs(distance), 0f, 1f);
			return edge * edge * (3f - 2f * edge);
		}

		private static Texture2D CreateMask(int width, int height, Func<float, float, float> profile)
		{
			Texture2D texture = new(Main.instance.GraphicsDevice, width, height);
			Color[] pixels = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					pixels[y * width + x] = Color.White * profile(2f * x / (width - 1) - 1f, 2f * y / (height - 1) - 1f);
				}
			}
			texture.SetData(pixels);
			return texture;
		}
	}
}
