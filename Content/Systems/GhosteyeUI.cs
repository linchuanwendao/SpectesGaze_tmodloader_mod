using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
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

			bool mastered = tier >= GhosteyePlayer.AwakenedTier;
			float ratio = mastered ? 0f : mp.Revival / GhosteyePlayer.MaxRevival;
			Rectangle barFill = new Rectangle(barX, barY, (int)(barW * ratio), barH);
			spriteBatch.Draw(pixel, barFill, new Color(214, 40, 52));

			DrawBorder(spriteBatch, pixel, barBack, new Color(120, 30, 40) * 0.9f);

			// 数值
			string label = mastered ? "复苏已平息" : $"复苏 {(int)mp.Revival}";
			Utils.DrawBorderString(spriteBatch, label, new Vector2(barX + barW + 8, barY - 2), Color.White, 0.75f);

			// 层数
			Utils.DrawBorderString(spriteBatch, $"第 {tier} 层", new Vector2(barX, barY - 22f), new Color(220, 150, 150), 0.8f);

			// 状态行从条下面开始，逐行往下排，避免不同层数时位置跳动。
			float lineY = barY + barH + 4f;
			const float lineStep = 18f;

			// 鬼域
			if (mp.DomainOn)
			{
				Utils.DrawBorderString(spriteBatch, mastered ? "鬼域 完全复苏" : "鬼域 开启中",
					new Vector2(barX, lineY), mastered ? new Color(255, 219, 207) : new Color(230, 90, 90), 0.75f);
			}
			else if (mp.DomainCooldown > 0)
			{
				Utils.DrawBorderString(spriteBatch, $"鬼眼闭合 {mp.DomainCooldown / 60 + 1}s", new Vector2(barX, lineY), new Color(150, 130, 130), 0.75f);
			}
			lineY += lineStep;

			// 激光：第五层起
			if (tier >= 5)
			{
				bool ready = mp.DomainOn && mp.BeamCooldown <= 0;
				string text = !mp.DomainOn
					? "激光 需开域"
					: mp.BeamCooldown > 0
						? $"激光 冷却 {mp.BeamCooldown / 60 + 1}s"
						: "激光 就绪";

				Utils.DrawBorderString(spriteBatch, text, new Vector2(barX, lineY), ready ? new Color(255, 150, 150) : new Color(140, 120, 120), 0.72f);
				lineY += lineStep;
			}

			// 静止：第六层起
			if (tier >= 6)
			{
				bool ready = mp.DomainOn && mp.StillCooldown <= 0;
				string text = !mp.DomainOn
					? "静止 需开域"
					: mp.StillCooldown > 0
						? $"静止 冷却 {mp.StillCooldown / 60 + 1}s"
						: "静止 就绪";

				Utils.DrawBorderString(spriteBatch, text, new Vector2(barX, lineY), ready ? new Color(150, 200, 230) : new Color(140, 120, 120), 0.72f);
				lineY += lineStep;
			}

			// 重启自身：第七层起
			if (tier >= 7)
			{
				bool ready = mp.DomainOn && mp.RestartCooldown <= 0;
				string text = !mp.DomainOn
					? "重启 需开域"
					: mp.RestartCooldown > 0
						? $"重启 冷却 {mp.RestartCooldown / 60 + 1}s"
						: "重启 就绪";

				Utils.DrawBorderString(spriteBatch, text, new Vector2(barX, lineY), ready ? new Color(200, 160, 230) : new Color(140, 120, 120), 0.72f);
				lineY += lineStep;
			}

			if (tier >= DomainRewind.RequiredTier)
			{
				bool supported = Main.netMode == NetmodeID.SinglePlayer;
				bool rewinding = supported && ModContent.GetInstance<DomainRewind>().IsRewinding;
				bool ready = supported && mp.DomainOn && mp.RewindCooldown <= 0;
				string text = !supported ? "大范围重启 仅单人"
					: rewinding ? "大范围重启 回溯中"
					: !mp.DomainOn ? "大范围重启 需开域"
					: mp.RewindCooldown > 0 ? $"大范围重启 冷却 {(mp.RewindCooldown + 59) / 60}s"
					: "大范围重启 就绪";

				Utils.DrawBorderString(spriteBatch, text, new Vector2(barX, lineY), ready || rewinding ? new Color(225, 135, 179) : new Color(140, 120, 120), 0.72f);
				lineY += lineStep;
			}

			if (tier >= GhosteyePlayer.OverlapTier)
			{
				bool active = mp.DomainOn && mp.OverlapTime > 0;
				bool ready = mp.DomainOn && mp.OverlapCooldown == 0;
				string text = active
					? $"鬼域叠加 ×{mp.AttackHitCount(tier)} {(mp.OverlapTime + 59) / 60}s"
					: !mp.DomainOn ? "鬼域叠加 需开域"
					: mp.OverlapCooldown > 0 ? $"鬼域叠加 冷却 {(mp.OverlapCooldown + 59) / 60}s"
					: "鬼域叠加 就绪";

				Utils.DrawBorderString(spriteBatch, text, new Vector2(barX, lineY),
					active || ready ? new Color(250, 118, 149) : new Color(140, 120, 120), 0.72f);
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
