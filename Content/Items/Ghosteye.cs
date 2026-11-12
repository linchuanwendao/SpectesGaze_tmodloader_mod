using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Items
{
	/// <summary>
	/// 鬼眼。不是饰品，是消耗品：用一次就固化到玩家身上，之后再也拿不下来。
	/// 物品栏和地面图标由程序绘制，瞳孔会短促抽动。
	/// </summary>
	public class Ghosteye : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 48;
			Item.height = 48;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.noUseGraphic = true;
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

		public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
		{
			ProceduralGhosteye.Draw(spriteBatch, position, new Vector2(48f) * scale,
				GhosteyePlayer.CalculateTier(), Main.GlobalTimeWrappedHourly, drawColor, seed: 271);
			return false;
		}

		public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
		{
			Vector2 pos = Item.Center - Main.screenPosition;
			ProceduralGhosteye.Draw(spriteBatch, pos, new Vector2(48f) * scale,
				GhosteyePlayer.CalculateTier(), Main.GlobalTimeWrappedHourly, lightColor, rotation, whoAmI);
			return false;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			int tier = GhosteyePlayer.CalculateTier();
			bool equipped = Main.LocalPlayer.GetModPlayer<GhosteyePlayer>().Equipped;

			Color tierColor = tier switch
			{
				>= 10 => new Color(255, 199, 177),
				>= 7 => new Color(231, 75, 81),
				>= 4 => new Color(194, 87, 112),
				_ => new Color(180, 124, 126)
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
