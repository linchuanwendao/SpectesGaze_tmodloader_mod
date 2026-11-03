using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Items
{
	/// <summary>
	/// 鬼眼。不是饰品，是消耗品：用一次就固化到玩家身上，之后再也拿不下来。
	/// 图标按当前层数动态切换，直接读 Textures/Eyes 下的十张图。
	/// </summary>
	public class Ghosteye : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 48;
			Item.height = 48;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.maxStack = 1;
			Item.consumable = true;
			Item.value = Item.sellPrice(gold: 20);
			Item.rare = ItemRarityID.Purple;
			Item.UseSound = SoundID.Item4;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return null;

			GhosteyePlayer mp = player.GetModPlayer<GhosteyePlayer>();
			if (mp.Equipped)
			{
				// 已经吞过了，不消耗物品。
				Main.NewText("鬼眼已经在你身上了。", 160, 40, 40);
				return false;
			}

			mp.Equip();
			return true;
		}

		public override void UpdateInventory(Player player)
		{
			// 稀有度跟着层数走，玩家在物品栏里能直接看出成长。
			int tier = GhosteyePlayer.CalculateTier();
			Item.rare = tier switch
			{
				>= 10 => ItemRarityID.Master,
				>= 7 => ItemRarityID.Red,
				>= 4 => ItemRarityID.Purple,
				_ => ItemRarityID.Blue
			};
		}

		// ---- 动态图标 ----

		private static Texture2D EyeTexture(int tier)
		{
			int index = Math.Clamp(tier, 1, 10);
			return ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{index}").Value;
		}

		public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
		{
			Texture2D tex = EyeTexture(GhosteyePlayer.CalculateTier());
			spriteBatch.Draw(tex, position, null, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
			return false;
		}

		public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
		{
			Texture2D tex = EyeTexture(GhosteyePlayer.CalculateTier());
			Vector2 pos = Item.Center - Main.screenPosition;
			spriteBatch.Draw(tex, pos, null, lightColor, rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0f);
			return false;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			int tier = GhosteyePlayer.CalculateTier();
			bool equipped = Main.LocalPlayer.GetModPlayer<GhosteyePlayer>().Equipped;

			Color tierColor = tier switch
			{
				>= 10 => new Color(255, 215, 0),
				>= 7 => new Color(220, 60, 60),
				>= 4 => new Color(190, 100, 220),
				_ => new Color(120, 160, 220)
			};

			tooltips.Add(new TooltipLine(Mod, "GhosteyeTier", $"第 {tier} 层") { OverrideColor = tierColor });

			if (!equipped)
			{
				tooltips.Add(new TooltipLine(Mod, "GhosteyeUse", "使用后与鬼眼融合，无法取下"));
				tooltips.Add(new TooltipLine(Mod, "GhosteyeCost", "融合时失去当前生命值的 80%") { OverrideColor = new Color(200, 80, 80) });
			}
		}

		public override void AddRecipes()
		{
			// 临时配方，测试用。正式版换成设定相关的材料。
			Recipe recipe = CreateRecipe();
			recipe.AddIngredient(ItemID.DirtBlock, 10);
			recipe.AddTile(TileID.WorkBenches);
			recipe.Register();
		}
	}
}
