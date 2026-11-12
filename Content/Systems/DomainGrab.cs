using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.NPCs;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	internal sealed class DomainGrab : ModSystem
	{
		private const float CursorReach = 42f;
		private const float DragSpeed = 14f;
		private Entity held;
		private int heldSlot;
		private int heldType;
		private Vector2 npcPositionBeforeUpdate;

		public override void OnWorldUnload() => held = null;

		internal bool Holds(Entity entity) => held != null && ReferenceEquals(held, entity);

		internal void ReleaseIfHolding(Entity entity)
		{
			if (Holds(entity)) held = null;
		}

		internal void HandleInput(Player player, bool domainOn)
		{
			ModKeybind key = SpectesGaze.GrabKey;
			if (key?.Current != true || !domainOn || player.dead || player.mouseInterface)
			{
				held = null;
				return;
			}
			if (!key.JustPressed) return;

			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				Main.NewText("鬼域抓取目前仅在单人模式可用。", 190, 130, 145);
				return;
			}

			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			Vector2 cursor = Main.MouseWorld;
			if (Vector2.DistanceSquared(player.Center, cursor) > radius * radius) return;

			FindTarget(player.Center, cursor, radius, tier);
		}

		private void FindTarget(Vector2 center, Vector2 cursor, float radius, int tier)
		{
			float bestDistance = CursorReach * CursorReach;
			float radiusSquared = radius * radius;
			held = null;

			for (int i = 0; i < Main.maxItems; i++)
			{
				Item item = Main.item[i];
				if (item.active && !item.IsAir) Consider(item, i, item.type);
			}

			if (!ModContent.GetInstance<DomainRewind>().IsRewinding)
			{
				for (int i = 0; i < Main.maxNPCs; i++)
				{
					NPC npc = Main.npc[i];
					if (CanGrabNpc(npc, tier)) Consider(npc, i, npc.type);
				}
			}

			void Consider(Entity entity, int slot, int type)
			{
				if (Vector2.DistanceSquared(center, entity.Center) > radiusSquared) return;

				Vector2 nearest = Vector2.Clamp(cursor, entity.position, entity.position + entity.Size);
				float distance = Vector2.DistanceSquared(cursor, nearest);
				if (distance >= bestDistance) return;

				held = entity;
				heldSlot = slot;
				heldType = type;
				bestDistance = distance;
			}
		}

		private static bool CanGrabNpc(NPC npc, int tier)
		{
			if (!npc.active || npc.life <= 0 || npc.immortal || npc.dontTakeDamage) return false;
			if (npc.boss || npc.realLife >= 0 || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
				|| NPCID.Sets.ProjectileNPC[npc.type] || IsVanillaBossPart(npc.type)) return false;
			if (NPCID.Sets.CountsAsCritter[npc.type]) return true;

			return tier >= 8 || (tier >= 4 && !npc.friendly && !npc.townNPC);
		}

		private static bool IsVanillaBossPart(int type) => type is NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail
			or NPCID.Creeper or NPCID.SkeletronHand or NPCID.WallofFleshEye
			or NPCID.PrimeCannon or NPCID.PrimeLaser or NPCID.PrimeSaw or NPCID.PrimeVice
			or NPCID.PlanterasHook or NPCID.PlanterasTentacle
			or NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight
			or NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordFreeEye;

		private bool ValidateHeld()
		{
			if (held == null) return false;

			Player player = Main.LocalPlayer;
			GhosteyePlayer eye = player.GetModPlayer<GhosteyePlayer>();
			int tier = GhosteyePlayer.CalculateTier();
			float radius = GhosteyePlayer.DomainRadiusOf(tier);
			bool valid = Main.netMode == NetmodeID.SinglePlayer && player.active && !player.dead && !player.mouseInterface
				&& eye.Equipped && eye.DomainOn && SpectesGaze.GrabKey?.Current == true
				&& Vector2.DistanceSquared(player.Center, held.Center) <= radius * radius;

			if (valid && held is Item item)
				valid = ReferenceEquals(Main.item[heldSlot], item) && item.active && !item.IsAir && item.type == heldType;
			else if (valid && held is NPC npc)
				valid = ReferenceEquals(Main.npc[heldSlot], npc) && npc.type == heldType && CanGrabNpc(npc, tier)
					&& !ModContent.GetInstance<DomainRewind>().IsRewinding;

			if (!valid) held = null;
			return valid;
		}

		public override void PreUpdateNPCs()
		{
			if (held is NPC npc && ValidateHeld()) npcPositionBeforeUpdate = npc.position;
		}

		public override void PostUpdateNPCs()
		{
			if (held is not NPC npc || !ValidateHeld()) return;

			Vector2 position = DragPosition(npc, npcPositionBeforeUpdate);
			npc.GetGlobalNPC<GhosteyeGlobalNPC>().MoveToPosition(npc, position);
			npc.velocity = Vector2.Zero;
		}

		public override void PostUpdateItems()
		{
			if (held is not Item item || !ValidateHeld()) return;

			item.position = DragPosition(item, item.position);
			item.velocity = Vector2.Zero;
		}

		private static Vector2 DragPosition(Entity entity, Vector2 position)
		{
			Vector2 playerCenter = Main.LocalPlayer.Center;
			float radius = GhosteyePlayer.DomainRadiusOf(GhosteyePlayer.CalculateTier());
			Vector2 cursorOffset = Main.MouseWorld - playerCenter;
			if (cursorOffset.LengthSquared() > radius * radius)
				cursorOffset = cursorOffset.SafeNormalize(Vector2.Zero) * (radius - 2f);

			Vector2 destination = playerCenter + cursorOffset - entity.Size / 2f;
			Vector2 step = (destination - position) * 0.48f;
			if (step.LengthSquared() > DragSpeed * DragSpeed)
				step = step.SafeNormalize(Vector2.Zero) * DragSpeed;

			step = Collision.TileCollision(position, step, entity.width, entity.height, fallThrough: true);
			Vector2 candidate = position + step;
			Vector2 farCorner = candidate + entity.Size;
			if (Vector2.DistanceSquared(playerCenter, candidate + entity.Size / 2f) > radius * radius
				|| !WorldGen.InWorld((int)(candidate.X / 16f), (int)(candidate.Y / 16f), 1)
				|| !WorldGen.InWorld((int)(farCorner.X / 16f), (int)(farCorner.Y / 16f), 1)
				|| Collision.SolidCollision(candidate, entity.width, entity.height))
				return position;

			return candidate;
		}

		internal void Draw(float time)
		{
			if (held == null || !held.active) return;

			Player player = Main.LocalPlayer;
			Vector2 origin = GhosteyePlayer.CalculateTier() >= 3
				? player.GetModPlayer<GhosteyePlayer>().FloatingEyeCenter
				: player.Center;
			Vector2 target = held.Center;
			Vector2 direction = (target - origin).SafeNormalize(Vector2.UnitX);
			Vector2 normal = new(-direction.Y, direction.X);
			Vector2 previous = origin;
			for (int i = 1; i <= 14; i++)
			{
				float progress = i / 14f;
				float taper = 4f * progress * (1f - progress);
				float curl = FleshNoise.Perlin(progress * 9.5f, time * 3.2f, 175) * 19f;
				Vector2 point = Vector2.Lerp(origin, target, progress) + normal * (curl * taper);
				float variation = FleshNoise.Unit((uint)i * 719);
				DomainRenderer.DrawLine(previous, point, 2.5f + variation * 5.5f,
					DomainRenderer.Light(DomainRenderer.Blood, 0.25f + variation * 0.25f));
				if (variation > 0.78f)
				{
					Vector2 sag = new(variation * 8f - 4f, 6f + taper * 10f);
					DomainRenderer.DrawLine(point, point + sag, 1.25f,
						DomainRenderer.Light(DomainRenderer.Crimson, 0.23f));
				}
				previous = point;
			}
		}
	}
}
