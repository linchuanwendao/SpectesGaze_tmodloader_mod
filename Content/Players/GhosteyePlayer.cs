using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using SpectesGaze.Content.Buffs;

namespace SpectesGaze.Content.Players
{
	/// <summary>
	/// 鬼眼的全部运行时状态。
	/// 为什么不用饰品槽：改 Player.armor 数组要 patch 原版 UI 和存档，和别的 mod 冲突概率高。
	/// 这里用"使用后固化"代替，状态存玩家数据里，UI 单独画一个格子显示。
	/// </summary>
	public class GhosteyePlayer : ModPlayer
	{
		// ================= 装备状态 =================

		/// <summary>是否已经吞噬鬼眼。一旦为 true 永久锁死，没有取下逻辑。</summary>
		public bool Equipped;

		// ================= 复苏值 =================

		public const float MaxRevival = 100f;

		/// <summary>0 ~ 100。越高输出越高，同时受到的伤害也越高。只涨不跌，升层时清零。</summary>
		public float Revival;

		/// <summary>输出加成比例，满值 +60%。</summary>
		public float RevivalDamageBonus => 0.6f * (Revival / MaxRevival);

		/// <summary>受伤加成比例，满值 +50%。</summary>
		public float RevivalHurtBonus => 0.5f * (Revival / MaxRevival);

		// ================= 鬼域状态 =================

		/// <summary>
		/// 鬼域开关。按一次开，再按一次关，没有持续时间限制。
		/// 用 bool 字段而不是循环：游戏每帧调用一次 Update，读字段就够，
		/// 写 while 循环会把主线程卡死。
		/// </summary>
		public bool DomainOn;

		/// <summary>鬼域锁定剩余时间（帧）。只在复苏值溢满被迫闭眼时启用。</summary>
		public int DomainCooldown;

		/// <summary>主动大激光的冷却剩余时间（帧）。</summary>
		public int BeamCooldown;

		/// <summary>静止的冷却剩余时间（帧）。</summary>
		public int StillCooldown;

		/// <summary>重启自身的冷却剩余时间（帧）。</summary>
		public int RestartCooldown;

		// 被动小激光的发射计时，累计到阈值就打一发。
		private int boltTimer;

		/// <summary>兼容旧名字，UI 和渲染器都读这个。</summary>
		public bool DomainActive => DomainOn;

		// 记录上一帧的层数，用来检测"升层"事件。
		private int lastTier;

		/// <summary>
		/// 每一层对应的鬼域半径（像素）。16 像素 = 1 格。
		/// 20 / 30 / 40 / 60 / 80 / 100 格直径，第 6 层封顶，之后不再变大。
		/// 400 格直径是没实测拍脑袋定的，屏幕根本装不下，已按实测改成 100 格。
		/// </summary>
		private static readonly float[] RadiusByTier =
		{
			160f, 240f, 320f, 480f, 640f, 800f, 800f, 800f, 800f, 800f
		};

		public static float DomainRadiusOf(int tier)
		{
			return RadiusByTier[Math.Clamp(tier - 1, 0, RadiusByTier.Length - 1)];
		}

		/// <summary>鬼域开着时每秒涨多少复苏值。</summary>
		private const float RevivalDrainPerTick = 0.025f;

		// ================= 装备流程 =================

