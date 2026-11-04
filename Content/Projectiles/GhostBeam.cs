using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Projectiles
{
	/// <summary>
	/// 主动大激光。朝鼠标方向持续 1.5 秒的光束，思路参考 ExampleLastPrismBeam。
	///
	/// 实现上没走 Projectile.velocity 的常规弹道，而是：
	/// - velocity 只存方向（单位向量）
	/// - ai[0] 存光束长度
	/// - 每帧把自身中心钉在玩家身上，方向跟鼠标走
	/// - 伤害不走碰撞体，而是手动做线段与敌人矩形求交
	///
	/// 这样做的好处是光束可以任意长（1600 像素），不需要一个巨大的碰撞箱。
	/// </summary>
	public class GhostBeam : ModProjectile
	{
		/// <summary>光束最大长度。</summary>
		public const float MaxLength = 1600f;

		/// <summary>光束视觉厚度，同时也是伤害判定的宽容范围。</summary>
		public const float Thickness = 56f;

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
			Projectile.timeLeft = 90;
			Projectile.DamageType = DamageClass.Generic;
			Projectile.netImportant = true;
			Projectile.light = 0.9f;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.active || player.dead)
			{
				Projectile.Kill();
				return;
			}

			// 只有发射者自己决定方向，其他人跟随同步过来的 velocity。
			if (Projectile.owner == Main.myPlayer)
			{
				Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
				if (Vector2.DistanceSquared(dir, Projectile.velocity) > 0.001f)
				{
					Projectile.velocity = dir;
					Projectile.netUpdate = true;
				}
			}

			Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);

			// 钉在玩家身上，跟着走。
			Projectile.Center = player.Center;
			Projectile.rotation = aim.ToRotation();
			Projectile.ai[0] = MaxLength;

			// 粒子：沿光束撒，让整条线看起来在烧。
			if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
			{
				float t = Main.rand.NextFloat();
				Vector2 pos = Projectile.Center + aim * (t * MaxLength);
				Dust d = Dust.NewDustDirect(pos, 1, 1, DustID.Blood, 0f, 0f, 0, default, 1.3f);
				d.noGravity = true;
				d.velocity *= 0.2f;
			}

			DealBeamDamage(player, aim);
		}

		/// <summary>
		/// 手动做线段与敌人矩形求交，命中就结算伤害。
		/// 不用弹幕碰撞体，因为光束长 1600，做一个这么大的判定框既不精确也很吃性能。
		/// </summary>
		private void DealBeamDamage(Player player, Vector2 aim)
		{
			if (Projectile.owner != Main.myPlayer && Main.netMode == NetmodeID.MultiplayerClient) return;

			Vector2 start = Projectile.Center;
			Vector2 end = start + aim * MaxLength;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;

				// 无敌帧挡住时跳过，避免每帧都打。
				if (npc.immune[Projectile.owner] > 0) continue;

				if (!Collision.CheckAABBvLineCollision(npc.position, npc.Size, start, end)) continue;

				int dir = aim.X > 0f ? 1 : -1;
				npc.SimpleStrikeNPC(Projectile.damage, dir);
				npc.immune[Projectile.owner] = 8;

				if (Main.netMode != NetmodeID.Server)
				{
					for (int k = 0; k < 5; k++)
					{
						Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
						Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.2f);
						d.noGravity = true;
					}
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;

			Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			Vector2 start = Projectile.Center;
			float length = Projectile.ai[0] <= 0f ? MaxLength : Projectile.ai[0];

			// 呼吸，让光束不是死板的静态条。
			float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 22f);

			// 贴图左边缘中点作为原点，旋转后从左端伸出去。
			Vector2 origin = new Vector2(0f, tex.Height / 2f);
			float rot = aim.ToRotation();

			// 三层叠加：外层宽而淡，内层窄而亮，模拟发光。
			DrawLayer(tex, start, origin, rot, length, Thickness * 1.6f, new Color(120, 10, 20) * (0.45f * pulse));
			DrawLayer(tex, start, origin, rot, length, Thickness, new Color(210, 40, 50) * (0.7f * pulse));
			DrawLayer(tex, start, origin, rot, length, Thickness * 0.4f, new Color(255, 190, 190) * (0.85f * pulse));

			return false;
		}

		private static void DrawLayer(Texture2D tex, Vector2 start, Vector2 origin, float rot, float length, float thickness, Color color)
		{
			Vector2 scale = new Vector2(length / tex.Width, thickness / tex.Height);
			Main.spriteBatch.Draw(tex, start, null, color, rot, origin, scale, SpriteEffects.None, 0f);
		}
	}
}
