using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Projectiles
{
	/// <summary>
	/// 随鬼眼移动的持续光束。伤害由发射端使用整条线段判定。
	/// </summary>
	public class GhostBeam : ModProjectile
	{
		/// <summary>光束最大长度。</summary>
		public const float MaxLength = 1600f;

		/// <summary>光束的基准视觉厚度。</summary>
		public const float Thickness = 80f;

		/// <summary>持续帧数。90 帧 = 1.5 秒。</summary>
		public const int Duration = 90;
		private Vector2 lastSyncedAim;

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

		// velocity 表示瞄准方向，位置由鬼眼每帧提供。
		public override bool ShouldUpdatePosition() => false;

		// 避免光束起点的弹幕矩形再算一次接触伤害。
		public override bool? CanDamage() => false;

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

			GhosteyePlayer eye = player.GetModPlayer<GhosteyePlayer>();
			Vector2 start = eye.FloatingEyeCenter;

			bool ownsBeam = Projectile.owner == Main.myPlayer;
			if (ownsBeam)
			{
				Projectile.velocity = (Main.MouseWorld - start).SafeNormalize(Vector2.UnitX);
				if (Main.netMode == NetmodeID.MultiplayerClient && (int)Projectile.ai[1] % 6 == 0
					&& Vector2.Dot(Projectile.velocity, lastSyncedAim) < 0.9998f)
				{
					lastSyncedAim = Projectile.velocity;
					Projectile.netUpdate = true;
				}
			}

			Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);

			Projectile.Center = start;
			Projectile.rotation = aim.ToRotation();
			Projectile.ai[0] = MaxLength;

			if (!Main.dedServ)
			{
				int age = (int)Projectile.ai[1];
				GhosteyeEffects.EmitBeamSpatter(start, aim, MaxLength, age, Projectile.identity);
				Vector2 normal = new(-aim.Y, aim.X);
				uint seed = (uint)Projectile.identity;
				for (int i = 0; i < 8; i++)
				{
					float fray = FleshNoise.Perlin(i * 0.91f, age * 0.14f, seed);
					float along = (i + 0.33f + 0.57f * FleshNoise.Unit(seed + (uint)i * 593u)) * (MaxLength / 8f);
					float strength = MathHelper.Clamp(0.24f + fray * 0.19f, 0.09f, 0.35f);
					Lighting.AddLight(start + aim * along + normal * (fray * 27f),
						strength, strength * 0.11f, strength * 0.23f);
				}
			}

			if (ownsBeam) DealBeamDamage(eye, start, aim);
		}

		/// <summary>线段与敌人矩形求交，命中就结算伤害。8 帧无敌间隔防止每帧重复打。</summary>
		private void DealBeamDamage(GhosteyePlayer eye, Vector2 start, Vector2 aim)
		{
			Vector2 end = start + aim * MaxLength;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.dontTakeDamage) continue;
				if ((npc.friendly || npc.immortal) && npc.type != NPCID.TargetDummy) continue;
				if (npc.immune[Projectile.owner] > 0) continue;
				if (!Collision.CheckAABBvLineCollision(npc.position, npc.Size, start, end)) continue;

				int dir = aim.X > 0f ? 1 : -1;
				NPC.HitInfo hit = npc.CalculateHitInfo(Projectile.damage, dir);
				int damageDone = npc.StrikeNPC(hit);
				if (Main.netMode != NetmodeID.SinglePlayer) NetMessage.SendStrikeNPC(npc, hit);
				npc.immune[Projectile.owner] = 8;

				eye.RecordDps(npc, damageDone);
				eye.ApplyEchoHits(npc, hit, damageDone);
				GhosteyeEffects.Impact(npc.Center, beam: true);
			}
		}

		// 统一由 DomainRenderer 绘制整段光束。
		public override bool PreDraw(ref Color lightColor) => false;
	}
}
