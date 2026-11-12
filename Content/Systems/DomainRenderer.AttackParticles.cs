using System;
using Microsoft.Xna.Framework;
using Terraria;
using SpectesGaze.Content.Projectiles;

namespace SpectesGaze.Content.Systems
{
	public partial class DomainRenderer
	{
		private static readonly Color BeamSpark = new(255, 143, 96);
		private static readonly Color BoltSpark = new(230, 64, 91);
		private readonly Random attackSparkRandom = new();
		private readonly SparkParticles beamSparks = new(208);
		private readonly SparkParticles lightAttackSparks = new(80);
		private int beamImpactBursts;
		private int weaponSparkCooldown;

		internal void EmitBeamSparks(Vector2 start, Vector2 aim, int age)
		{
			if (beamSparks.IsFull || age >= GhostBeam.Duration - 6 || age % 2 == 0) return;

			float fade = MathHelper.Clamp((GhostBeam.Duration - age) / 15f, 0f, 1f);
			if (GhosteyeEffects.NearScreen(start))
			{
				if (age == 1) EmitAttackFan(beamSparks, start, aim, 18, 1.55f, BeamSpark);
				if (age % 4 == 1)
				{
					for (int i = 0; i < 3; i++)
					{
						Vector2 heading = aim.RotatedBy(AttackRange(-0.68f, 0.68f));
						beamSparks.Spawn(start + heading * AttackRange(6f, 22f), heading * AttackRange(5f, 12f),
							BeamSpark, AttackRange(2.4f, 4.4f), fade, attackSparkRandom.Next(13, 24));
					}
				}
			}

			// Sample the entire beam so sparks still cover the visible section when the muzzle is off screen.
			Vector2 normal = new(-aim.Y, aim.X);
			int count = age <= 7 ? 12 : 5;
			for (int i = 0; i < count && !beamSparks.IsFull; i++)
			{
				float along = 0.025f + (i + attackSparkRandom.NextSingle()) / count * 0.955f;
				float side = attackSparkRandom.Next(2) == 0 ? -1f : 1f;
				float taper = Math.Min(1f, (1f - along) * 8f);
				float offset = GhostBeam.Thickness * AttackRange(0.18f, 0.58f) * taper;
				Vector2 point = start + aim * (GhostBeam.MaxLength * along) + normal * (side * offset);
				if (!GhosteyeEffects.NearScreen(point)) continue;

				Vector2 velocity = aim * AttackRange(3.4f, 10.4f) + normal * (side * AttackRange(1.3f, 5.3f));
				Color tint = Color.Lerp(Crimson, BeamSpark, AttackRange(0.26f, 0.92f));
				beamSparks.Spawn(point, velocity, tint, AttackRange(1.5f, 4f), fade * AttackRange(0.66f, 0.96f),
					attackSparkRandom.Next(16, 31));
			}
		}

		internal void EmitBeamHitSparks(Vector2 point, Vector2 aim)
		{
			if (beamImpactBursts >= 3 || beamSparks.IsFull || !GhosteyeEffects.NearScreen(point)) return;
			beamImpactBursts++;
			EmitAttackFan(beamSparks, point, aim, 9, 1.1f, BeamSpark);
		}

		internal void EmitBoltSparks(Vector2 center, Vector2 velocity, int age)
		{
			if (age % 3 != 0 || lightAttackSparks.IsFull || !GhosteyeEffects.NearScreen(center)) return;
			Vector2 heading = velocity.SafeNormalize(Vector2.UnitX);
			Vector2 normal = new(-heading.Y, heading.X);
			float side = AttackRange(-1f, 1f);
			Vector2 point = center - heading * 8f + normal * (side * 5f);
			Vector2 drift = -heading * AttackRange(1.4f, 3.9f) + normal * (side * 1.4f);
			lightAttackSparks.Spawn(point, drift, BoltSpark, AttackRange(1.2f, 2.2f), 0.76f, attackSparkRandom.Next(12, 20));
		}

		internal void EmitBoltHitSparks(Vector2 point, Vector2 direction)
		{
			if (GhosteyeEffects.NearScreen(point))
				EmitAttackFan(lightAttackSparks, point, direction.SafeNormalize(Vector2.UnitX), 3, 0.65f, BoltSpark);
		}

		internal void EmitWeaponHitSparks(Vector2 point, Vector2 heading, bool critical)
		{
			if (weaponSparkCooldown > 0 || lightAttackSparks.IsFull || !GhosteyeEffects.NearScreen(point)) return;
			weaponSparkCooldown = 4;
			EmitAttackFan(lightAttackSparks, point, -heading, critical ? 5 : 3, critical ? 0.72f : 0.52f, Crimson);
		}

		private void EmitAttackFan(SparkParticles particles, Vector2 center, Vector2 aim, int count, float power, Color color)
		{
			for (int i = 0; i < count && !particles.IsFull; i++)
			{
				Vector2 heading = aim.RotatedBy(AttackRange(-1.28f, 1.28f));
				Vector2 velocity = heading * (AttackRange(2.4f, 8.6f) * power);
				Color tint = Color.Lerp(color, Pale, AttackRange(0f, 0.32f));
				float width = AttackRange(1.45f, 3.3f) * (0.58f + power * 0.55f);
				particles.Spawn(center + heading * AttackRange(1f, 7f), velocity, tint, width,
					Math.Min(1f, 0.38f + 0.56f * power), attackSparkRandom.Next(12, 21) + (int)(power * 8f));
			}
		}

		private float AttackRange(float min, float max) => MathHelper.Lerp(min, max, attackSparkRandom.NextSingle());
	}
}
