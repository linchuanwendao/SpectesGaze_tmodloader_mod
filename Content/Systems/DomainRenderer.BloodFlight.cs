using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private const int FlightTrailLength = 17;
		private const int MaxFlightParticles = 420;
		private static readonly Color FlightShadow = new(13, 3, 12);
		private static readonly Color FlightBlood = new(145, 8, 35);
		private static readonly Color FlightFlare = new(255, 94, 87);
		private readonly FlightTrail[] flightTrails = new FlightTrail[Main.maxPlayers];
		private readonly List<FlightParticle> flightParticles = new(MaxFlightParticles);
		private readonly Random flightRandom = new();
		private bool hasFlightTrails;

		private sealed class FlightTrail
		{
			public readonly Vector2[] Points = new Vector2[FlightTrailLength];
			public int Count;
		}

		private struct FlightParticle
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Size;
			public float Rotation;
			public float Spin;
			public Color Color;
			public int Variant;
			public int Age;
			public int Lifetime;
		}

		private void UpdateBloodFlightEffects()
		{
			for (int i = flightParticles.Count - 1; i >= 0; i--)
			{
				FlightParticle particle = flightParticles[i];
				if (++particle.Age >= particle.Lifetime)
				{
					flightParticles[i] = flightParticles[^1];
					flightParticles.RemoveAt(flightParticles.Count - 1);
					continue;
				}

				particle.Position += particle.Velocity;
				particle.Velocity *= 0.95f;
				particle.Rotation += particle.Spin;
				flightParticles[i] = particle;
			}

			hasFlightTrails = false;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player player = Main.player[i];
				if (player == null || !player.active || player.dead || !player.GetModPlayer<GhosteyePlayer>().BloodFlightActive)
				{
					if (flightTrails[i] != null) flightTrails[i].Count = 0;
					continue;
				}

				hasFlightTrails = true;
				FlightTrail trail = flightTrails[i] ??= new FlightTrail();
				Vector2 center = player.Center;
				Vector2 previous = trail.Count > 0 ? trail.Points[0] : center;
				bool restart = trail.Count == 0 || Vector2.DistanceSquared(center, previous) > 72f * 72f;
				if (restart)
				{
					trail.Count = 0;
					previous = center;
				}

				trail.Count = Math.Min(trail.Count + 1, FlightTrailLength);
				Array.Copy(trail.Points, 0, trail.Points, 1, trail.Count - 1);
				trail.Points[0] = center;
				if (!GhosteyeEffects.NearScreen(center)) continue;

				Lighting.AddLight(center, 0.50f, 0.06f, 0.12f);
				Vector2 travel = center - previous;
				bool moving = travel.LengthSquared() > 4f;
				if (restart || moving || Main.GameUpdateCount % 2 == 0)
				{
					Vector2 heading = travel.SafeNormalize(player.velocity.SafeNormalize(Vector2.UnitX * player.direction));
					EmitFlightParticles(previous, center, heading, restart ? 24 : moving ? 9 : 3, restart);
				}
			}
		}

		private void EmitFlightParticles(Vector2 from, Vector2 to, Vector2 heading, int count, bool burst)
		{
			Vector2 normal = new(-heading.Y, heading.X);
			float speed = Vector2.Distance(from, to);
			for (int i = 0; i < count && flightParticles.Count < MaxFlightParticles; i++)
			{
				bool dark = i % 2 == 0;
				float side = FlightRange(-1f, 1f);
				Vector2 position = Vector2.Lerp(from, to, (i + flightRandom.NextSingle()) / count)
					+ normal * (side * FlightRange(6f, 20f)) - heading * FlightRange(0f, 15f);
				Vector2 velocity = heading * FlightRange(-5.6f - speed * 0.14f, -0.8f) + normal * (side * FlightRange(0.7f, 4.7f));
				if (burst) velocity = FlightRange(-MathHelper.Pi, MathHelper.Pi).ToRotationVector2() * FlightRange(1.5f, 8.5f);
				flightParticles.Add(new FlightParticle
				{
					Position = position,
					Velocity = velocity,
					Size = dark ? FlightRange(17f, 44f) : FlightRange(9f, 27f),
					Rotation = velocity.ToRotation(),
					Spin = FlightRange(-0.18f, 0.18f),
					Color = dark ? Color.Lerp(FlightShadow, Shadow, flightRandom.NextSingle())
						: Color.Lerp(FlightBlood, FlightFlare, FlightRange(0.2f, 1f)),
					Variant = flightRandom.Next(8),
					Lifetime = flightRandom.Next(16, dark ? 39 : 32)
				});
			}
		}

		private float FlightRange(float min, float max) => MathHelper.Lerp(min, max, flightRandom.NextSingle());

		private void DrawBloodFlight(Rectangle view, float time)
		{
			foreach (FlightParticle particle in flightParticles)
			{
				if (!view.Contains(particle.Position.ToPoint())) continue;
				float life = particle.Age / (float)particle.Lifetime;
				float fade = (1f - life) * (1f - life);
				Vector2 size = new Vector2(particle.Size * (1.75f - life * 0.67f), particle.Size * (0.54f + life * 0.26f));
				GhosteyeEffects.DrawFlesh(particle.Position, size, particle.Color * (fade * 0.88f), particle.Rotation, particle.Variant);
			}

			for (int i = 0; i < flightTrails.Length; i++)
			{
				FlightTrail trail = flightTrails[i];
				if (trail == null || trail.Count == 0) continue;
				Player player = Main.player[i];
				if (!player.active || player.dead || !player.GetModPlayer<GhosteyePlayer>().BloodFlightActive) continue;
				DrawFlightTrail(trail, player, view, time);
			}
		}

		private static void DrawFlightTrail(FlightTrail trail, Player player, Rectangle view, float time)
		{
			float phase = time * 12f + player.whoAmI;
			for (int i = trail.Count - 1; i > 0; i--)
			{
				Vector2 from = trail.Points[i];
				Vector2 to = trail.Points[i - 1];
				if (!Visible(from, to, view)) continue;

				float taper = 1f - i / (float)FlightTrailLength;
				DrawSoftLine(from, to, 39f * taper + 4f, FlightShadow * (taper * 0.89f));
				DrawSoftLine(from, to, 24f * taper + 3f, FlightBlood * (taper * 0.72f));
				DrawSoftLine(from, to, 5f * taper + 1f, Light(FlightFlare, taper * 0.66f));

				Vector2 normal = (to - from).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
				float waveFrom = MathF.Sin(phase - i * 0.52f) * (4f + 15f * taper);
				float waveTo = MathF.Sin(phase - (i - 1) * 0.52f) * (4f + 15f * taper);
				DrawSoftLine(from + normal * waveFrom, to + normal * waveTo, 5f * taper + 2f, Light(Crimson, taper * 0.62f));
				DrawSoftLine(from - normal * waveFrom, to - normal * waveTo, 9f * taper + 2f, FlightShadow * (taper * 0.75f));
			}

			Vector2 center = trail.Points[0];
			if (!view.Contains(center.ToPoint())) return;
			Vector2 motion = center - trail.Points[Math.Min(2, trail.Count - 1)];
			float stretch = Math.Min(motion.Length() * 0.64f, 25f);
			float angle = motion.SafeNormalize(Vector2.UnitX * player.direction).ToRotation();
			float pulse = 1f + 0.12f * MathF.Sin(phase);
			GhosteyeEffects.DrawFlesh(center, new Vector2(90f + stretch * 2f, 58f) * pulse, FlightShadow, angle, 5);
			GhosteyeEffects.DrawFlesh(center, new Vector2(78f + stretch, 36f) * pulse, FlightBlood, angle, 2);
			DrawGlow(center, new Vector2(70f + stretch, 37f), Light(Crimson, 0.94f), angle);
			DrawGlow(center, new Vector2(33f + stretch, 14f), Light(FlightFlare, 0.95f), angle);
			DrawGlow(center, new Vector2(18f + stretch, 7f), Light(Pale, 0.76f), angle);
		}
	}
}
