using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.NPCs
{
	/// <summary>
	/// 敌人身上的全局修改：减速、鬼火灼烧的伤害结算。
	/// </summary>
	public class GhosteyeGlobalNPC : GlobalNPC
	{
		public override void PostAI(NPC npc)
		{
			// AI 跑完之后再改速度，才不会被 AI 覆盖。
			// 0.88 是每帧乘的系数，配合各种怪自己的摩擦，大约相当于减速 30%。
			if (npc.HasBuff(ModContent.BuffType<EyeSlow>()))
			{
				npc.velocity *= 0.88f;
			}
		}

		/// <summary>
		/// 鬼火灼烧的持续伤害。层数越高烧得越快：
		/// 4 层 30/秒，5~6 层 60/秒，7 层以上 150/秒。
		/// lifeRegen 的单位是每帧累积值，每秒伤害等于 -lifeRegen / 2。
		/// 初版给的 10/20/50 实测太刮，整体提高三倍。
		/// </summary>
		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (!npc.HasBuff(ModContent.BuffType<GhostFlame>())) return;

			int tier = GhosteyePlayer.CalculateTier();

			int dps;
			if (tier >= 7) dps = 150;
			else if (tier >= 5) dps = 60;
			else dps = 30;

			// 先把正向回血清零，否则高回血的怪会把伤害吃掉。
			if (npc.lifeRegen > 0) npc.lifeRegen = 0;

			npc.lifeRegen -= dps * 2;

			// damage 决定跳字大小，给个合理值。
			if (damage < dps / 2) damage = dps / 2;
		}
	}
}
