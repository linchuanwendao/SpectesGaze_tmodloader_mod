using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Projectiles
{
	/// <summary>
	/// 被动小激光。鬼眼自己打出去的光弹，直线飞行，轻微追踪。
	/// 不消耗复苏值，由 GhosteyePlayer 定时发射。
	/// </summary>
	public class GhostBolt : ModProjectile
	{
		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 11;
			ProjectileID.Sets.TrailingMode[Type] = 0;
		}

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

			if (!Main.dedServ)
				Lighting.AddLight(Projectile.Center, 0.58f, 0.09f, 0.17f);
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

		public override void OnKill(int timeLeft) => GhosteyeEffects.Impact(Projectile.Center, beam: false);

		public override bool PreDraw(ref Color lightColor) => false;
	}
}
