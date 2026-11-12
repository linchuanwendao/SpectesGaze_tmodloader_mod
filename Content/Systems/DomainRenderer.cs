using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Projectiles;
using static SpectesGaze.Content.Systems.FleshNoise;

namespace SpectesGaze.Content.Systems
{
	public class DomainRenderer : ModSystem
	{
		internal static readonly Color Crimson = new(221, 28, 57);
		internal static readonly Color Ember = new(248, 114, 127);
		internal static readonly Color Pale = new(249, 210, 196);
		internal static readonly Color Still = new(136, 106, 133);
		internal static readonly Color Violet = new(159, 65, 118);
		internal static readonly Color Blood = new(162, 9, 22);
		private static readonly Color Dried = new(85, 0, 0);
		private static readonly Color Shadow = new(34, 4, 23);
		private static readonly Color PaintShadow = new(78, 2, 14);
		private static readonly Color PaintBlood = new(178, 10, 29);
		private static readonly Color PaintCrimson = new(231, 27, 45);
		private static readonly Color PaintHighlight = new(255, 114, 85);
		private static readonly Color DomainEdgeShadow = new(17, 4, 13);
		private static readonly Color DomainEdgeBlood = new(187, 22, 43);
		private static readonly Color DomainEdgePale = new(255, 224, 219);

		private static Texture2D stain;
		private static Texture2D domainEdgeStrip;
		private int domainAge;
		private float DomainFade => MathHelper.SmoothStep(0f, 1f, (domainAge + 1) / 23f);

		public override void Unload()
		{
			if (Main.dedServ) return;

			Main.QueueMainThreadAction(() =>
			{
				stain?.Dispose();
				stain = null;
				domainEdgeStrip?.Dispose();
				domainEdgeStrip = null;
				DomainBloodBackdrop.Unload();
				FleshPrimitives.Unload();
			});
		}

		public override void OnWorldUnload() => domainAge = 0;

		public override void PostUpdateEverything()
		{
			if (!Main.dedServ) domainAge = HasDomain(Main.LocalPlayer) ? Math.Min(22, domainAge + 1) : 0;
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
			int tier = hasDomain ? GhosteyePlayer.CalculateTier() : 0;
			float domainRadius = hasDomain ? GhosteyePlayer.DomainRadiusOf(tier) : 0f;
			int beamType = ModContent.ProjectileType<GhostBeam>();
			int boltType = ModContent.ProjectileType<GhostBolt>();
			Rectangle worldView = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
			worldView.Inflate(192, 192);
			float time = Main.GlobalTimeWrappedHourly;
			float overlap = hasDomain ? OverlapOpacity(player.GetModPlayer<GhosteyePlayer>().OverlapTime) : 0f;
			Vector2? firingEye = null;
			bool hasVisibleBolt = false;

			SpriteBatch batch = Main.spriteBatch;
			Matrix view = Main.GameViewMatrix.TransformationMatrix;
			FleshPrimitives.Begin(Main.screenPosition);
			BeginSpriteBatch(batch, view);
			try
			{
				if (hasDomain)
				{
					DomainBloodBackdrop.Draw(batch, player.Center, domainRadius, time, DomainFade);
					DrawDomainEdges(player.Center, domainRadius, tier, time, DomainFade, worldView, overlap);
				}

				// 活跃弹幕先占用顶点预算，施法残影过多时也能看见光束。
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile projectile = Main.projectile[i];
					if (!projectile.active) continue;
					if (projectile.type == beamType)
					{
						DrawBeam(projectile, time, worldView);
						if (projectile.owner == Main.myPlayer) firingEye = projectile.Center;
					}
					else if (projectile.type == boltType && worldView.Intersects(projectile.Hitbox))
					{
						hasVisibleBolt = true;
					}
				}

				if (firingEye.HasValue && tier == 0) tier = GhosteyePlayer.CalculateTier();
				if (tier >= 3 && (hasDomain || firingEye.HasValue))
				{
					Vector2 eyePos = firingEye ?? player.GetModPlayer<GhosteyePlayer>().FloatingEyeCenter;
					QueueEyeTendons(eyePos, tier, time, firingEye.HasValue ? 1f : DomainFade, player.whoAmI);
				}

				ModContent.GetInstance<DomainGrab>().Draw(time);
				if (hasDomain) DrawDomainMotes(player.Center, domainRadius, tier, time, DomainFade);
				GhosteyeEffects.Draw(time, worldView);
				ModContent.GetInstance<DomainRewind>().DrawPlayback(time, worldView);
				DrawNpcEffects(time, worldView);
			}
			finally
			{
				batch.End();
			}

