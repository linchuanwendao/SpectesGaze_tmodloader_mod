using System;
using Microsoft.Xna.Framework;
using Terraria;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private const int EdgePulseCount = 3;
		private static readonly Color SparkHot = new(255, 154, 116);
		private readonly Random edgeSparkRandom = new();
		private readonly SparkParticles edgeSparks = new(128);

		private static float EdgePulsePhase => (Main.GameUpdateCount % 192u) * (MathHelper.TwoPi / 192f);

		private void EmitEdgeSparks(Player player, float time)
		{
			bool openingBurst = domainAge is 7 or 15;
			bool emitFromPulses = Main.GameUpdateCount % 4u == 0;
			if (!openingBurst && !emitFromPulses) return;

			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			Vector2 center = player.Center;
			Color tint = tier >= GhosteyePlayer.AwakenedTier ? Color.Lerp(DomainColor(tier), Pale, 0.5f) : DomainColor(tier);
			float fade = DomainFade;
			if (openingBurst)
			{
				const int count = 16;
				float offset = edgeSparkRandom.NextSingle() * MathHelper.TwoPi;
				for (int i = 0; i < count; i++)
				{
					float angle = offset + (i + edgeSparkRandom.NextSingle()) * (MathHelper.TwoPi / count);
					SpawnEdgeSpark(center, radius, angle, time, (i % 2 == 0 ? 1f : -1f), tint, fade);
				}
			}

			if (!emitFromPulses) return;

			float phase = EdgePulsePhase;
			EmitPulseSparks(center, radius, time, phase, 1f, tint, fade);
			float overlap = OverlapOpacity(player.GetModPlayer<GhosteyePlayer>().OverlapTime);
			if (tier >= GhosteyePlayer.OverlapTier && overlap > 0.08f)
			{
				EmitPulseSparks(OverlapEdgeCenter(center, radius, time), radius * 0.81f, -time * 0.79f, -phase,
					-1f, Violet, fade * overlap * 0.64f);
			}
		}

		private void EmitPulseSparks(Vector2 center, float radius, float time, float phase, float direction, Color tint, float opacity)
		{
			for (int i = 0; i < EdgePulseCount; i++)
			{
				float angle = (phase + i * MathHelper.TwoPi) / EdgePulseCount;
				angle += (edgeSparkRandom.NextSingle() - 0.5f) * 0.34f;
				SpawnEdgeSpark(center, radius, angle, time, direction, tint, opacity);
			}
		}

		private void SpawnEdgeSpark(Vector2 center, float radius, float angle, float time, float direction, Color tint, float opacity)
		{
			if (edgeSparks.IsFull) return;

			Vector2 radial = angle.ToRotationVector2();
			Vector2 tangent = new(-radial.Y, radial.X);
			Vector2 position = center + radial * (radius * DomainEdgeScale(angle, time));
			float speedScale = Math.Clamp(radius / 420f, 0.58f, 1f);
			float tangentSpeed = (1.7f + edgeSparkRandom.NextSingle() * 3.3f) * direction;
			float inwardSpeed = 0.9f + edgeSparkRandom.NextSingle() * 2.5f;
			Vector2 velocity = (tangent * tangentSpeed - radial * inwardSpeed) * speedScale;
			Color color = Color.Lerp(tint, SparkHot, 0.25f + edgeSparkRandom.NextSingle() * 0.65f);
			float width = 1.2f + edgeSparkRandom.NextSingle() * 2.2f;
			edgeSparks.Spawn(position, velocity, color, width, opacity, edgeSparkRandom.Next(25, 47));
		}
	}
}
