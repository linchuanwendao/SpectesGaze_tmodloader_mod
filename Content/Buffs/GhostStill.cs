using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Buffs
{
	/// <summary>
	/// 静止：被鬼域定住的生物完全不能动。
	/// 速度清零和位置锁定都在 GhosteyeGlobalNPC.PostAI 里执行，
	/// 因为 buff 的 Update 跑在 AI 之前，改了也会被 AI 覆盖。
	/// </summary>
	public class GhostStill : ModBuff
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

			// 身上飘出静止的冷色粒子。
			if (Main.rand.NextBool(6))
			{
				float x = npc.position.X + Main.rand.Next(npc.width);
				float y = npc.position.Y + Main.rand.Next(npc.height);

				Dust d = Dust.NewDustDirect(new Vector2(x, y), 1, 1, DustID.IceTorch, 0f, 0f, 0, default, 0.9f);
				d.noGravity = true;
				d.velocity *= 0.15f;
			}
		}
	}
}