			// Primitive strips need their own effect. Submit them between the two sprite batches.
			FleshPrimitives.Flush(Main.instance.GraphicsDevice, view);
			bool drawEye = tier >= 3 && (hasDomain || firingEye.HasValue);
			if (!drawEye && !hasVisibleBolt) return;

			BeginSpriteBatch(batch, view);
			try
			{
				if (hasVisibleBolt)
				{
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile projectile = Main.projectile[i];
						if (projectile.active && projectile.type == boltType && worldView.Intersects(projectile.Hitbox))
							DrawBolt(projectile);
					}
				}
				if (drawEye)
				{
					Vector2 eyePos = firingEye ?? player.GetModPlayer<GhosteyePlayer>().FloatingEyeCenter;
					DrawEye(batch, eyePos, tier, time, firingEye.HasValue ? 1f : DomainFade, overlap, player.whoAmI);
				}
			}
			finally
			{
				batch.End();
			}
		}

		private static void BeginSpriteBatch(SpriteBatch batch, Matrix view) =>
			batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
				DepthStencilState.None, Main.Rasterizer, null, view);

		private static float OverlapOpacity(int time)
		{
			if (time <= 0) return 0f;
			float edge = Math.Min((GhosteyePlayer.OverlapDuration - time + 1) / 12f, time / 24f);
			return MathHelper.SmoothStep(0f, 1f, edge);
		}

		private static void DrawDomainEdges(Vector2 center, float radius, int tier, float time, float fade,
			Rectangle view, float overlap)
		{
			Color edgeColor = tier >= GhosteyePlayer.AwakenedTier ? DomainEdgePale
				: Color.Lerp(DomainEdgeBlood, new Color(158, 33, 69), (tier - 1) / 9f);
			DrawDomainEdge(center, radius, time, edgeColor, fade, view);
			if (tier >= GhosteyePlayer.OverlapTier && overlap > 0f)
			{
				Vector2 echo = new(Perlin(time * 2.1f, 0.6f, 172u), Perlin(2.43f, time * 1.8f, 1u));
				DrawDomainEdge(center + echo * (radius * 0.07f), radius * 0.81f, -time * 0.79f,
					Color.Lerp(PaintBlood, Violet, 0.4f), fade * overlap * 0.58f, view);
			}
		}

		private static void DrawDomainMotes(Vector2 center, float radius, int tier, float time, float fade)
		{
			Color red = DomainColor(tier);
			int motes = 8 + tier;
			for (uint i = 1; i <= motes; i++)
			{
				float angle = Unit(i * 457u) * MathHelper.TwoPi + Perlin(i, time * 0.51f, 41u) * 0.23f;
				float depth = Unit(i * 113u);
				float phase = time * (0.18f + Unit(i * 2953u) * 0.28f) + Unit(i * 23u);
				float fall = phase - MathF.Floor(phase);
				Vector2 point = center + angle.ToRotationVector2() * radius * (0.21f + 0.72f * depth)
					+ Vector2.UnitY * (fall * fall * 38f);
				float life = Math.Min(fall * 5f, 1f - fall);
				DrawGlow(point, new Vector2(3f + 3f * depth, 5f + 12f * fall),
					Light(red, fade * life * 0.57f), angle * 0.19f);
			}
		}

		private static Color DomainColor(int tier) => Color.Lerp(PaintCrimson, Violet, (tier - 1) / 26f);

		// 沿用 4cb3fd0 的撕裂轮廓和向内弯折的血丝，绘制在本地血色底纹之上。
		private static void DrawDomainEdge(Vector2 center, float radius, float time, Color color, float opacity, Rectangle view)
		{
			const int segments = 96;
			Vector2 previous = center + Vector2.UnitX * (radius * DomainEdgeScale(0f, time));
			for (int i = 1; i <= segments; i++)
			{
				float angle = MathHelper.TwoPi * i / segments;
				Vector2 radial = angle.ToRotationVector2();
				Vector2 point = center + radial * (radius * DomainEdgeScale(angle, time));
				if (Visible(previous, point, view))
				{
					float fray = 0.5f + 0.5f * MathF.Sin(angle * 19f - time * 1.8f + MathF.Sin(angle * 7f) * 2f);
					DrawDomainEdgeLine(previous, point, 11f + radius * 0.058f, DomainEdgeShadow * (0.60f * opacity));
					DrawDomainEdgeLine(previous, point, 3f + fray * (4f + radius * 0.013f),
						Light(color, opacity * (0.19f + fray * 0.63f)));

					if (i % 4 == 0)
					{
						Vector2 tangent = new(-radial.Y, radial.X);
						float length = radius * (0.025f + 0.10f * fray);
						float curl = MathF.Sin(angle * 13f - time * 1.1f);
						Vector2 bend = point - radial * length * 0.46f + tangent * curl * length * 0.47f;
						Vector2 tip = point - radial * length - tangent * curl * length * 0.28f;
						DrawDomainEdgeLine(point, bend, 3f + fray * 5f, Light(color, opacity * fray * 0.72f));
						DrawDomainEdgeLine(bend, tip, 2f + fray * 2f, Light(color, opacity * fray * 0.45f));
					}
				}
				previous = point;
			}
		}

		private static float DomainEdgeScale(float angle, float time) => 0.99f
			+ 0.017f * MathF.Sin(9f * angle - time * 1.1f)
			+ 0.011f * MathF.Sin(27f * angle + time * 1.5f)
			+ 0.005f * MathF.Sin(43f * angle - time * 2f);

		private static void DrawDomainEdgeLine(Vector2 from, Vector2 to, float width, Color color)
		{
			Vector2 delta = to - from;
			float length = delta.Length();
			if (length < 0.01f) return;

			domainEdgeStrip ??= CreateDomainEdgeStrip();
			Main.spriteBatch.Draw(domainEdgeStrip, (from + to) / 2f - Main.screenPosition, null, color,
				delta.ToRotation(), domainEdgeStrip.Size() / 2f,
				new Vector2(length + Math.Min(width * 0.12f, 2f), width) / domainEdgeStrip.Size(), SpriteEffects.None, 0f);
		}

		private static Texture2D CreateDomainEdgeStrip()
		{
			const int height = 32;
			Color[] pixels = new Color[2 * height];
			for (int y = 0; y < height; y++)
			{
				float distance = 2f * y / (height - 1) - 1f;
				float falloff = MathHelper.SmoothStep(0f, 1f, 1f - MathF.Abs(distance));
				pixels[2 * y] = pixels[2 * y + 1] = Color.White * falloff;
			}

			Texture2D texture = new(Main.instance.GraphicsDevice, 2, height);
			texture.SetData(pixels);
			return texture;
		}

		private static void QueueEyeTendons(Vector2 center, int tier, float time, float fade, int playerId)
		{
			float eyeSize = EyeSize(tier, time);
			uint playerSeed = unchecked((uint)playerId * 14179u + 115u);
			for (uint i = 1; i <= 6; i++)
			{
				uint seed = playerSeed + i * 5419u;
				float angle = 0.12f + Unit(seed) * 3.06f;
				Vector2 radial = angle.ToRotationVector2();
				float length = 18f + Unit(seed + 12u) * 36f;
				Vector2 root = center + radial * new Vector2(eyeSize * 0.36f, eyeSize * 0.23f);
				Vector2 tip = root + radial * length + Vector2.UnitY * (12f + Unit(seed + 5u) * 10f);
				tip.X += Perlin(i * 0.84f, time * (1.5f + Unit(seed + 14u)), seed) * 29f;
				Color color = Color.Lerp(Dried, Violet, Unit(seed + 2u) * 0.65f);
				DrawVein(root, tip, 3f + Unit(seed + 8u) * 7f, time, seed, color * (fade * 0.86f), 13f);
				if (Unit(seed + 9u) > 0.47f)
				{
					Vector2 branch = Vector2.Lerp(root, tip, 0.42f);
					DrawVein(branch, tip + new Vector2(radial.Y * 19f, -7f), 2.6f, time, seed + 3u,
						Blood * (fade * 0.56f), 8f);
				}
			}
		}

		private static float EyeSize(int tier, float time) => 44f + tier * 3f + Perlin(time * 2f, 0.67f, 43u) * 6f;

		private static void DrawEye(SpriteBatch batch, Vector2 center, int tier, float time, float fade, float overlap, int playerId)
		{
			float eyeSize = EyeSize(tier, time);
			Vector2 eyePos = center - Main.screenPosition;
			uint eyeSeed = unchecked((uint)playerId * 10691u + 1871u);
			if (overlap > 0f)
			{
				// Each echo has its own wander, rotation and rate. Only the center eye fixes its gaze on the player.
				for (uint i = 1; i <= 8; i++)
				{
					uint seed = eyeSeed + i * 5477u;
					float angle = Unit(seed) * MathHelper.TwoPi;
					float drift = 0.38f + Unit(seed + 94u) * 1.48f;
					Vector2 offset = angle.ToRotationVector2() * (33f + Unit(seed + 7u) * 68f);
					offset.Y *= 0.62f;
					offset += new Vector2(Perlin(i + 0.12f, time * drift, seed), Perlin(time * drift, i + 0.58f, seed + 5u)) * 29f;
					float scale = 0.26f + Unit(seed + 2u) * 0.34f;
					float rotation = (Unit(seed + 3u) - 0.5f) * 1.5f + Perlin(time * 1.2f, i * 0.91f, seed) * 0.46f;
					float opacity = overlap * fade * (0.19f + Unit(seed + 4u) * 0.33f);
					ProceduralGhosteye.Draw(batch, eyePos + offset, new Vector2(eyeSize * scale), tier,
						time * drift + i * 0.21f, new Color(199, 104, 132) * opacity, rotation, unchecked((int)seed));
				}
			}

			float twitch = Perlin(time * 4.5f, 0.46f, eyeSeed) * 0.24f;
			ProceduralGhosteye.Draw(batch, eyePos, new Vector2(eyeSize), tier, time, Color.White * fade,
				twitch, unchecked((int)eyeSeed));
		}

		internal static void DrawTornEdge(Vector2 center, float radius, float time, Color color, float opacity, Rectangle view)
		{
			if (radius < 1f || opacity <= 0f) return;

			int segments = Math.Clamp((int)(radius * 0.26f), 22, 92);
			float width = Math.Clamp(radius * 0.022f, 2.6f, 19f);
			FleshPrimitives.Section previous = EdgeSection(center, Vector2.UnitX, radius, width, time, color, opacity);
			for (int i = 1; i <= segments; i++)
			{
				uint seed = unchecked((uint)i * 7919u + 7229u);
				float offset = i == segments ? 0f : (Unit(seed) - 0.5f) * 0.65f;
				float turn = (i + offset) / segments;
				Vector2 radial = i == segments ? Vector2.UnitX : (turn * MathHelper.TwoPi).ToRotationVector2();
				FleshPrimitives.Section current = EdgeSection(center, radial, radius, width, time, color, opacity);
				if (Visible(previous.Center, current.Center, view))
				{
					FleshPrimitives.Connect(previous, current);
					if (i < segments && Unit(seed + 17u) > 0.37f)
					{
						float irregularity = Unit(seed + 4u);
						Color gleam = Light(Color.Lerp(color, Ember, irregularity), opacity * (0.22f + irregularity * 0.44f));
						Vector2 start = previous.Center + previous.Normal * (previous.Right * 0.47f);
						Vector2 end = current.Center + current.Normal * (current.Right * 0.48f);
						float strokeWidth = 1.4f + irregularity * 2.1f;
						DrawVein(start, end, strokeWidth, time, seed, gleam, 4f);
					}
					if (radius > 25f && Unit(seed + 38u) > 0.76f)
					{
						float length = Math.Min(121f, radius * (0.06f + Unit(seed + 71u) * 0.13f));
						Vector2 tangent = new(-radial.Y, radial.X);
						Vector2 tip = current.Center - radial * length + tangent * ((Unit(seed + 13u) - 0.5f) * length);
						Color root = Color.Lerp(Dried, color, 0.45f);
						DrawVein(current.Center, tip, width * (0.42f + Unit(seed + 23u) * 0.3f), time, seed,
							root * (0.66f * opacity), length * 0.42f);
					}
				}
				previous = current;
			}
		}

		private static FleshPrimitives.Section EdgeSection(Vector2 center, Vector2 radial, float radius, float width,
			float time, Color color, float opacity)
		{
			float y = radial.Y - time * 0.47f;
			float wave = Perlin(radial.X * 3.4f + time * 0.61f, y * 2.2f, 91u);
			float tear = Perlin(radial.X * 18f - time * 1.8f, radial.Y * 18f + time * 1.45f, 762u);
			Vector2 point = center + radial * (radius * (1f + wave * 0.19f + tear * 0.066f));
			float left = width * (0.67f + wave * 0.54f);
			float right = width * (0.56f + tear * 0.61f);
			Color tissue = Color.Lerp(Shadow, color, 0.28f + Math.Abs(tear) * 0.49f) * (opacity * 0.72f);
			return new FleshPrimitives.Section(point, radial, left, right, tissue);
		}

		private static void DrawBeam(Projectile projectile, float time, Rectangle view)
		{
			Vector2 start = projectile.Center;
			Vector2 aim = projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 end = start + aim * GhostBeam.MaxLength;
			if (!Visible(start, end, view)) return;

			float age = projectile.ai[1];
			float fade = MathHelper.Clamp(Math.Min(age / 4f, (GhostBeam.Duration - age) / 12f), 0f, 1f);
			if (fade <= 0f) return;

			uint seed = unchecked((uint)projectile.identity * 7187u + (uint)projectile.owner * 7927u + 872u);
			Vector2 normal = new(-aim.Y, aim.X);
			DrawGlow(start, new Vector2(86f, 69f), PaintCrimson * (fade * 0.76f), aim.ToRotation());
			const int segments = 69;
			float strokeStart = Unit(seed + 653u);
			FleshPrimitives.Section lastTissue = new(start, normal, 12f, 16f, PaintShadow * fade, PaintBlood * fade, strokeStart);
			FleshPrimitives.Section lastCut = new(start, normal, 3f, 7f, PaintBlood * fade, PaintCrimson * fade, strokeStart + 0.37f);
			for (int i = 1; i <= segments; i++)
			{
				uint branchSeed = seed + (uint)i * 3571u;
				float unevenStep = i == segments ? 0f : (Unit(branchSeed) - 0.5f) * 0.71f;
				float along = (i + unevenStep) / segments;
				float baseWidth = GhostBeam.Thickness * (0.31f + MathF.Sin(along * MathHelper.Pi) * 0.17f);
				float twist = Perlin(along * 19f, time * 13f, seed) + 0.45f * Perlin(along * 58f, time * 24f, seed + 74u);
				float leftWidth = baseWidth * (0.43f + Unit(branchSeed + 91u) * 0.76f);
				float rightWidth = baseWidth * (0.3f + Unit(branchSeed + 3u) * 0.95f);
				float skin = Perlin(along * 34f + time * 8f, i * 0.32f, seed + 29u);
				float deposit = MathHelper.Clamp(0.48f + skin * 0.85f + (Unit(branchSeed + 76u) - 0.5f) * 0.29f, 0f, 1f);
				float ending = MathHelper.Clamp((1f - along) * 11f, 0.06f, 1f);
				float opacity = fade * ending;
				Vector2 center = Vector2.Lerp(start, end, along) + normal * (twist * 26f * ending);
				Color shadow = Color.Lerp(PaintShadow, PaintBlood, deposit * 0.37f) * (opacity * 0.94f);
				Color flesh = Color.Lerp(PaintBlood, PaintCrimson, 0.19f + deposit * 0.63f) * opacity;
				Color ridge = Color.Lerp(PaintCrimson, PaintHighlight, deposit * deposit * 0.86f) * (opacity * 0.93f);
				float brushTravel = strokeStart + along * (GhostBeam.MaxLength / 144f);
				var section = new FleshPrimitives.Section(center, normal, leftWidth * ending, rightWidth * ending, shadow, flesh, brushTravel);
				var cut = new FleshPrimitives.Section(center + normal * (8.5f * twist + 3.7f * skin), normal,
					leftWidth * (0.24f + deposit * 0.17f) * ending, rightWidth * (0.19f + deposit * 0.21f) * ending,
					flesh, ridge, brushTravel + 0.37f);

				if (Visible(lastTissue.Center, section.Center, view))
				{
					FleshPrimitives.Connect(lastTissue, section);
					FleshPrimitives.Connect(lastCut, cut);
					if (Unit(branchSeed + 19u) > 0.59f)
					{
						float side = Unit(branchSeed + 27u) > 0.52f ? -1f : 1f;
						float reach = (11f + Unit(branchSeed + 13u) * 48f) * ending;
						Vector2 root = center + normal * (side * 8f);
						Vector2 tip = root + normal * (side * reach) + aim * ((Unit(branchSeed + 46u) - 0.61f) * 73f);
						DrawVein(root, tip, 3.6f + deposit * 7f, time, branchSeed,
							Color.Lerp(PaintShadow, PaintCrimson, deposit) * (opacity * 0.89f), reach * 0.33f);
					}
					if (Unit(branchSeed + 57u) > 0.31f)
					{
						float brushOffset = Unit(branchSeed + 103u) - 0.5f;
						Vector2 from = lastTissue.Center + normal * (brushOffset * lastTissue.Left);
						Vector2 to = center + normal * (brushOffset * (leftWidth + rightWidth) * 0.85f * ending);
						Color stroke = Color.Lerp(PaintBlood, PaintHighlight, Unit(branchSeed + 59u) * 0.87f);
						DrawVein(from, to, 2.2f + deposit * 4.5f, time, branchSeed + 114u,
							stroke * (opacity * (0.49f + deposit * 0.43f)), 6.4f);
					}
				}
				lastTissue = section;
				lastCut = cut;
			}
		}

		private static void DrawBolt(Projectile projectile)
		{
			uint seed = unchecked((uint)projectile.identity * 4079u + (uint)projectile.owner * 6599u + 11u);
			Vector2 halfSize = projectile.Size / 2f;
			for (int i = 1; i < projectile.oldPos.Length; i++)
			{
				if (projectile.oldPos[i] == Vector2.Zero) break;

				uint fleckSeed = seed + (uint)i * 11u;
				if (Unit(fleckSeed) < 0.41f) continue;

				float fade = 1f - i / (float)projectile.oldPos.Length;
				Vector2 point = projectile.oldPos[i] + halfSize + Vector2.UnitY * (i * i * 0.19f);
				float spread = (Unit(fleckSeed + 5u) - 0.5f) * i * 2f;
				point.X += spread;
				Vector2 size = new((7f + Unit(fleckSeed + 1u) * 12f) * fade, (6f + Unit(fleckSeed + 3u) * 12f) * fade);
				GhosteyeEffects.DrawFlesh(point, size, Blood * (0.53f * fade),
					projectile.rotation + Unit(fleckSeed + 7u) * 2.9f, unchecked((int)fleckSeed));
			}

			float pinch = 0.75f + 0.25f * Unit(seed + (uint)(projectile.timeLeft / 3) * 13u);
			GhosteyeEffects.DrawFlesh(projectile.Center, new Vector2(25f, 16f) * pinch, Ember,
				projectile.rotation + (Unit(seed + 21u) - 0.5f) * 0.78f, 4 + (int)(seed & 3));
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
				Vector2 center = npc.Center;
				uint seed = unchecked((uint)i * 3719u + 911u);
				if (npc.HasBuff(flameType))
				{
					for (uint j = 1; j <= 4; j++)
					{
						float phase = time * (0.51f + Unit(seed + j * 14u) * 0.78f) + Unit(seed + j * 21u);
						float rise = phase - MathF.Floor(phase);
						float heat = Math.Min(rise * 6f, 1f - rise);
						Vector2 tongue = center + new Vector2((Unit(seed + j * 51u) - 0.5f) * size,
							size * (0.43f - 1.26f * rise));
						DrawGlow(tongue, new Vector2(10f + heat * 9f, 23f + rise * 11f),
							Light(j % 2 == 0 ? Violet : Crimson, heat * 0.73f), rise * 0.61f);
					}
				}

				if (npc.HasBuff(stillType))
				{
					DrawGlow(center, new Vector2(size * 1.65f, size * 2.1f), Shadow * 0.34f, 0.26f);
				}
				else if (npc.HasBuff(slowType))
				{
					Vector2 foot = center + Vector2.UnitY * (npc.height * 0.49f);
					DrawVein(foot - new Vector2(size * 0.42f, 8f), foot + new Vector2(size * 0.61f, 4f),
						4.6f, time, seed, Violet * 0.57f, 7f);
				}

				if (npc.HasBuff(gazeType))
				{
					Vector2 mark = center - Vector2.UnitY * (npc.height * 0.5f + 10f);
					DrawVein(mark + new Vector2(-12f, -4f), mark + new Vector2(9f, 5f), 4.4f,
						time, seed + 73u, Light(Crimson, 0.74f), 6f);
				}
			}
		}

		private static bool Visible(Vector2 a, Vector2 b, Rectangle view) =>
			Math.Max(a.X, b.X) >= view.Left && Math.Min(a.X, b.X) <= view.Right
			&& Math.Max(a.Y, b.Y) >= view.Top && Math.Min(a.Y, b.Y) <= view.Bottom;

		internal static Color Light(Color color, float intensity) => (color * intensity) with { A = 0 };

		internal static void DrawLine(Vector2 from, Vector2 to, float width, Color color) =>
			DrawVein(from, to, width, Main.GlobalTimeWrappedHourly, 212u, color, width * 0.66f);

		private static void DrawVein(Vector2 from, Vector2 to, float width, float time, uint seed, Color color, float curl)
		{
			Vector2 delta = to - from;
			float length = delta.Length();
			if (length < 0.03f || width <= 0.05f) return;

			Vector2 normal = new(-delta.Y / length, delta.X / length);
			int segments = Math.Clamp((int)(length / 9f), 2, 9);
			float drift = time * 3.9f + from.Y * 0.014f;
			FleshPrimitives.Section previous = new(from, normal, width * 0.54f, width * 0.41f, color);
			for (int i = 1; i <= segments; i++)
			{
				float u = i / (float)segments;
				float wave = Perlin(u * 5.3f + from.X * 0.019f, drift, seed);
				float ragged = Unit(seed + (uint)i * 71u);
				float taper = (1f - u) * (0.71f + 0.51f * ragged) + 0.035f;
				Vector2 point = Vector2.Lerp(from, to, u) + normal * (curl * wave * MathF.Sin(MathHelper.Pi * u));
				var section = new FleshPrimitives.Section(point, normal, width * taper * 0.51f,
					width * taper * (0.29f + ragged * 0.28f), color * (1f - u * 0.67f));
				FleshPrimitives.Connect(previous, section);
				previous = section;
			}
		}

		private static void DrawGlow(Vector2 center, Vector2 size, Color color, float rotation = 0f)
		{
			stain ??= CreateStain();
			Main.spriteBatch.Draw(stain, center - Main.screenPosition, null, color, rotation,
				stain.Size() / 2f, size / stain.Size(), SpriteEffects.None, 0f);
		}

		private static Texture2D CreateStain()
		{
			const int size = 96;
			Texture2D texture = new(Main.instance.GraphicsDevice, size, size);
			Color[] pixels = new Color[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float u = x * (2f / (size - 1)) - 1f;
					float v = y * (2f / (size - 1)) - 1f;
					float radius = MathF.Sqrt(u * u + v * v);
					float warp = Perlin(u * 3.7f + 3.18f, v * 4.9f + 1.86f, 984u) * 0.49f;
					float fibers = Perlin(u * 9f + v * 6f, v * 23f + 0.2f, 871u);
					float mask = MathHelper.Clamp(1f - (radius + warp) * 1.32f, 0f, 1f);
					float edge = MathHelper.Clamp((1f - Math.Max(Math.Abs(u), Math.Abs(v))) * 9f, 0f, 1f);
					float opacity = mask * mask * (0.77f + fibers * 0.45f) * edge;
					pixels[y * size + x] = Color.White * opacity;
				}
			}
			texture.SetData(pixels);
			return texture;
		}
	}
}
