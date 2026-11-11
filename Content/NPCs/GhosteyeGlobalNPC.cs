using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using SpectesGaze.Content.Buffs;
using SpectesGaze.Content.Players;
using SpectesGaze.Content.Systems;

namespace SpectesGaze.Content.NPCs
{
	/// <summary>
	/// 敌人身上的全局修改：减速、鬼火灼烧、静止。
	/// </summary>
	public class GhosteyeGlobalNPC : GlobalNPC
	{
		internal const int HistoryTicks = 120;
		private Vector2? frozenPosition;
		private Vector2[] positionHistory;
		private int nextSnapshot;
		private int snapshotCount;
		private uint lastSnapshotTick;

		public override bool InstancePerEntity => true;

		public override void SetDefaults(NPC npc)
		{
			frozenPosition = null;
			nextSnapshot = 0;
			snapshotCount = 0;
		}

		internal void RecordPosition(NPC npc, uint tick)
		{
			positionHistory ??= new Vector2[HistoryTicks];
			if (tick != lastSnapshotTick + 1) snapshotCount = 0;

			positionHistory[nextSnapshot] = npc.position;
			nextSnapshot = (nextSnapshot + 1) % HistoryTicks;
			if (snapshotCount < HistoryTicks) snapshotCount++;
			lastSnapshotTick = tick;
		}

		internal bool TryGetPastPosition(uint tick, out Vector2 position)
		{
			if (snapshotCount == HistoryTicks && lastSnapshotTick == tick)
			{
				position = positionHistory[nextSnapshot];
				return true;
			}

			position = default;
			return false;
		}

		internal Vector2[] CaptureRewindPath(Vector2 currentPosition)
		{
			// 由旧到新复制快照，实时记录可继续使用原来的环形缓冲。
			Vector2[] path = new Vector2[HistoryTicks + 1];
			int firstPart = HistoryTicks - nextSnapshot;
			System.Array.Copy(positionHistory, nextSnapshot, path, 0, firstPart);
			System.Array.Copy(positionHistory, 0, path, firstPart, nextSnapshot);
			path[HistoryTicks] = currentPosition;
			return path;
		}

		internal bool HasRecordedMovement(Vector2 currentPosition)
		{
			foreach (Vector2 sample in positionHistory)
			{
				if (sample != currentPosition) return true;
			}
			return false;
		}

		internal void MoveToPosition(NPC npc, Vector2 position)
		{
			npc.position = position;
			npc.oldPosition = position;
			if (frozenPosition.HasValue) frozenPosition = position;
		}

		public override void OnSpawn(NPC npc, IEntitySource source) =>
			ModContent.GetInstance<DomainGrab>().ReleaseIfHolding(npc);

		public override bool PreAI(NPC npc)
		{
			if (!ModContent.GetInstance<DomainGrab>().Holds(npc)) return true;

			npc.velocity = Vector2.Zero;
			return false;
		}

		public override void PostAI(NPC npc)
		{
			bool slowed = npc.HasBuff(ModContent.BuffType<EyeSlow>());
			bool still = npc.HasBuff(ModContent.BuffType<GhostStill>());

			// 静止优先级最高，减速不用再叠。
			if (still)
			{
				// 第一次进入静止时记住位置，之后每帧强制拉回去。
				// 只清 velocity 不够，有些 AI 直接改 position（瞬移类）。
				frozenPosition ??= npc.position;
				npc.position = frozenPosition.Value;
				npc.velocity = Vector2.Zero;
				return;
			}

			frozenPosition = null;

			if (slowed)
			{
				// AI 跑完之后再改速度，才不会被 AI 覆盖。
				// 0.88 配合各种怪自己的摩擦，大约相当于减速 30%。
				npc.velocity *= 0.88f;
			}
		}

		/// <summary>
		/// 鬼火灼烧的持续伤害。层数越高烧得越快：
		/// 4 层 30/秒，5~6 层 60/秒，7 层以上 150/秒。
		/// lifeRegen 的单位是每帧累积值，每秒伤害等于 -lifeRegen / 2。
		/// </summary>
		public override void UpdateLifeRegen(NPC npc, ref int damage)
		{
			if (!npc.HasBuff(ModContent.BuffType<GhostFlame>())) return;

			int tier = GhosteyePlayer.CalculateTier();

			int dps;
			if (tier >= 7) dps = 150;
			else if (tier >= 5) dps = 60;
			else dps = 30;

			// 先把正向回血清零，否则高回血的怪会把伤害吃掉。
			if (npc.lifeRegen > 0) npc.lifeRegen = 0;

			npc.lifeRegen -= dps * 2;

			// damage 决定跳字大小，给个合理值。
			if (damage < dps / 2) damage = dps / 2;
		}
	}
}
