using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.NPCs;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	internal sealed class DomainRewind : ModSystem
	{
		internal const int RequiredTier = 8;
		private const int PlaybackTicks = 120;
		private readonly List<RewindTarget> targets = new();
		private readonly bool[] interruptedGroups = new bool[Main.maxNPCs];
		private uint tick;
		private int playbackAge;
		private Vector2 castCenter;
		private float castRadius;

		private readonly record struct RewindTarget(NPC Npc, GhosteyeGlobalNPC History, int Group, Vector2[] Path)
		{
			public Vector2 PositionAt(float sample)
			{
				int earlier = (int)sample;
				int later = Math.Min(earlier + 1, Path.Length - 1);
				return Vector2.Lerp(Path[earlier], Path[later], sample - earlier);
			}
		}

		internal bool IsRewinding => targets.Count > 0;

		public override void OnWorldUnload()
		{
			tick = 0;
			targets.Clear();
		}

		public override void PostUpdateNPCs()
		{
			tick++;
			if (Main.netMode != NetmodeID.SinglePlayer || GhosteyePlayer.CalculateTier() < RequiredTier
				|| !Main.LocalPlayer.GetModPlayer<GhosteyePlayer>().Equipped) return;

			if (IsRewinding) AdvancePlayback();

			// 鬼域关闭时也留存位置，这样开域后即可回溯。
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (IsEnemy(npc)) npc.GetGlobalNPC<GhosteyeGlobalNPC>().RecordPosition(npc, tick);
			}
		}

		internal bool TryStartRewind(Vector2 center, float radius)
		{
			if (IsRewinding) return false;

			Span<bool> selected = stackalloc bool[Main.maxNPCs];
			Span<bool> blocked = stackalloc bool[Main.maxNPCs];
			Span<bool> hasMovement = stackalloc bool[Main.maxNPCs];
			selected.Clear();
			blocked.Clear();
			hasMovement.Clear();
			float radiusSquared = radius * radius;

			// 共享本体的分节怪物要整组校验，再一起移动。
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!IsEnemy(npc)) continue;

				int group = GroupOf(npc);
				if (Vector2.DistanceSquared(npc.Center, center) <= radiusSquared) selected[group] = true;

				GhosteyeGlobalNPC history = npc.GetGlobalNPC<GhosteyeGlobalNPC>();
				if (!history.TryGetPastPosition(tick, out Vector2 destination) || !CanLand(npc, destination))
					blocked[group] = true;
				else if (!hasMovement[group] && history.HasRecordedMovement(npc.position))
					hasMovement[group] = true;
			}

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (!IsEnemy(npc)) continue;

				int group = GroupOf(npc);
				if (!selected[group] || blocked[group] || !hasMovement[group]) continue;

				GhosteyeGlobalNPC history = npc.GetGlobalNPC<GhosteyeGlobalNPC>();
				targets.Add(new RewindTarget(npc, history, group, history.CaptureRewindPath(npc.position)));
			}

			if (!IsRewinding) return false;

			Array.Clear(interruptedGroups);
			playbackAge = 0;
			castCenter = center;
			castRadius = radius;
			return true;
		}

		private float Progress => playbackAge / (float)PlaybackTicks;

		private float PlaybackSample => MathHelper.SmoothStep(GhosteyeGlobalNPC.HistoryTicks, 0f, Progress);

		private void AdvancePlayback()
		{
			playbackAge++;
			float sample = PlaybackSample;
			Span<Vector2> positions = stackalloc Vector2[targets.Count];

			// 怪物死亡、变形或路上有方块拦住时，该组留在安全一侧。
			for (int i = 0; i < targets.Count; i++)
			{
				RewindTarget target = targets[i];
				if (interruptedGroups[target.Group]) continue;

				Vector2 position = target.PositionAt(sample);
				NPC npc = target.Npc;
				if (!IsCurrent(target) || GroupOf(npc) != target.Group
					|| !target.History.TryGetPastPosition(tick - 1, out _) || !CanLand(npc, position))
				{
					interruptedGroups[target.Group] = true;
					continue;
				}
				positions[i] = position;
			}

			for (int i = 0; i < targets.Count; i++)
			{
				RewindTarget target = targets[i];
				if (!interruptedGroups[target.Group])
					target.History.MoveToPastPosition(target.Npc, positions[i]);
			}

			if (playbackAge >= PlaybackTicks) targets.Clear();
		}

		internal void DrawPlayback(float time, Rectangle view)
		{
			if (!IsRewinding) return;

			float strength = GhosteyeEffects.DrawRewind(castCenter, castRadius, Progress, time, view);
			float sample = PlaybackSample;
			foreach (RewindTarget target in targets)
			{
				if (interruptedGroups[target.Group] || !IsCurrent(target) || !view.Intersects(target.Npc.Hitbox)) continue;

				Vector2 previous = target.Npc.Center;
				for (int echo = 1; echo <= 3; echo++)
				{
					float earlierInPlayback = Math.Min(GhosteyeGlobalNPC.HistoryTicks, sample + echo * 7f);
					Vector2 point = target.PositionAt(earlierInPlayback) + target.Npc.Size / 2f;
					GhosteyeEffects.DrawRewindEcho(previous, point, strength * (1f - echo * 0.21f));
					previous = point;
				}
			}
		}

		private static bool IsCurrent(RewindTarget target) => IsEnemy(target.Npc)
			&& ReferenceEquals(Main.npc[target.Npc.whoAmI], target.Npc)
			&& ReferenceEquals(target.Npc.GetGlobalNPC<GhosteyeGlobalNPC>(), target.History);

		private static bool IsEnemy(NPC npc) => npc.active && npc.life > 0 && !npc.friendly
			&& !NPCID.Sets.CountsAsCritter[npc.type];

		private static int GroupOf(NPC npc) => (uint)npc.realLife < Main.maxNPCs && IsEnemy(Main.npc[npc.realLife])
			? npc.realLife
			: npc.whoAmI;

		private static bool CanLand(NPC npc, Vector2 position)
		{
			Vector2 farCorner = position + npc.Size;
			if (!WorldGen.InWorld((int)(position.X / 16f), (int)(position.Y / 16f), 1)
				|| !WorldGen.InWorld((int)(farCorner.X / 16f), (int)(farCorner.Y / 16f), 1)) return false;

			return npc.noTileCollide || !Collision.SolidCollision(position, npc.width, npc.height);
		}
	}
}
