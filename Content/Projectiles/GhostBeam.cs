using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

		/// <summary>光束视觉厚度，也是伤害判定的宽容范围。</summary>
		public const float Thickness = 150f;

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
			Projectile.light = 0.9f;

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

			if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
			{
				float t = Main.rand.NextFloat();
				Vector2 pos = player.Center + aim * (t * MaxLength);
				Dust d = Dust.NewDustDirect(pos, 1, 1, DustID.Blood, 0f, 0f, 0, default, 1.3f);
				d.noGravity = true;
				d.velocity *= 0.2f;
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

				if (Main.netMode != NetmodeID.Server)
				{
					for (int k = 0; k < 6; k++)
					{
						Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
						Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.3f);
						d.noGravity = true;
					}
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

			Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 start = Projectile.Center;
			float length = Projectile.ai[0] <= 0f ? MaxLength : Projectile.ai[0];

			// 快频闪烁，看起来像在烧。
			float flicker = 0.82f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

			// 前 6 帧和后 6 帧做淡入淡出，避免硬切。
			float fade = 1f;
			float t = Projectile.ai[1];
			if (t < 6f) fade = t / 6f;
			else if (t > Duration - 6f) fade = Math.Max(0f, (Duration - t) / 6f);

			// 贴图左边缘中点做原点，旋转后从左端伸出去。
			Vector2 origin = new Vector2(0f, tex.Height / 2f);
			float rot = aim.ToRotation();

			// 三层叠加：外层宽而淡，内层窄而亮，模拟发光。
			DrawLayer(tex, start, origin, rot, length, Thickness * 1.7f, new Color(110, 8, 18) * (0.5f * flicker * fade));
			DrawLayer(tex, start, origin, rot, length, Thickness, new Color(215, 45, 55) * (0.78f * flicker * fade));
			DrawLayer(tex, start, origin, rot, length, Thickness * 0.36f, new Color(255, 205, 205) * (0.9f * flicker * fade));

			return false;
		}

		private static void DrawLayer(Texture2D tex, Vector2 start, Vector2 origin, float rot, float length, float thickness, Color color)
		{
			Vector2 scale = new Vector2(length / tex.Width, thickness / tex.Height);
			Main.spriteBatch.Draw(tex, start, null, color, rot, origin, scale, SpriteEffects.None, 0f);
		}
	}
}