		/// <summary>物品被使用时调用，完成"吞噬"。</summary>
		public void Equip()
		{
			if (Equipped) return;

			Equipped = true;
			Revival = 0f;

			// 记录装备时的层数，避免第一次更新时误报"升级"。
			lastTier = CalculateTier();

			// 首次佩戴强制扣掉当前血量的 80%，至少保留 1 点，避免装备即死。
			int loss = (int)(Player.statLife * 0.8f);
			Player.statLife = Math.Max(1, Player.statLife - loss);
			Player.HealEffect(-loss, true);

			SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.8f, Pitch = -0.6f }, Player.Center);

			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 40; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
					Dust d = Dust.NewDustDirect(Player.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.6f);
					d.noGravity = true;
				}
			}
		}

		// ================= 每帧更新 =================

		public override void PostUpdate()
		{
			if (!Equipped) return;

			if (DomainCooldown > 0) DomainCooldown--;
			if (BeamCooldown > 0) BeamCooldown--;
			if (StillCooldown > 0) StillCooldown--;
			if (RestartCooldown > 0) RestartCooldown--;

			// 鬼域开启期间：每帧续一次 buff 保持常驻，同时复苏值持续上涨。
			if (DomainOn)
			{
				Player.AddBuff(ModContent.BuffType<GazeDomain>(), 2);

				Revival = Math.Min(MaxRevival, Revival + RevivalDrainPerTick);

				// 复苏值溢满，鬼眼被迫闭合，进入锁定。
				if (Revival >= MaxRevival)
				{
					CloseDomain(true);
				}
			}

			// 检测升层：每提升一层，把上一级积累的复苏值清零。
			// lastTier > 0 的检查是为了避免新存档第一次更新时误报。
			int tier = CalculateTier();
			if (lastTier > 0 && tier > lastTier)
			{
				Revival = 0f;
				if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer)
				{
					Main.NewText($"鬼眼已成长至第 {tier} 层", 200, 60, 60);
				}
			}
			lastTier = tier;

			// 第五层起：鬼域开启时，头顶鬼眼自动攻击范围内的敌人。
			// 这部分不消耗复苏值，所以给了独立的 0.75 秒间隔，避免白嫖输出过高。
			if (DomainOn && tier >= 5 && Player.whoAmI == Main.myPlayer)
			{
				boltTimer++;
				if (boltTimer >= 45)
				{
					boltTimer = 0;
					FireBolt(tier);
				}
			}
			else
			{
				boltTimer = 0;
			}

			// 右键瞬移。只在本地玩家身上处理。
			if (DomainOn && Player.whoAmI == Main.myPlayer && Main.mouseRight && Main.mouseRightRelease)
			{
				TryTeleport();
			}
		}

		public override void PostUpdateEquips()
		{
			if (!Equipped) return;

			int tier = CalculateTier();

			// 被动：每层 +2% 伤害、+1 防御。
			Player.GetDamage(DamageClass.Generic) += 0.02f * tier;
			Player.statDefense += tier;

			// 复苏值带来的输出加成。
			Player.GetDamage(DamageClass.Generic) += RevivalDamageBonus;

			// 第三层起获得"看见灵异"：透视敌人，等同于猎人药水。
			if (tier >= 3)
			{
				Player.detectCreature = true;
			}
		}

		/// <summary>
		/// 复苏值的代价：受伤倍率。
		/// 1.4.4 的新伤害管线，FinalDamage 是最终的乘法修正。
		/// </summary>
		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (!Equipped) return;
			modifiers.FinalDamage *= 1f + RevivalHurtBonus;
		}

		// ================= 击中判定 =================

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			TryApplySlow(target);
			TryApplyFlame(target);
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			TryApplySlow(target);
			TryApplyFlame(target);
		}

		/// <summary>
		/// 鬼火灼烧：第四层起，攻击命中必定点燃。具体每秒伤害由层数决定，
		/// 结算在 GhosteyeGlobalNPC.UpdateLifeRegen，这里只负责挂 buff。
		/// </summary>
		private void TryApplyFlame(NPC target)
		{
			if (!Equipped) return;
			if (Main.netMode == NetmodeID.Server) return;
			if (CalculateTier() < 4) return;

			target.AddBuff(ModContent.BuffType<GhostFlame>(), 300);
		}

		private void TryApplySlow(NPC target)
		{
			// 只在鬼域开启时生效，5% 概率。
			if (!Equipped || !DomainOn) return;
			if (Main.netMode == NetmodeID.Server) return;
			if (Main.rand.NextFloat() >= 0.05f) return;

			target.AddBuff(ModContent.BuffType<EyeSlow>(), 180);
		}

		// ================= 主动技能 =================

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Player.whoAmI != Main.myPlayer) return;
			if (!Equipped) return;

			// 一个键既是开也是关。
			if (SpectesGaze.GazeKey != null && SpectesGaze.GazeKey.JustPressed)
			{
				if (DomainOn)
				{
					CloseDomain(false);
				}
				else
				{
					TryOpenDomain();
				}
			}

			// 大激光：第五层起，鬼域开启状态下才能用。
			if (SpectesGaze.BeamKey != null && SpectesGaze.BeamKey.JustPressed)
			{
				if (DomainOn && CalculateTier() >= 5)
				{
					TryFireBeam();
				}
			}

			// 静止：第六层起，鬼域开启状态下才能用。
			if (SpectesGaze.StillKey != null && SpectesGaze.StillKey.JustPressed)
			{
				if (DomainOn && CalculateTier() >= 6)
				{
					TryActivateStill();
				}
			}

			// 重启自身：第七层起，且必须鬼域开着。
			// 要求开域是为了防止在闭眼状态下推高复苏值，否则会出现"复苏值满但鬼眼睁不开"的死锁。
			if (SpectesGaze.RestartKey != null && SpectesGaze.RestartKey.JustPressed)
			{
				if (DomainOn && CalculateTier() >= 7)
				{
					TryRestartSelf();
				}
			}

			// 测试用：清零复苏值。正式发布前删掉这段。
			if (SpectesGaze.ResetKey != null && SpectesGaze.ResetKey.JustPressed)
			{
				ResetRevival();
			}
		}

		/// <summary>测试用。把复苏值直接清零，省得等自然回落。</summary>
		public void ResetRevival()
		{
			Revival = 0f;
			Main.NewText("[测试] 复苏值已清零", 130, 200, 130);
		}

		/// <summary>
		/// 静止。鬼域内所有生物定住：普通敌人和动物 5 秒，boss 2 秒。
		/// 位置锁定在 GhosteyeGlobalNPC.PostAI 里执行。
		/// </summary>
		private void TryActivateStill()
		{
			if (StillCooldown > 0)
			{
				Main.NewText($"静止还在冷却，{StillCooldown / 60 + 1} 秒。", 160, 60, 60);
				return;
			}

			int tier = CalculateTier();
			float radius = DomainRadiusOf(tier);
			int count = 0;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;
				if (Vector2.DistanceSquared(npc.Center, Player.Center) > radius * radius) continue;

				// boss 只定 2 秒，普通生物 5 秒。
				npc.AddBuff(ModContent.BuffType<GhostStill>(), npc.boss ? 120 : 300);
				count++;
			}

			StillCooldown = 60 * 30;
			Revival = Math.Min(MaxRevival, Revival + 12f);

			if (count > 0)
			{
				SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = -0.4f }, Player.Center);
			}
		}

		/// <summary>
		/// 重启自身。回满血、清空负面效果。
		/// 不回退位置：位置不是"状态"，把玩家拉回三秒前会破坏走位和战斗节奏。
		/// 复苏值也不回退，这是技能代价。
		/// </summary>
		private void TryRestartSelf()
		{
			if (RestartCooldown > 0)
			{
				Main.NewText($"重启还在冷却，{RestartCooldown / 60 + 1} 秒。", 160, 60, 60);
				return;
			}

			int healed = Player.statLifeMax2 - Player.statLife;
			Player.statLife = Player.statLifeMax2;
			if (healed > 0) Player.HealEffect(healed, true);

			// 清掉所有负面效果。DelBuff 会打乱数组，所以下标要回退一位。
			for (int i = 0; i < Player.MaxBuffs; i++)
			{
				int type = Player.buffType[i];
				if (type <= 0) continue;
				if (!Main.debuff[type]) continue;

				Player.DelBuff(i);
				i--;
			}

			RestartCooldown = 60 * 30;
			Revival = Math.Min(MaxRevival, Revival + 18f);

			SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.9f, Pitch = -0.5f }, Player.Center);

			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 30; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
					Dust d = Dust.NewDustDirect(Player.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.4f);
					d.noGravity = true;
				}
			}
		}

		/// <summary>
		/// 被动小激光。找鬼域范围内最近的敌人打一发。
		/// </summary>
		private void FireBolt(int tier)
		{
			float radius = DomainRadiusOf(tier);
			float bestDist = radius * radius;
			NPC target = null;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;

				float dist = Vector2.DistanceSquared(npc.Center, Player.Center);
				if (dist < bestDist)
				{
					bestDist = dist;
					target = npc;
				}
			}

			if (target == null) return;

			Vector2 dir = (target.Center - Player.Center).SafeNormalize(Vector2.UnitX);
			int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(25f + tier * 8f);

			Projectile.NewProjectile(
				Player.GetSource_FromThis(),
				Player.Center,
				dir * 14f,
				ModContent.ProjectileType<Projectiles.GhostBolt>(),
				damage,
				0f,
				Player.whoAmI);
		}

		/// <summary>
		/// 主动大激光。朝鼠标方向发一道持续 1.5 秒的光束，发射要付复苏值。
		/// 冷却 20 秒，第六层起降到 10 秒。
		/// </summary>
		private void TryFireBeam()
		{
			if (BeamCooldown > 0)
			{
				Main.NewText($"激光还在冷却，{BeamCooldown / 60 + 1} 秒。", 160, 60, 60);
				return;
			}

			int tier = CalculateTier();
			Vector2 dir = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
			int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(120f + tier * 20f);

			Projectile.NewProjectile(
				Player.GetSource_FromThis(),
				Player.Center,
				dir,
				ModContent.ProjectileType<Projectiles.GhostBeam>(),
				damage,
				0f,
				Player.whoAmI);

			BeamCooldown = 60 * (tier >= 6 ? 10 : 20);
			Revival = Math.Min(MaxRevival, Revival + 8f);

			SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.9f, Pitch = -0.2f }, Player.Center);
		}

		private void TryOpenDomain()
		{
			if (DomainCooldown > 0)
			{
				Main.NewText($"鬼眼还在闭合状态，{DomainCooldown / 60 + 1} 秒后才能睁开。", 160, 60, 60);
				return;
			}

			// 理论上走不到这里：开域期间溢满会自动闭合并把值压到 60。
			// 但如果存档异常让它停在 100，这里兜底回落，避免永久打不开。
			if (Revival >= MaxRevival)
			{
				Revival = 60f;
			}

			DomainOn = true;
			Revival = Math.Min(MaxRevival, Revival + 10f);

			// 开启瞬间：范围内所有敌人挂上凝视。
			int tier = CalculateTier();
			float radius = DomainRadiusOf(tier);

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage) continue;
				if (Vector2.DistanceSquared(npc.Center, Player.Center) > radius * radius) continue;

				npc.AddBuff(ModContent.BuffType<GhastlyGaze>(), 300);

				if (Main.netMode != NetmodeID.Server)
				{
					SpawnGazeLine(Player.Center, npc.Center);
				}
			}

			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.5f }, Player.Center);
		}

		private void CloseDomain(bool overflowed)
		{
			DomainOn = false;

			if (overflowed)
			{
				// 复苏值溢满导致的强制闭合，要锁一段时间。
				DomainCooldown = 60 * 30;

				// 关键：必须回落到一个安全值，否则玩家重新开域时复苏值仍是 100，
				// 下一帧立刻又溢满、又强制闭合，会卡死在"永远开不了"的状态。
				// 回落不算"自动下降"，这是鬼眼强行闭眼的代价。
				Revival = 60f;

				if (Player.whoAmI == Main.myPlayer)
				{
					Main.NewText("复苏值溢满，鬼眼强行闭合。", 200, 40, 40);
				}

				SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.7f, Pitch = 0.3f }, Player.Center);
			}
		}

		private void TryTeleport()
		{
			Vector2 target = Main.MouseWorld;
			int tier = CalculateTier();
			float radius = DomainRadiusOf(tier);

			// 目标点必须在鬼域范围内。
			if (Vector2.Distance(target, Player.Center) > radius) return;

			Vector2 checkPos = target - Player.Size / 2f;

			// 落点不能卡在实体方块里。
			if (Collision.SolidCollision(checkPos, Player.width, Player.height)) return;

			// 视线要通，防止隔着墙瞬移。
			if (!Collision.CanHitLine(Player.Center, 1, 1, target, 1, 1)) return;

			if (Main.netMode != NetmodeID.Server)
			{
				for (int i = 0; i < 16; i++)
				{
					Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
					Dust d = Dust.NewDustDirect(Player.Center, 1, 1, DustID.Blood, vel.X, vel.Y, 0, default, 1.3f);
					d.noGravity = true;
				}
			}

			Player.Teleport(checkPos, 1);
			Player.velocity = Vector2.Zero;
			Revival = Math.Min(MaxRevival, Revival + 3f);
		}

		private static void SpawnGazeLine(Vector2 from, Vector2 to)
		{
			int steps = 12;
			for (int i = 0; i < steps; i++)
			{
				Vector2 pos = Vector2.Lerp(from, to, i / (float)steps);
				Dust d = Dust.NewDustDirect(pos, 1, 1, DustID.Blood, 0f, 0f, 0, default, 1.1f);
				d.noGravity = true;
				d.velocity *= 0.2f;
			}
		}

		// ================= 层数判定 =================

		/// <summary>
		/// 按主线进度换算层数。从高到低判断，命中即返回。
		///
		/// 顺序必须严格对齐设计文档，之前写错过一次：
		/// 眼 -> 世吞/脑 -> 骷髅王 -> 血肉墙 -> 三王齐 -> 石巨人 -> 教徒 -> 四柱之一 -> 月总
		///
		/// 注意这里没有"升级事件"，层数是每次现算的。世界进度一变，返回值立刻变。
		/// 灾厄 DLC 之后会用另一套进度覆盖这里，所以全项目只有这一个入口。
		/// </summary>
		public static int CalculateTier()
		{
			if (NPC.downedMoonlord) return 10;                                                        // 月亮领主
			if (NPC.downedTowerSolar || NPC.downedTowerNebula
				|| NPC.downedTowerVortex || NPC.downedTowerStardust) return 9;                        // 四柱任一
			if (NPC.downedAncientCultist) return 8;                                                   // 拜月教徒
			if (NPC.downedGolemBoss) return 7;                                                        // 石巨人
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) return 6;           // 三王齐
			if (Main.hardMode) return 5;                                                              // 血肉墙之后
			if (NPC.downedBoss3) return 4;                                                            // 骷髅王
			if (NPC.downedBoss2) return 3;                                                            // 世界吞噬者 / 克苏鲁之脑
			if (NPC.downedBoss1) return 2;                                                            // 克苏鲁之眼
			return 1;
		}

		// ================= 存档 =================

		public override void SaveData(TagCompound tag)
		{
			tag["equipped"] = Equipped;
			tag["revival"] = Revival;
			tag["lastTier"] = lastTier;
		}

		public override void LoadData(TagCompound tag)
		{
			Equipped = tag.GetBool("equipped");
			Revival = tag.GetFloat("revival");
			lastTier = tag.GetInt("lastTier");
		}
	}
}
