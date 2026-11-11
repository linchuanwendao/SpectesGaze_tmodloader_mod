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
		internal static readonly Color Still = new(132, 151, 176);
		internal static readonly Color Violet = new(194, 111, 255);
		internal static readonly Color Blood = new(187, 22, 43);
		private static readonly Color Shadow = new(17, 4, 13);

		private const string EffectPath = "SpectesGaze/Content/Textures/Effects/";
		private const float RingTextureRadius = 0.91f;
		private static Texture2D glow;
		private static Texture2D ring;
		private static Texture2D strip;
		private int domainAge;
		private float DomainFade => MathHelper.SmoothStep(0f, 1f, (domainAge + 1) / 23f);

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
				float overlap = 0f;
				if (hasDomain)
				{
					int overlapTime = player.GetModPlayer<GhosteyePlayer>().OverlapTime;
					if (overlapTime > 0)
					{
						float edge = Math.Min((GhosteyePlayer.OverlapDuration - overlapTime + 1) / 12f, overlapTime / 24f);
						overlap = MathHelper.SmoothStep(0f, 1f, edge);
					}
					DrawDomain(player, time, view, overlap);
				}
				GhosteyeEffects.Draw(time, view);
				ModContent.GetInstance<DomainRewind>().DrawPlayback(time, view);
				DrawNpcEffects(time, view);

				Vector2? firingEye = null;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile projectile = Main.projectile[i];
					if (!projectile.active) continue;
					if (projectile.type == beamType)
					{
						DrawBeam(projectile, time, view);
						if (projectile.owner == Main.myPlayer) firingEye = projectile.Center;
					}
					else if (projectile.type == boltType && view.Intersects(projectile.Hitbox)) DrawBolt(projectile);
				}

				// 鬼眼最后画，发射时不会被光束起点的红雾盖住。
				if (hasDomain || firingEye.HasValue)
				{
					int tier = GhosteyePlayer.CalculateTier();
					if (tier >= 3)
					{
						Vector2 eyePos = firingEye ?? player.GetModPlayer<GhosteyePlayer>().FloatingEyeCenter;
						DrawEye(eyePos, tier, time, firingEye.HasValue ? 1f : DomainFade, overlap);
					}
				}
			}
			finally
			{
				batch.End();
			}
		}

		private void DrawDomain(Player player, float time, Rectangle view, float overlap)
		{
			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			Vector2 center = player.Center;
			float fade = DomainFade;
			float pulse = DomainPulse(time);
			Color red = DomainColor(tier);
			bool awakened = tier >= GhosteyePlayer.AwakenedTier;

			DrawGlow(center, new Vector2(radius * 2.15f), Shadow * ((awakened ? 0.55f : 0.36f) * fade));
			for (int i = 0; i < 9; i++)
			{
				float phase = i * 2.39996f;
				float angle = phase + 0.3f * MathF.Sin(time * 0.4f + phase);
				float drift = Wave(time * (0.6f + i * 0.07f) + phase);
				Vector2 offset = angle.ToRotationVector2() * radius * (0.2f + 0.55f * drift);
				DrawGlow(center + offset, new Vector2(radius * 0.92f, radius * 0.28f),
					new Color(64, 7, 26) * (fade * (0.17f + 0.18f * drift)), angle + 0.7f);
			}

			DrawTornEdge(center, radius, time, awakened ? Pale : red, fade, view);
			if (tier >= GhosteyePlayer.OverlapTier)
			{
				Vector2 echo = new(MathF.Sin(time * 1.1f) * 16f, MathF.Cos(time * 0.83f) * 11f);
				DrawTornEdge(center + echo, radius * (0.91f - overlap * 0.1f), -time * 0.72f,
					Blood, fade * (0.44f + overlap * 0.46f), view);
				if (overlap > 0f)
				{
					DrawTornEdge(center - echo, radius * 0.62f, time * 0.67f, red, fade * overlap * 0.68f, view);
				}
			}

			int motes = 14 + 2 * tier;
			for (int i = 0; i < motes; i++)
			{
				float phase = i * 2.39996f;
				float drift = phase + 0.22f * MathF.Sin(time * 0.72f + phase);
				float depth = (i * 17 % motes + 0.5f) / motes;
				float fall = Wave(time * 0.94f + phase);
				Vector2 point = center + drift.ToRotationVector2() * radius * (0.16f + 0.73f * depth)
					+ Vector2.UnitY * (fall * 26f);
				DrawGlow(point, new Vector2(3f + 4f * depth, 6f + 11f * fall),
					Light(awakened && i % 3 == 0 ? Pale : red, fade * fall * 0.45f), MathF.Sin(phase) * 0.4f);
			}

			Texture2D core = ModContent.Request<Texture2D>(EffectPath + "DomainCore").Value;
			Vector2 anchor = center + new Vector2(0f, player.height * 0.45f);
			Stamp(core, anchor, new Vector2(110f, 58f) * (1f + 0.14f * pulse),
				Color.White * (0.21f * fade), 0f);
		}

		private static void DrawEye(Vector2 eyePos, int tier, float time, float fade, float overlap)
		{
			Texture2D eye = ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{tier}").Value;
			Color red = DomainColor(tier);
			float eyeSize = 44f + tier * 3f + DomainPulse(time) * 3f;
			if (overlap > 0f)
			{
				// 八个浮动的残影围住真正的鬼眼。
				for (int i = 0; i < 8; i++)
				{
					float angle = i * MathHelper.TwoPi / 8f + MathF.Sin(time * 1.4f + i) * 0.16f;
					Vector2 offset = angle.ToRotationVector2() * new Vector2(89f, 55f) * (0.6f + overlap * 0.4f);
					float opacity = fade * overlap * (0.24f + 0.31f * Wave(time * 4f + i * 1.8f));
					Stamp(eye, eyePos + offset, new Vector2(eyeSize * 0.45f, eyeSize * 0.36f),
						new Color(239, 104, 133) * opacity, MathF.Sin(angle) * 0.35f);
				}
			}
			DrawGlow(eyePos, new Vector2(eyeSize * 2.5f, eyeSize * 2f), Shadow * (0.74f * fade));
			if (tier >= GhosteyePlayer.AwakenedTier)
				DrawGlow(eyePos, new Vector2(eyeSize * 2.05f, eyeSize * 1.25f), Light(Pale, fade * 0.37f));
			DrawGlow(eyePos, new Vector2(eyeSize * 1.6f, eyeSize * 1.3f), Light(red, fade * 0.4f));
			for (int i = -2; i <= 2; i++)
			{
				float curl = MathF.Sin(time * 2.8f + i * 2.4f);
				Vector2 root = eyePos + new Vector2(i * eyeSize * 0.17f, eyeSize * 0.19f);
				Vector2 bend = root + new Vector2(i * 3f + curl * 6f, eyeSize * 0.32f);
				Vector2 tip = bend + new Vector2(-i * 4f + curl * 7f, 9f + 5f * Math.Abs(i));
				DrawLine(root, bend, 7f, Light(red, fade * 0.55f));
				DrawLine(bend, tip, 3f, Light(red, fade * 0.34f));
			}
			float blink = MathF.Pow(Wave(time * 1.4f + MathF.Sin(time * 0.81f)), 38f);
			Stamp(eye, eyePos, new Vector2(eyeSize, eyeSize * (1f - blink * 0.67f)),
				Color.White * fade, MathF.Sin(time * 1.6f) * 0.07f);
		}

		private static float DomainPulse(float time) => Wave(time * 2.1f + MathF.Sin(time * 0.8f));

		private static Color DomainColor(int tier) => Color.Lerp(Blood, new Color(158, 33, 69), (tier - 1) / 9f);

		internal static void DrawTornEdge(Vector2 center, float radius, float time, Color color, float opacity, Rectangle view)
		{
			const int segments = 96;
			Vector2 previous = center + Vector2.UnitX * (radius * EdgeScale(0f, time));
			for (int i = 1; i <= segments; i++)
			{
				float angle = MathHelper.TwoPi * i / segments;
				Vector2 radial = angle.ToRotationVector2();
				Vector2 point = center + radial * (radius * EdgeScale(angle, time));
				// view 已包含留边，屏幕外的边缘和向内伸出的血丝可以一起跳过。
				if (!view.Contains((int)point.X, (int)point.Y) && !view.Contains((int)previous.X, (int)previous.Y))
				{
					previous = point;
					continue;
				}
				float fray = Wave(angle * 19f - time * 1.8f + MathF.Sin(angle * 7f) * 2f);
				DrawLine(previous, point, 11f + radius * 0.058f, Shadow * (0.60f * opacity));
				DrawLine(previous, point, 3f + fray * (4f + radius * 0.013f),
					Light(color, opacity * (0.19f + fray * 0.63f)));

				if (i % 4 == 0)
				{
					Vector2 tangent = new(-radial.Y, radial.X);
					float length = radius * (0.025f + 0.10f * fray);
					float curl = MathF.Sin(angle * 13f - time * 1.1f);
					Vector2 bend = point - radial * length * 0.46f + tangent * curl * length * 0.47f;
					Vector2 tip = point - radial * length - tangent * curl * length * 0.28f;
					DrawLine(point, bend, 3f + fray * 5f, Light(color, opacity * fray * 0.72f));
					DrawLine(bend, tip, 2f + fray * 2f, Light(color, opacity * fray * 0.45f));
				}
				previous = point;
			}
		}

		private static float EdgeScale(float angle, float time) => 0.99f
			+ 0.017f * MathF.Sin(9f * angle - time * 1.1f)
			+ 0.011f * MathF.Sin(27f * angle + time * 1.5f)
			+ 0.005f * MathF.Sin(43f * angle - time * 2f);

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
			float width = GhostBeam.Thickness * (0.78f + 0.12f * Wave(time * 17f) + flare * 0.24f);
			Vector2 normal = new(-aim.Y, aim.X);
			DrawLine(start, end, width * 2.2f, Shadow * (fade * 0.65f));
			DrawLine(start, end, width * 1.4f, Light(Blood, fade * 0.27f));

			// 窄而暗的裂心，两侧是宽窄交替的血色撕口。
			const int segments = 48;
			Vector2 previous = start;
			Vector2 previousLip = Vector2.Zero;
			for (int i = 1; i <= segments; i++)
			{
				float u = i / (float)segments;
				float taper = MathF.Sin(MathHelper.Pi * u);
				float twitch = MathF.Sin(u * 71f - time * 16f) + 0.37f * MathF.Sin(u * 113f + time * 24f);
				Vector2 point = Vector2.Lerp(start, end, u) + normal * (twitch * width * 0.13f * taper);
				float tear = Wave(u * 39f - time * 13f + MathF.Sin(u * 63f) * 1.9f);
				Vector2 lip = normal * (width * (0.04f + 0.14f * tear) * taper);
				DrawLine(previous, point, width * (0.28f + 0.28f * tear), Light(Blood, fade * 0.83f));
				DrawLine(previous, point, width * (0.12f + 0.13f * tear), Shadow * (fade * 0.92f));
				DrawLine(previous + previousLip, point + lip, 4f + tear * 7f,
					Light(Ember, fade * (0.24f + 0.55f * tear)));
				DrawLine(previous - previousLip * 0.7f, point - lip * 0.7f, 3f + tear * 4f,
					Light(Crimson, fade * (0.27f + 0.38f * tear)));

				if (i % 4 == 0 && i < segments)
				{
					float side = i % 8 == 0 ? -1f : 1f;
					float reach = width * side * taper * (0.46f + 0.34f * tear);
					Vector2 branch = point + aim * (7f + 17f * tear) + normal * (reach * 0.62f);
					Vector2 tip = point - aim * (16f + 21f * tear) + normal * reach;
					DrawLine(point, branch, 4f + tear * 4f, Light(Blood, fade * (0.38f + 0.46f * tear)));
					DrawLine(branch, tip, 3f, Light(Crimson, fade * (0.22f + 0.46f * tear)));
					DrawGlow(tip + Vector2.UnitY * (8f + tear * 16f), new Vector2(4f, 8f + tear * 7f),
						Light(Blood, fade * tear * 0.73f), 0.18f * side);
				}
				previous = point;
				previousLip = lip;
			}

			Texture2D core = ModContent.Request<Texture2D>(EffectPath + "DomainCore").Value;
			Stamp(core, start, new Vector2(width * 1.15f, width * (1.14f + flare * 0.45f)),
				new Color(255, 131, 148) * (fade * 0.88f), aim.ToRotation() + MathF.Sin(time * 10f) * 0.16f);
			DrawGlow(start, new Vector2(width * 1.85f), Light(Blood, fade * (0.36f + flare * 0.35f)));
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
					DrawGlow(center, new Vector2(size * 2.25f, size * 2.9f),
						new Color(32, 24, 43) * 0.38f, 0.18f);
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
