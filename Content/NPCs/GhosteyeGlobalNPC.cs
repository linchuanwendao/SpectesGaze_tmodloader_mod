using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;

namespace SpectesGaze.Content.NPCs
{
	/// <summary>
	/// 敌人身上的全局修改。目前只处理减速。
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
	}
}
