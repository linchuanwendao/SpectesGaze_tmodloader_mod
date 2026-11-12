using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Players
{
	public partial class GhosteyePlayer
	{
		internal const int OverlapTier = 9;
		internal const int AwakenedTier = 10;
		internal const int OverlapDuration = 60 * 3;

		internal int OverlapTime { get; private set; }
		internal int OverlapCooldown { get; private set; }
		internal bool FullyAwakened => Equipped && DomainOn && !Player.dead && CalculateTier() >= AwakenedTier;
		private bool applyingEchoHits;

		private void UpdateEndgameState(int tier)
		{
			if (OverlapTime > 0) OverlapTime--;
			if (tier >= AwakenedTier)
			{
				Revival = 0f;
				DomainCooldown = BeamCooldown = StillCooldown = RestartCooldown = RewindCooldown = OverlapCooldown = 0;
			}
			else if (OverlapCooldown > 0)
			{
				OverlapCooldown--;
			}
		}

		private static int TierCooldown(int ticks) => CalculateTier() >= AwakenedTier ? 0 : ticks;

		private void GainRevival(float amount) =>
			Revival = CalculateTier() >= AwakenedTier ? 0f : Math.Min(MaxRevival, Revival + amount);

		private void TryOverlapDomain()
		{
			if (TierCooldown(OverlapCooldown) > 0)
			{
				Main.NewText($"鬼域叠加还在冷却，{(OverlapCooldown + 59) / 60} 秒。", 160, 60, 60);
				return;
			}

			OverlapTime = OverlapDuration;
			OverlapCooldown = TierCooldown(60 * 60);
			float radius = DomainRadiusOf(CalculateTier());
			GhosteyeEffects.Overlap(Player.Center, radius);
			SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.85f, Pitch = -0.8f }, Player.Center);
		}

		public override void UpdateDead()
		{
			DomainOn = false;
			OverlapTime = 0;
			boltTimer = 0;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
			ApplyEchoHits(target, hit, damageDone);

		internal int AttackHitCount(int tier)
		{
			int hits = tier >= AwakenedTier ? 10 : 1;
			if (tier >= OverlapTier && OverlapTime > 0) hits += 8;
			return hits;
		}

		internal void RecordDps(NPC target, int damage)
		{
			if (damage > 0 && Player.whoAmI == Main.myPlayer && Player.accDreamCatcher && !target.HideStrikeDamage)
				Player.addDPS(damage);
		}

		internal void ApplyEchoHits(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (applyingEchoHits || !Equipped || !DomainOn || Player.dead || Player.whoAmI != Main.myPlayer
				|| damageDone <= 0 || hit.InstantKill || (target.friendly && target.type != NPCID.TargetDummy)) return;

			int tier = CalculateTier();
			int extraHits = AttackHitCount(tier) - 1;
			if (extraHits == 0) return;

			// 回响沿用已经结算过的伤害，不重新扣防御，也不重复触发武器特效。
			hit.Knockback = 0f;
			hit.HideCombatText = true;
			int targetType = target.type;
			int landed = 0;
			applyingEchoHits = true;
			try
			{
				for (int i = 0; i < extraHits; i++)
				{
					if (!target.active || target.life <= 0 || target.type != targetType
						|| target.dontTakeDamage) break;

					// 稻草人可以正常受击，StrikeNPC 会保留它的 immortal 状态。
					int damage = target.StrikeNPC(hit);
					if (damage <= 0) break;

					if (Main.netMode == NetmodeID.MultiplayerClient) NetMessage.SendStrikeNPC(target, hit);
					RecordDps(target, damage);
					landed++;
				}
			}
			finally
			{
				applyingEchoHits = false;
			}

			if (landed > 0) GhosteyeEffects.EchoHit(target.Center, target.Size, tier >= AwakenedTier);
		}

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => FullyAwakened;

		public override void UpdateLifeRegen()
		{
			if (!FullyAwakened) return;
			Player.lifeRegen = Math.Max(0, Player.lifeRegen);
			Player.lifeRegenCount = Math.Max(0, Player.lifeRegenCount);
		}

		public override float UseSpeedMultiplier(Item item) => FullyAwakened ? 10f : 1f;

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			if (!FullyAwakened) return true;

			Player.statLife = Math.Max(1, Player.statLifeMax2);
			Player.statMana = Player.statManaMax2;
			Player.breath = Player.breathMax;
			Player.lifeRegenCount = 0;
			ClearDebuffs();
			GhosteyeEffects.Rebirth(Player.Center);
			SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.85f, Pitch = -0.8f }, Player.Center);
			return false;
		}
	}
}
