using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Projectiles
{
	/// <summary>
	/// 主动大激光。朝鼠标方向持续一条完整的光束，没有前摇，按下就是全长全宽。
	///
	/// 生命周期的坑（实测踩过）：不要用 Projectile.timeLeft 计时。它在某些流程里会被重置，
	/// 弹幕一两帧就没了，表现就是"瞬发"。改成 ai[1] 自己累加，timeLeft 只给一个大值兜底。
	///
	/// 判定也不走碰撞体。光束长 1600，做这么大的判定框既不精确也吃性能，
	/// 改成每帧拿线段和敌人矩形求交。
	/// </summary>
	public class GhostBeam : ModProjectile
	{
		/// <summary>光束最大长度。</summary>
		public const float MaxLength = 1600f;

		/// <summary>光束的基准视觉厚度。</summary>
		public const float Thickness = 80f;

		/// <summary>持续帧数。90 帧 = 1.5 秒。</summary>
		public const int Duration = 90;

		public override string Texture => "SpectesGaze/Content/Projectiles/GhostBeam";

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.netImportant = true;

			// 只做兜底，真正的存活由 ai[1] 控制。
			Projectile.timeLeft = 600;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.active || player.dead)
			{
				Projectile.Kill();
				return;
			}

			// 自己计时，不依赖 timeLeft。
			Projectile.ai[1]++;
			if (Projectile.ai[1] > Duration)
			{
				Projectile.Kill();
				return;
			}

			// 只有发射者决定方向，其他人跟随同步过来的 velocity。
			if (Projectile.owner == Main.myPlayer)
			{
				Projectile.velocity = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
			}

			Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);

			// 直接改 position，不用 Center 赋值，避免和 netcode 打架。
			Projectile.position = player.Center - Projectile.Size / 2f;
			Projectile.rotation = aim.ToRotation();
			Projectile.ai[0] = MaxLength;

			if (!Main.dedServ)
			{
				for (int i = 0; i < 12; i++)
					Lighting.AddLight(player.Center + aim * (MaxLength * i / 12f), 0.67f, 0.11f, 0.20f);
			}

			DealBeamDamage(player, aim);
		}

		/// <summary>线段与敌人矩形求交，命中就结算伤害。8 帧无敌间隔防止每帧重复打。</summary>
		private void DealBeamDamage(Player player, Vector2 aim)
		{
			Vector2 start = player.Center;
			Vector2 end = start + aim * MaxLength;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;
				if (npc.immune[Projectile.owner] > 0) continue;
				if (!Collision.CheckAABBvLineCollision(npc.position, npc.Size, start, end)) continue;

				int dir = aim.X > 0f ? 1 : -1;
				npc.SimpleStrikeNPC(Projectile.damage, dir);
				npc.immune[Projectile.owner] = 8;

				GhosteyeEffects.Impact(npc.Center, beam: true);
			}
		}

		// 统一由 DomainRenderer 绘制整段光束。
		public override bool PreDraw(ref Color lightColor) => false;
	}
}
