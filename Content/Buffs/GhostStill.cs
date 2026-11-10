using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	/// <summary>
	/// 静止：被鬼域定住的生物完全不能动。
	/// GhosteyeGlobalNPC.PostAI 锁定位置，DomainRenderer 绘制锁环。
	/// </summary>
	public class GhostStill : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoSave[Type] = true;
			BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
		}
	}
}
