using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	/// <summary>
	/// 鬼域范围的可视化。
	///
	/// 坐标系踩了四次坑，结论记在这里，别再改回去了：
	/// 1. PostDrawTiles 阶段传给 Begin 的自定义矩阵不可靠，用 Main.GameViewMatrix.TransformationMatrix
	///    画出来会跑到屏幕外（世界坐标 67054 这种数字直接当屏幕坐标用）。
	/// 2. 所以矩阵一律用默认（Identity），坐标自己换算：世界坐标 - Main.screenPosition。
	///    实测玩家中心 67054,6571 减屏幕偏移 66094,6031 得到 960,540，正好是 1920x1080 的正中，公式成立。
	/// 3. PostDrawInterface 阶段不能这么算，那个阶段 screenPosition 已经被改过。
	/// 4. 不要用几十个旋转矩形拼圆，会变成放射状光条。用预渲染贴图缩放。
	/// </summary>
	public class DomainRenderer : ModSystem
	{
		/// <summary>圆环贴图里那个环的半径，单位像素。缩放时用它做基准。</summary>
		private const float TextureRingRadius = 500f;

		// 渲染异常只报一次，避免每帧刷屏。
		private static bool _reportedError;

		public override void PostDrawTiles()
		{
			if (Main.dedServ) return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active) return;

			GhosteyePlayer mp = player.GetModPlayer<GhosteyePlayer>();
			if (!mp.Equipped || !mp.DomainOn) return;

			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);

			// 世界坐标转屏幕坐标。这个阶段 screenPosition 是可靠的。
			Vector2 screenCenter = player.Center - Main.screenPosition;

			Texture2D ring = ModContent.Request<Texture2D>("SpectesGaze/Content/Textures/Effects/DomainRing").Value;
			Texture2D core = ModContent.Request<Texture2D>("SpectesGaze/Content/Textures/Effects/DomainCore").Value;

			float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
			float intensity = 0.62f + 0.26f * pulse;
			float ringScale = radius / TextureRingRadius;

			try
			{
				// 原批次状态不确定，先关；关不掉说明本来就是关的，忽略。
				try { Main.spriteBatch.End(); } catch { }

				// 默认矩阵，坐标即屏幕像素。
				Main.spriteBatch.Begin(
					SpriteSortMode.Deferred,
					BlendState.AlphaBlend,
					Main.DefaultSamplerState,
					DepthStencilState.None,
					Main.Rasterizer);

				Main.spriteBatch.Draw(
					ring,
					screenCenter,
					null,
					Color.White * intensity,
					0f,
					ring.Size() / 2f,
					ringScale,
					SpriteEffects.None,
					0f);

				Main.spriteBatch.Draw(
					core,
					screenCenter,
					null,
					new Color(255, 255, 255) * 0.26f,
					Main.GlobalTimeWrappedHourly * 0.25f,
					core.Size() / 2f,
					0.42f + 0.06f * pulse,
					SpriteEffects.None,
					0f);

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

		/// <summary>
		/// 屏幕四边的红色渐晕。这个要的是屏幕坐标，放在 UI 层直接用屏幕宽高算。
		/// </summary>
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
