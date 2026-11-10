using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.NPCs
{
	/// <summary>
	/// 敌人身上的全局修改：减速、鬼火灼烧、静止。
	/// </summary>
	public class GhosteyeGlobalNPC : GlobalNPC
	{
		// 静止期间锁定的位置。key 是 npc.whoAmI。
		private static readonly Dictionary<int, Vector2> FrozenPositions = new();

		public override void PostAI(NPC npc)
		{
			bool slowed = npc.HasBuff(ModContent.BuffType<EyeSlow>());
			bool still = npc.HasBuff(ModContent.BuffType<GhostStill>());

			// 静止优先级最高，减速不用再叠。
			if (still)
			{
				// 第一次进入静止时记住位置，之后每帧强制拉回去。
				// 只清 velocity 不够，有些 AI 直接改 position（瞬移类）。
				if (!FrozenPositions.TryGetValue(npc.whoAmI, out Vector2 pos))
				{
					pos = npc.position;
					FrozenPositions[npc.whoAmI] = pos;
				}

				npc.position = pos;
				npc.velocity = Vector2.Zero;
				return;
			}

			// 脱离静止后清掉记录，避免 whoAmI 复用导致位置错乱。
			FrozenPositions.Remove(npc.whoAmI);

			if (slowed)
			{
				// AI 跑完之后再改速度，才不会被 AI 覆盖。
				// 0.88 配合各种怪自己的摩擦，大约相当于减速 30%。
				npc.velocity *= 0.88f;
			}
		}

		/// <summary>清空冻结记录。世界切换或重载时用。</summary>
		public static void ClearHistory()
		{
			FrozenPositions.Clear();
		}

		/// <summary>
		/// 鬼火灼烧的持续伤害。层数越高烧得越快：
		/// 4 层 30/秒，5~6 层 60/秒，7 层以上 150/秒。
		/// lifeRegen 的单位是每帧累积值，每秒伤害等于 -lifeRegen / 2。
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
