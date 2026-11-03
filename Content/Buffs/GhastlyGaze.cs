using Terraria;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	// 凝视：挂在敌人身上的负面效果。
	// ModBuff 有两个 Update 重载，参数是 Player 的给玩家用，参数是 NPC 的给敌人用。
	public class GhastlyGaze : ModBuff
	{
		public override void SetStaticDefaults()
		{
			// 标记为负面，这样护士驱散、部分装备的判定都会当成 debuff 处理。
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
		}

		public override void Update(NPC npc, ref int buffIndex)
		{
			// lifeRegen 的单位是每帧累积值，-20 约等于每秒 10 点伤害。
			npc.lifeRegen -= 20;

			// 被凝视的敌人移动变迟钝，但不冻结 AI，避免和后续的"静止"效果冲突。
			npc.velocity *= 0.94f;
		}
	}
}
