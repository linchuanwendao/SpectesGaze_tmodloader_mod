using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public sealed class SirenCurseProtection : ModSystem
	{
		private const string ToolsNamespace = "CalamityOverhaul.Content.Items.Tools.";
		private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		private const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private Hook deathHook;
		private Hook updateHook;
		private Hook musicHook;
		private Action stopSession;
		private Func<bool> sessionActive;

		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("CalamityOverhaul", out Mod overhaul)) return;
			Type playerType = overhaul.Code.GetType(ToolsNamespace + "SirenMusicalBoxPlayer");
			Type systemType = overhaul.Code.GetType(ToolsNamespace + "SirenMusicalSystem");
			MethodInfo executeDeath = playerType?.GetMethod("ExecuteDeath", InstanceMethods, Type.EmptyTypes);
			MethodInfo preUpdate = playerType?.GetMethod(nameof(ModPlayer.PreUpdate), InstanceMethods, Type.EmptyTypes);
			MethodInfo stop = systemType?.GetMethod("RequestForceStop", StaticMethods, Type.EmptyTypes);
			MethodInfo active = systemType?.GetProperty("Active", StaticMethods)?.GetGetMethod(nonPublic: true);
			if (executeDeath == null || preUpdate == null || stop == null || active == null)
			{
				Mod.Logger.Warn("Calamity Overhaul's Siren Musical Box curse methods could not be resolved.");
				return;
			}

			try
			{
				stopSession = stop.CreateDelegate<Action>();
				sessionActive = active.CreateDelegate<Func<bool>>();
				deathHook = new Hook(executeDeath, (Action<Action<ModPlayer>, ModPlayer>)ExecuteDeath);
				updateHook = new Hook(preUpdate, (Action<Action<ModPlayer>, ModPlayer>)PreUpdateCurse);
			}
			catch (Exception ex)
			{
				Unload();
				Mod.Logger.Warn("Could not install Siren Musical Box curse protection.", ex);
				return;
			}

			MethodInfo shouldPlay = overhaul.Code.GetType(ToolsNamespace + "SirenMusicClaim")
				?.GetMethod("ShouldPlay", InstanceMethods, Type.EmptyTypes);
			if (Main.dedServ || shouldPlay?.ReturnType != typeof(bool)) return;
			try
			{
				musicHook = new Hook(shouldPlay, (Func<Func<object, bool>, object, bool>)ShouldPlayMusic);
			}
			catch (Exception ex)
			{
				Mod.Logger.Warn("Could not suppress the Siren Musical Box track for a cleansed listener.", ex);
			}
		}

		public override void Unload()
		{
			musicHook?.Dispose();
			updateHook?.Dispose();
			deathHook?.Dispose();
			musicHook = updateHook = deathHook = null;
		}

		private void ExecuteDeath(Action<ModPlayer> orig, ModPlayer siren)
		{
			Player player = siren.Player;
			if (!player.active || player.dead || !player.TryGetModPlayer(out GhosteyePlayer eye) || !eye.ProtectsFromDeath)
			{
				orig(siren);
				return;
			}

			// 个人倒计时可能先于世界进入结算。单人直接结束本轮播放，防止下一帧重新附咒。
			if (Main.netMode == NetmodeID.SinglePlayer)
				stopSession();

			// 八音盒自己的复活回调同时清除存档诅咒、倒计时、IsCursed 和死亡触发标记。
			siren.OnRespawn();
			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				eye.SirenCurseCleared = sessionActive();
				if (eye.SirenCurseCleared)
					((SpectesGaze)Mod).SendSirenCurseCleared(player.whoAmI);
				siren.SyncPlayer(toWho: -1, fromWho: -1, newPlayer: false);
			}
			eye.RestoreFromDeath();
		}

		private void PreUpdateCurse(Action<ModPlayer> orig, ModPlayer siren)
		{
			if (siren.Player.TryGetModPlayer(out GhosteyePlayer eye) && eye.SirenCurseCleared)
			{
				// 同步包也可能带回旧诅咒。直到这轮播放结束，只清理这个已免死的玩家。
				siren.OnRespawn();
				if (sessionActive()) return;
				eye.SirenCurseCleared = false;
			}
			orig(siren);
		}

		private static bool ShouldPlayMusic(Func<object, bool> orig, object claim)
		{
			if (Main.LocalPlayer is { active: true } player && player.TryGetModPlayer(out GhosteyePlayer eye) && eye.SirenCurseCleared)
				return false;
			return orig(claim);
		}
	}
}
