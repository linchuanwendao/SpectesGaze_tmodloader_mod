using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Projectiles
{
	/// <summary>
	/// 被动小激光。鬼眼自己打出去的光弹，直线飞行，轻微追踪。
	/// 不消耗复苏值，由 GhosteyePlayer 定时发射。
	/// </summary>
	public class GhostBolt : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 140;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.light = 0.6f;
		}

		public override void AI()
		{
			// 贴图是横向的，所以旋转直接取速度方向，不需要再转 90 度。
			Projectile.rotation = Projectile.velocity.ToRotation();

			// 轻微追踪：找到最近的敌人，每帧把方向往它偏一点。
			// 转向角度很小，不会变成制导导弹，只是让远距离不至于完全打空。
			NPC target = FindTarget();
			if (target != null)
			{
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired * Projectile.velocity.Length(), 0.06f);
			}

			if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 0, default, 0.9f);
				d.noGravity = true;
				d.velocity *= 0.3f;
			}
		}

		private NPC FindTarget()
		{
			NPC best = null;
			float bestDist = 420f * 420f;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;

				float dist = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = npc;
				}
			}

			return best;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.netMode == NetmodeID.Server) return;

			for (int i = 0; i < 10; i++)
			{
				Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
				Dust d = Dust.NewDustDirect(Projectile.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.1f);
				d.noGravity = true;
			}
		}
	}
}
