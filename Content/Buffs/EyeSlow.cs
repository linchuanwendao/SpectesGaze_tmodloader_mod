using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	/// <summary>减速：被鬼眼看见的敌人行动变慢。真正的减速在 GhosteyeGlobalNPC.PostAI 里执行。</summary>
	public class EyeSlow : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			// 护士不能提前驱散，否则打 Boss 时一个治疗就把减速洗了。
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}

		public override void Update(NPC npc, ref int buffIndex)
		{
			// 这里只做视觉。速度修改放在 PostAI，因为 buff 更新发生在 AI 之前，
			// 那时候改 velocity 会被同一帧的 AI 覆盖掉。
			if (Main.netMode != Terraria.ID.NetmodeID.Server && Main.rand.NextBool(8))
			{
				Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, Terraria.ID.DustID.Blood, 0f, 0f, 0, default, 0.9f);
				d.noGravity = true;
				d.velocity *= 0.3f;
			}
		}
	}
}
