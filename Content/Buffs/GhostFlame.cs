using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	/// <summary>
	/// 鬼火灼烧：第四层鬼域起，攻击命中的敌人持续燃烧。
	///
	/// 这里不写扣血逻辑。伤害大小要跟着层数变（10 / 20 / 50 每秒），
	/// buff 本身拿不到层数，所以统一放到 GhosteyeGlobalNPC.UpdateLifeRegen 里结算。
	/// 这个类只负责视觉和标记。
	/// </summary>
	public class GhostFlame : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}

		public override void Update(NPC npc, ref int buffIndex)
		{
			if (Main.netMode == NetmodeID.Server) return;

			// 火焰粒子从敌人身上往上飘。
			if (Main.rand.NextBool(4))
			{
				Vector2 pos = npc.position + new Vector2(Main.rand.Next(npc.width), npc.height - Main.rand.Next(12));
				Vector2 vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-2.2f, -0.9f));

				Dust d = Dust.NewDustDirect(pos, 1, 1, DustID.Torch, vel.X, vel.Y, 0, default, 1.15f);
				d.noGravity = true;
			}

			if (Main.rand.NextBool(9))
			{
				Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.SolarFlare, 0f, -0.4f, 0, default, 0.9f);
				d.noGravity = true;
			}
		}
	}
}
