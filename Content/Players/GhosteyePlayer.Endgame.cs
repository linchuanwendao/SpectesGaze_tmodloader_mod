using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Players
{
	public partial class GhosteyePlayer
	{
		internal const int OverlapTier = 9;
		internal const int AwakenedTier = 10;
		internal const int OverlapDuration = 60 * 3;
		internal const int OverlapDamageMultiplier = 16;

		internal int OverlapTime { get; private set; }
		internal int OverlapCooldown { get; private set; }
		internal bool SirenCurseCleared { get; set; }
		private bool permanentOverlap;
		private bool remoteAwakenedDomain;
		private bool sentAwakenedDomain;
		private uint lastRebirthTick;

		internal bool ProtectsFromDeath => CalculateTier() >= AwakenedTier
			&& (Player.whoAmI == Main.myPlayer ? Equipped && DomainOn : remoteAwakenedDomain);
		internal bool FullyAwakened => !Player.dead && ProtectsFromDeath;

		internal bool OverlapActive => CalculateTier() switch
		{
			>= AwakenedTier => permanentOverlap && FullyAwakened,
			OverlapTier => !Player.dead && OverlapTime > 0
				&& (Player.whoAmI != Main.myPlayer || (Equipped && DomainOn)),
			_ => false
		};

		private void UpdateEndgameState(int tier)
		{
			if (Player.statLife <= 0 && ProtectsFromDeath) RestoreFromDeath();
			SyncAwakenedDomain();
			if (tier >= AwakenedTier)
			{
				Revival = 0f;
				if (lastTier < AwakenedTier || DomainCooldown > 0) Player.ClearBuff(ModContent.BuffType<RevivalOverload>());
				DomainCooldown = BeamCooldown = StillCooldown = RestartCooldown = RewindCooldown = OverlapCooldown = 0;
			}
			else if (OverlapCooldown > 0)
			{
				OverlapCooldown--;
			}
		}

		private static int TierCooldown(int ticks) => CalculateTier() >= AwakenedTier ? 0 : ticks;

		private void GainRevival(float amount) => GainRevival(amount, CalculateTier());

		private void GainRevival(float amount, int tier)
		{
			int limit = RevivalLimitOf(tier);
			Revival = Math.Min(limit, Revival + amount);
			if (limit > 0 && Revival >= limit && DomainOn)
				CloseDomain(overflowed: true);
		}

		private void TryOverlapDomain()
		{
			int tier = CalculateTier();
			if (!DomainOn || Player.dead || tier < OverlapTier) return;
			if (OverlapActive)
			{
				StopOverlap();
				return;
			}

			if (tier >= AwakenedTier)
			{
				SetTimedOverlap(false);
				SetPermanentOverlap(true);
			}
			else
			{
				if (OverlapCooldown > 0)
				{
					Main.NewText($"鬼域叠加还在冷却，{(OverlapCooldown + 59) / 60} 秒。", 160, 60, 60);
					return;
				}

				SetTimedOverlap(true);
				OverlapCooldown = 60 * 60;
			}

			float radius = DomainRadiusOf(tier);
			GhosteyeEffects.Overlap(Player.Center, radius);
			SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = 0.85f, Pitch = -0.8f }, Player.Center);
			ShakeCamera(new Vector2(0.6f, -0.8f), 5.0f, 13);
		}

		public override void UpdateDead()
		{
			SetBloodFlight(false);
			DomainOn = false;
			remoteAwakenedDomain = false;
			SyncAwakenedDomain();
			StopOverlap();
			boltTimer = 0;
		}

		private void StopOverlap()
		{
			SetTimedOverlap(false);
			SetPermanentOverlap(false);
		}

		private void SetTimedOverlap(bool active)
		{
			if (active == (OverlapTime > 0)) return;
			ReceiveTimedOverlap(active);
			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
				((SpectesGaze)Mod).SendTimedOverlap(Player.whoAmI, active);
		}

		internal void ReceiveTimedOverlap(bool active) => OverlapTime = active ? OverlapDuration : 0;

		private void SetPermanentOverlap(bool active)
		{
			if (active == permanentOverlap) return;
			ReceivePermanentOverlap(active);
			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
				((SpectesGaze)Mod).SendPermanentOverlap(Player.whoAmI, active);
		}

		internal void ReceivePermanentOverlap(bool active) => permanentOverlap = active;

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			if (OverlapActive) modifiers.FinalDamage *= OverlapDamageMultiplier;
		}

		internal void RecordDps(NPC target, int damage)
		{
			if (damage > 0 && Player.whoAmI == Main.myPlayer && Player.accDreamCatcher && !target.HideStrikeDamage)
				Player.addDPS(damage);
		}

		private void SyncAwakenedDomain()
		{
			if (Main.netMode != NetmodeID.MultiplayerClient || Player.whoAmI != Main.myPlayer) return;
			bool active = FullyAwakened;
			if (active == sentAwakenedDomain) return;
			sentAwakenedDomain = active;
			((SpectesGaze)Mod).SendAwakenedDomain(Player.whoAmI, active);
		}

		internal void ReceiveAwakenedDomain(bool active)
		{
			remoteAwakenedDomain = active;
			if (!active) ReceivePermanentOverlap(false);
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
			if (!ProtectsFromDeath) return true;
			RestoreFromDeath();
			return false;
		}

		internal void RestoreFromDeath()
		{
			bool lifeChanged = Player.dead || Player.statLife != Player.statLifeMax2 || Player.statMana != Player.statManaMax2;
			Player.dead = false;
			Player.respawnTimer = 0;
			Player.statLife = Math.Max(1, Player.statLifeMax2);
			Player.statMana = Player.statManaMax2;
			Player.breath = Player.breathMax;
			Player.lifeRegenCount = 0;
			ClearDebuffs();
			if (lifeChanged && (Main.netMode == NetmodeID.Server
				|| (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)))
			{
				NetMessage.SendData(MessageID.PlayerLifeMana, number: Player.whoAmI);
				NetMessage.SendData(MessageID.PlayerMana, number: Player.whoAmI);
			}

			if (Main.dedServ || (lastRebirthTick != 0 && Main.GameUpdateCount - lastRebirthTick < 20)) return;
			lastRebirthTick = Main.GameUpdateCount;
			GhosteyeEffects.Rebirth(Player.Center);
			SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.85f, Pitch = -0.8f }, Player.Center);
		}
	}
}
