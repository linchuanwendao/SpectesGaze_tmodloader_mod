using System;
using Microsoft.Xna.Framework;
using Terraria;
using SpectesGaze.Content.Projectiles;
using static SpectesGaze.Content.Systems.FleshNoise;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private static void DrawBeamMuzzle(Vector2 pupil, GhostBeam.FiringEye shot, float time, uint seed)
		{
			float fade = shot.BeamOpacity;
			if (fade <= 0f) return;

			float phase = time * 6.4f + Unit(seed) * MathHelper.TwoPi;
			float angle = shot.Aim.ToRotation() + MathF.Sin(phase * 0.66f) * 0.16f;
			float open = MathHelper.Clamp((shot.Opening - 1f) * 1.75f, 0f, 1f);
			Vector2 radius = new(34f + open * 5f + MathF.Sin(phase) * 3f, 18f + open * 4f + MathF.Cos(phase * 1.4f) * 2f);
			Vector2 center = pupil + shot.Aim * (22f + MathF.Sin(phase) * 3f);
			DrawGlow(center, radius * new Vector2(2.55f, 2.62f), BeamSoot * (fade * 0.84f), angle);
			DrawGlow(center, radius * new Vector2(2.1f, 2.0f), Light(PaintCrimson, fade * 0.46f), angle);

			const int segments = 32;
			Vector2 first = MuzzleContour(center, radius, angle, 0f, phase);
			Vector2 previous = first;
			for (int i = 1; i <= segments; i++)
			{
				float turn = MathHelper.TwoPi * i / segments;
				Vector2 point = i == segments ? first : MuzzleContour(center, radius, angle, turn, phase);
				float ridges = 0.5f + 0.5f * MathF.Sin(turn * 7f + phase * 1.21f);
				float shimmer = 0.5f + 0.5f * MathF.Sin(turn * 4f - phase * 1.6f + ridges);
				DrawSoftLine(previous, point, 10f + 7f * ridges, BeamSoot * (fade * 0.85f));
				DrawSoftLine(previous, point, 4f + 3f * ridges,
					Light(Color.Lerp(PaintBlood, PaintCrimson, shimmer), fade * (0.46f + shimmer * 0.25f)));
				DrawSoftLine(previous, point, 1.6f + 1.7f * shimmer,
					Light(Color.Lerp(Crimson, Ember, shimmer), fade * (0.16f + 0.47f * shimmer * shimmer)));
				previous = point;
			}

			DrawSoftLine(pupil, pupil + shot.Aim * (radius.X * 1.65f), 13f, Light(Ember, fade * 0.69f));
			DrawGlow(pupil, new Vector2(25f, 16f), Light(Pale, fade * 0.86f), shot.Aim.ToRotation());
		}

		private static Vector2 MuzzleContour(Vector2 center, Vector2 radius, float rotation, float angle, float phase)
		{
			// Modulate a stable ellipse so the opening keeps its shape while the edge shifts.
			float fray = 1f + MathF.Sin(3f * angle + phase) * 0.09f
				+ MathF.Sin(7f * angle - phase * 1.26f) * 0.065f;
			return center + (angle.ToRotationVector2() * radius * fray).RotatedBy(rotation);
		}
	}
}
