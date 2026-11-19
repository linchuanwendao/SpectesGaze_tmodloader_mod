using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	internal sealed class DomainLighting : ModSystem
	{
		private static readonly Vector3 Ambient = new(0.66f, 0.61f, 0.62f);
		private Vector2 center;
		private float radiusSquared;

		public override void Load()
		{
			if (Main.dedServ) return;
			On_LightingEngine.GetColor += LightColor;
			On_LegacyLighting.GetColor += LegacyLightColor;
		}

		public override void Unload()
		{
			if (Main.dedServ) return;
			On_LightingEngine.GetColor -= LightColor;
			On_LegacyLighting.GetColor -= LegacyLightColor;
		}

		public override void OnWorldUnload() => radiusSquared = 0f;

		public override void PostUpdateEverything()
		{
			if (Main.dedServ) return;
			Player player = Main.LocalPlayer;
			if (!DomainRenderer.HasDomain(player))
			{
				radiusSquared = 0f;
				return;
			}

			center = player.Center;
			float radius = GhosteyePlayer.DomainRadiusOf(GhosteyePlayer.CalculateTier());
			radiusSquared = radius * radius;
		}

		private float TileDistanceSquared(int x, int y)
		{
			Vector2 tileCenter = new(x * 16f + 8f, y * 16f + 8f);
			return Vector2.DistanceSquared(tileCenter, center);
		}

		private Vector3 LightColor(On_LightingEngine.orig_GetColor orig, LightingEngine engine, int x, int y) =>
			ApplyAmbient(orig(engine, x, y), x, y);

		private Vector3 LegacyLightColor(On_LegacyLighting.orig_GetColor orig, LegacyLighting engine, int x, int y) =>
			ApplyAmbient(orig(engine, x, y), x, y);

		private Vector3 ApplyAmbient(Vector3 light, int x, int y)
		{
			if (radiusSquared <= 0f) return light;
			float distanceSquared = TileDistanceSquared(x, y);
			if (distanceSquared >= radiusSquared) return light;

			float strength = MathHelper.SmoothStep(0f, 1f, (1f - distanceSquared / radiusSquared) * 4f);
			return Vector3.Max(light, Ambient * strength);
		}
	}
}
