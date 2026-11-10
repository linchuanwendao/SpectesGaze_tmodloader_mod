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
	}
}
