using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Projectiles;

namespace SpectesGaze.Content.Systems
{
	/// <summary>
	/// 鬼域和鬼眼激光的可视化，统一在这里画。
	///
	/// 坐标系结论（试错五次后确认）：
	/// - 位置：PostDrawTiles，先 try End 掉现有批次，再 Begin 一个不带矩阵的新批次，
	///   坐标自己换算成屏幕坐标（世界坐标 - Main.screenPosition）。
	/// - 不要给 Begin 传 TransformationMatrix，那样世界坐标会被当成屏幕坐标用，画到屏幕外。
	/// - 不要在 PostDrawInterface 里算世界坐标，那个阶段 screenPosition 已经不可靠。
	/// - 弹幕的 PreDraw 这条路过不通（实测有伤害但画不出来），所以激光也挪到这里画。
	/// </summary>
	public class DomainRenderer : ModSystem
	{
		/// <summary>圆环贴图里那个环的半径，单位像素。缩放时用它做基准。</summary>
		private const float TextureRingRadius = 500f;

		private static bool _reportedError;

		public override void PostDrawTiles()
		{
			if (Main.dedServ) return;

			Player player = Main.LocalPlayer;
			GhosteyePlayer mp = player?.GetModPlayer<GhosteyePlayer>();

			bool hasDomain = player != null && player.active && mp != null && mp.Equipped && mp.DomainOn;
			bool hasBeam = AnyBeamActive();

			if (!hasDomain && !hasBeam) return;

			try
			{
				try { Main.spriteBatch.End(); } catch { }

				Main.spriteBatch.Begin(
					SpriteSortMode.Deferred,
					BlendState.AlphaBlend,
					Main.DefaultSamplerState,
					DepthStencilState.None,
					Main.Rasterizer);

				if (hasDomain) DrawDomain(player);
				if (hasBeam) DrawBeams();

				Main.spriteBatch.End();
			}
			catch (Exception e)
			{
				if (!_reportedError)
				{
					_reportedError = true;
					Main.NewText("[鬼眼] 渲染异常：" + e.Message, 255, 120, 120);
				}
			}
		}

		// ================= 鬼域 =================

		private static void DrawDomain(Player player)
		{
			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			Vector2 center = player.Center - Main.screenPosition;

			Texture2D ring = ModContent.Request<Texture2D>("SpectesGaze/Content/Textures/Effects/DomainRing").Value;
			Texture2D core = ModContent.Request<Texture2D>("SpectesGaze/Content/Textures/Effects/DomainCore").Value;

			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
			float intensity = 0.62f + 0.26f * pulse;

			Main.spriteBatch.Draw(
				ring,
				center,
				null,
				Color.White * intensity,
				0f,
				ring.Size() / 2f,
				radius / TextureRingRadius,
				SpriteEffects.None,
				0f);

			Main.spriteBatch.Draw(
				core,
				center,
				null,
				new Color(255, 255, 255) * 0.26f,
				Main.GlobalTimeWrappedHourly * 0.25f,
				core.Size() / 2f,
				0.42f + 0.06f * pulse,
				SpriteEffects.None,
				0f);

			// 头顶鬼眼：第三层起出现，随层数变大。
			if (tier >= 3)
			{
				Texture2D overhead = ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{tier}").Value;

				float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.5f) * 5f;
				Vector2 eyePos = center + new Vector2(0f, -player.height / 2f - 54f + bob);

				Main.spriteBatch.Draw(
					overhead,
					eyePos,
					null,
					Color.White * 0.92f,
					0f,
					overhead.Size() / 2f,
					1.0f + tier * 0.06f + pulse * 0.05f,
					SpriteEffects.None,
					0f);
			}
		}

		// ================= 激光 =================

		private static bool AnyBeamActive()
		{
			int type = ModContent.ProjectileType<GhostBeam>();

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == type) return true;
			}

			return false;
		}

		private static void DrawBeams()
		{
			int type = ModContent.ProjectileType<GhostBeam>();
			Texture2D tex = ModContent.Request<Texture2D>("SpectesGaze/Content/Projectiles/GhostBeam").Value;

			// 贴图左边缘中点做原点，旋转后从左端伸出去。
			Vector2 origin = new Vector2(0f, tex.Height / 2f);
			float flicker = 0.82f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != type) continue;

				Vector2 aim = p.velocity.SafeNormalize(Vector2.UnitX);
				Vector2 start = p.Center - Main.screenPosition;
				float rot = aim.ToRotation();

				// 头尾各 6 帧淡入淡出，避免硬切。
				float t = p.ai[1];
				float fade = 1f;
				if (t < 6f) fade = t / 6f;
				else if (t > GhostBeam.Duration - 6f) fade = Math.Max(0f, (GhostBeam.Duration - t) / 6f);

				DrawBeamLayer(tex, start, origin, rot, GhostBeam.MaxLength, GhostBeam.Thickness * 1.7f, new Color(110, 8, 18) * (0.5f * flicker * fade));
				DrawBeamLayer(tex, start, origin, rot, GhostBeam.MaxLength, GhostBeam.Thickness, new Color(215, 45, 55) * (0.78f * flicker * fade));
				DrawBeamLayer(tex, start, origin, rot, GhostBeam.MaxLength, GhostBeam.Thickness * 0.36f, new Color(255, 205, 205) * (0.9f * flicker * fade));
			}
		}

		private static void DrawBeamLayer(Texture2D tex, Vector2 start, Vector2 origin, float rot, float length, float thickness, Color color)
		{
			Vector2 scale = new Vector2(length / tex.Width, thickness / tex.Height);
			Main.spriteBatch.Draw(tex, start, null, color, rot, origin, scale, SpriteEffects.None, 0f);
		}

		// ================= 边缘渐晕（屏幕坐标） =================

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			if (Main.dedServ) return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active) return;

			GhosteyePlayer mp = player.GetModPlayer<GhosteyePlayer>();
			if (!mp.Equipped || !mp.DomainOn) return;

			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);

			float edgeRatio = radius / (Math.Min(Main.screenWidth, Main.screenHeight) * 0.5f);
			if (edgeRatio <= 0.75f) return;

			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
			float intensity = (0.62f + 0.26f * pulse) * MathHelper.Clamp((edgeRatio - 0.75f) * 2f, 0f, 1f);

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			const int layers = 6;

			for (int i = 0; i < layers; i++)
			{
				float alpha = 0.055f * (1f - i / (float)layers) * intensity;
				int thickness = 8 + i * 10;
				Color c = new Color(190, 18, 30) * alpha;

				spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, thickness), c);
				spriteBatch.Draw(pixel, new Rectangle(0, Main.screenHeight - thickness, Main.screenWidth, thickness), c);
				spriteBatch.Draw(pixel, new Rectangle(0, 0, thickness, Main.screenHeight), c);
				spriteBatch.Draw(pixel, new Rectangle(Main.screenWidth - thickness, 0, thickness, Main.screenHeight), c);
			}
		}
	}
}
