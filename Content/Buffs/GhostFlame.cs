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
	/// 这里负责标记，火焰由 DomainRenderer 绘制。
	/// </summary>
	public class GhostFlame : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}
	}
}
