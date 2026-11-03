using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	/// <summary>
	/// 复苏值条 + 鬼眼装备格。
	/// 这两样都不属于物品栏，是屏幕 HUD，所以画在 PostDrawInterface 里。
	/// </summary>
	public class GhosteyeUI : ModSystem
	{
		// HUD 定位。左上角是原版 UI 的空白区，不会和血条、buff 打架。
		private static readonly Vector2 Anchor = new Vector2(24f, 90f);

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			if (Main.dedServ) return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active) return;

			GhosteyePlayer mp = player.GetModPlayer<GhosteyePlayer>();
			if (!mp.Equipped) return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// ---- 装备格 ----
			Rectangle slot = new Rectangle((int)Anchor.X, (int)Anchor.Y, 56, 56);
			spriteBatch.Draw(pixel, slot, new Color(28, 6, 10) * 0.88f);
			DrawBorder(spriteBatch, pixel, slot, new Color(150, 40, 50) * 0.9f);

			int tier = GhosteyePlayer.CalculateTier();
			Texture2D eye = ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{tier}").Value;
			Vector2 eyeCenter = new Vector2(slot.X + slot.Width / 2f, slot.Y + slot.Height / 2f);
			spriteBatch.Draw(eye, eyeCenter, null, Color.White, 0f, eye.Size() / 2f, 1f, SpriteEffects.None, 0f);

			// ---- 复苏值条 ----
			int barX = slot.Right + 10;
			int barY = slot.Y + 14;
			int barW = 180;
			int barH = 16;

			Rectangle barBack = new Rectangle(barX, barY, barW, barH);
			spriteBatch.Draw(pixel, barBack, new Color(38, 8, 12) * 0.88f);

			float ratio = mp.Revival / GhosteyePlayer.MaxRevival;
			Rectangle barFill = new Rectangle(barX, barY, (int)(barW * ratio), barH);
			spriteBatch.Draw(pixel, barFill, new Color(214, 40, 52));

			DrawBorder(spriteBatch, pixel, barBack, new Color(120, 30, 40) * 0.9f);

			// 数值
			string label = $"复苏 {(int)mp.Revival}";
			Utils.DrawBorderString(spriteBatch, label, new Vector2(barX + barW + 8, barY - 2), Color.White, 0.75f);

			// 层数
			Utils.DrawBorderString(spriteBatch, $"第 {tier} 层", new Vector2(barX, barY - 22f), new Color(220, 150, 150), 0.8f);

			// 鬼域状态
			if (mp.DomainOn)
			{
				Utils.DrawBorderString(spriteBatch, "鬼域 开启中", new Vector2(barX, barY + barH + 4f), new Color(230, 90, 90), 0.75f);
			}
			else if (mp.DomainCooldown > 0)
			{
				int sec = mp.DomainCooldown / 60 + 1;
				Utils.DrawBorderString(spriteBatch, $"鬼眼闭合 {sec}s", new Vector2(barX, barY + barH + 4f), new Color(150, 130, 130), 0.75f);
			}
		}

		/// <summary>画一个空心矩形边框。用四条 MAGIC PIXEL 拼，一像素宽。</summary>
		private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color)
		{
			sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
			sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
			sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
			sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
		}
	}
}
