using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze
{
	// 模组主类。全局的东西（按键、配置）放这里，业务逻辑不写在这。
	public class SpectesGaze : Mod
	{
		// 开 / 关鬼域。默认不绑定，玩家自己去 设置 -> 按键绑定 里绑。
		public static ModKeybind GazeKey { get; private set; }

		// 主动大激光。第五层起可用，同样默认不绑定。
		public static ModKeybind BeamKey { get; private set; }
		public static ModKeybind BloodFlightKey { get; private set; }

		// 静止。第六层起可用。
		public static ModKeybind StillKey { get; private set; }

		// 重启自身。第七层起可用。
		public static ModKeybind RestartKey { get; private set; }

		// 大范围重启。第八层起可用。
		public static ModKeybind RewindKey { get; private set; }

		// 九层起手动叠加，十层取消时限。
		public static ModKeybind OverlapKey { get; private set; }

		public static ModKeybind GrabKey { get; private set; }
		public static ModKeybind GuideKey { get; private set; }

		public override void Load()
		{
			GazeKey = KeybindLoader.RegisterKeybind(this, "OpenDomain", "None");
			BeamKey = KeybindLoader.RegisterKeybind(this, "FireBeam", "None");
			BloodFlightKey = KeybindLoader.RegisterKeybind(this, "BloodFlight", "V");
			StillKey = KeybindLoader.RegisterKeybind(this, "FreezeDomain", "None");
			RestartKey = KeybindLoader.RegisterKeybind(this, "RestartSelf", "None");
			RewindKey = KeybindLoader.RegisterKeybind(this, "RewindDomain", "None");
			OverlapKey = KeybindLoader.RegisterKeybind(this, "OverlapDomain", "None");
			GrabKey = KeybindLoader.RegisterKeybind(this, "GrabInDomain", "Z");
			GuideKey = KeybindLoader.RegisterKeybind(this, "GhosteyeGuide", "J");
		}

		public override void Unload()
		{
			// 必须置空，否则重载模组时静态字段会残留旧实例。
			GazeKey = null;
			BeamKey = null;
			BloodFlightKey = null;
			StillKey = null;
			RestartKey = null;
			RewindKey = null;
			OverlapKey = null;
			GrabKey = null;
			GuideKey = null;
		}

		private const byte BloodFlightPacket = 1;
		private const byte AwakenedDomainPacket = 2;
		private const byte TimedOverlapPacket = 3;
		private const byte PermanentOverlapPacket = 4;
		private const byte SirenCurseClearedPacket = 5;

		internal void SendBloodFlight(int player, bool active, int toWho = -1, int ignoreWho = -1) =>
			SendPlayerToggle(BloodFlightPacket, player, active, toWho, ignoreWho);

		internal void SendAwakenedDomain(int player, bool active, int toWho = -1, int ignoreWho = -1) =>
			SendPlayerToggle(AwakenedDomainPacket, player, active, toWho, ignoreWho);

		internal void SendTimedOverlap(int player, bool active, int toWho = -1, int ignoreWho = -1) =>
			SendPlayerToggle(TimedOverlapPacket, player, active, toWho, ignoreWho);

		internal void SendPermanentOverlap(int player, bool active, int toWho = -1, int ignoreWho = -1) =>
			SendPlayerToggle(PermanentOverlapPacket, player, active, toWho, ignoreWho);

		internal void SendSirenCurseCleared(int player, int toWho = -1, int ignoreWho = -1) =>
			SendPlayerToggle(SirenCurseClearedPacket, player, true, toWho, ignoreWho);

		private void SendPlayerToggle(byte kind, int player, bool active, int toWho, int ignoreWho)
		{
			ModPacket packet = GetPacket();
			packet.Write(kind);
			packet.Write((byte)player);
			packet.Write(active);
			packet.Send(toWho, ignoreWho);
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			byte kind = reader.ReadByte();
			if (kind is not (BloodFlightPacket or AwakenedDomainPacket or TimedOverlapPacket or PermanentOverlapPacket or SirenCurseClearedPacket)) return;
			int playerIndex = reader.ReadByte();
			bool active = reader.ReadBoolean();
			bool server = Main.netMode == NetmodeID.Server;
			if (playerIndex >= Main.maxPlayers || (server && playerIndex != whoAmI)
				|| (!server && playerIndex == Main.myPlayer)) return;

			Player player = Main.player[playerIndex];
			int tier = GhosteyePlayer.CalculateTier();
			bool tierAllowed = kind switch
			{
				BloodFlightPacket => tier >= GhosteyePlayer.BloodFlightTier,
				AwakenedDomainPacket or PermanentOverlapPacket or SirenCurseClearedPacket => tier >= GhosteyePlayer.AwakenedTier,
				_ => tier == GhosteyePlayer.OverlapTier
			};
			if (active && (!player.active || player.dead || !tierAllowed)) return;
			GhosteyePlayer eye = player.GetModPlayer<GhosteyePlayer>();
			if (kind == BloodFlightPacket)
				eye.SetBloodFlightVisual(active);
			else if (kind == AwakenedDomainPacket)
				eye.ReceiveAwakenedDomain(active);
			else if (kind == TimedOverlapPacket)
				eye.ReceiveTimedOverlap(active);
			else if (kind == SirenCurseClearedPacket)
			{
				if (!active || (server && !eye.ProtectsFromDeath)) return;
				eye.SirenCurseCleared = true;
			}
			else
				eye.ReceivePermanentOverlap(active);
			if (server) SendPlayerToggle(kind, playerIndex, active, -1, whoAmI);
		}
	}
}
