using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.Players
{
	public partial class GhosteyePlayer
	{
		internal const int BloodFlightTier = 2;
		private const float BloodFlightSpeed = 22f;
		internal bool BloodFlightActive { get; private set; }
		private Vector2 bloodFlightVelocity;

		private void ToggleBloodFlight()
		{
			if (BloodFlightActive)
			{
				SetBloodFlight(false);
				Player.velocity = bloodFlightVelocity = Vector2.Zero;
				return;
			}

			if (!DomainOn || CalculateTier() < BloodFlightTier) return;
			Player.mount.Dismount(Player);
			Player.RemoveAllGrapplingHooks();
			SetBloodFlight(true);
		}

		private void SetBloodFlight(bool enabled)
		{
			if (BloodFlightActive == enabled) return;
			BloodFlightActive = enabled;
			bloodFlightVelocity = Vector2.Zero;
			Player.fallStart = (int)(Player.position.Y / 16f);
			if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
				((SpectesGaze)Mod).SendBloodFlight(Player.whoAmI, enabled);
		}

		internal void SetBloodFlightVisual(bool active) => BloodFlightActive = active;

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			if (Main.netMode != NetmodeID.Server) return;
			SpectesGaze mod = (SpectesGaze)Mod;
			if (BloodFlightActive) mod.SendBloodFlight(Player.whoAmI, true, toWho, fromWho);
			if (FullyAwakened)
			{
				mod.SendAwakenedDomain(Player.whoAmI, true, toWho, fromWho);
				if (OverlapActive) mod.SendPermanentOverlap(Player.whoAmI, true, toWho, fromWho);
			}
			else if (OverlapActive) mod.SendTimedOverlap(Player.whoAmI, true, toWho, fromWho);
		}

		public override void PreUpdateMovement()
		{
			if (!BloodFlightActive || Player.whoAmI != Main.myPlayer) return;
			if (!Equipped || !DomainOn || Player.dead || CalculateTier() < BloodFlightTier)
			{
				SetBloodFlight(false);
				return;
			}

			Player.fallStart = (int)(Player.position.Y / 16f);
			// 硬直、坐骑和钩爪接管位移时退出，避免两个移动系统相互覆盖。
			if (Player.mount.Active || Player.grapCount > 0 || Player.frozen || Player.stoned || Player.webbed)
			{
				SetBloodFlight(false);
				return;
			}

			bool usingUi = Main.playerInventory || Main.drawingPlayerChat || Main.mapFullscreen
				|| ModContent.GetInstance<GhosteyeUI>().GuideOpen;
			Vector2 offset = usingUi ? Vector2.Zero : Main.MouseWorld - Player.Center;
			float distance = offset.Length();
			float speed = MathHelper.Clamp((distance - 10f) * 0.22f, 0f, BloodFlightSpeed);
			Vector2 desired = offset.SafeNormalize(Vector2.Zero) * speed;
			bloodFlightVelocity = Vector2.Lerp(bloodFlightVelocity, desired, 0.26f);
			if (bloodFlightVelocity.LengthSquared() < 0.04f) bloodFlightVelocity = Vector2.Zero;
			Player.velocity = bloodFlightVelocity;
		}

		public override void HideDrawLayers(PlayerDrawSet drawInfo)
		{
			bool overlap = OverlapActive;
			if (Player.dead || drawInfo.headOnlyRender || (!BloodFlightActive && !overlap)) return;
			foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers)
			{
				if (overlap && !BloodFlightActive && layer == PlayerDrawLayers.HeldItem) continue;
				layer.Hide();
			}
		}
	}
}
