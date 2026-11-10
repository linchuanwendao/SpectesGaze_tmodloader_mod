using Terraria;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	// 鬼域：玩家身上的增益。
	// 这个 buff 由 GhosteyePlayer 每帧续 2 帧来维持常驻，所以关闭鬼域后会自动消失。
	// 因为一直续期，时间显示会一直是 0 秒，用 buffNoTimeDisplay 隐藏掉更干净。
	public class GazeDomain : ModBuff
	{
		public override void SetStaticDefaults()
		{
			// 死亡或退出重进后不保留，避免状态残留。
			Main.buffNoSave[Type] = true;
			// 不是负面效果，护士不会驱散它。
			Main.debuff[Type] = false;
			// 隐藏倒计时，因为它是无限续期的。
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.moveSpeed += 0.2f;
			player.noKnockback = true;

			player.GetDamage(DamageClass.Generic) += 0.15f;
			player.GetCritChance(DamageClass.Generic) += 8f;
		}
	}
}
