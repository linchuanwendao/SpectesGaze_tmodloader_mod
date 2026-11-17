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

		// 鬼域叠加。第九层起可用。
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

		internal void SendBloodFlight(int player, bool active, int toWho = -1, int ignoreWho = -1)
		{
			ModPacket packet = GetPacket();
			packet.Write(BloodFlightPacket);
			packet.Write((byte)player);
			packet.Write(active);
			packet.Send(toWho, ignoreWho);
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			if (reader.ReadByte() != BloodFlightPacket) return;
			int playerIndex = reader.ReadByte();
			bool active = reader.ReadBoolean();
			bool server = Main.netMode == NetmodeID.Server;
			if (playerIndex >= Main.maxPlayers || (server && playerIndex != whoAmI)
				|| (!server && playerIndex == Main.myPlayer)) return;

			Player player = Main.player[playerIndex];
			// 始终用原版玩家移动同步。这里的状态只负责其他客户端的化虹演出。
			if (active && (player.dead || GhosteyePlayer.CalculateTier() < GhosteyePlayer.BloodFlightTier)) return;
			player.GetModPlayer<GhosteyePlayer>().SetBloodFlightVisual(active);
			if (server) SendBloodFlight(playerIndex, active, ignoreWho: whoAmI);
		}
	}
}
