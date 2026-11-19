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
		private MethodInfo clearCurse;
		private Action<Player> stopMusicBoxes;
		private Func<bool> worldIsResolving;
		private FieldInfo isCursed;

		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("CalamityOverhaul", out Mod overhaul)) return;
			Type playerType = overhaul.Code.GetType(ToolsNamespace + "SirenMusicalBoxPlayer");
			Type systemType = overhaul.Code.GetType(ToolsNamespace + "SirenMusicalSystem");
			MethodInfo executeDeath = playerType?.GetMethod("ExecuteDeath", InstanceMethods, Type.EmptyTypes);
			MethodInfo preUpdate = playerType?.GetMethod(nameof(ModPlayer.PreUpdate), InstanceMethods, Type.EmptyTypes);
			clearCurse = playerType?.GetMethod("ClearCurse", InstanceMethods, new[] { typeof(bool) });
			isCursed = playerType?.GetField("IsCursed", InstanceMethods);
			MethodInfo stopBoxes = playerType?.GetMethod("StopAllMusicBoxes", StaticMethods, new[] { typeof(Player) });
			MethodInfo resolving = systemType?.GetProperty("ResolveDeath", StaticMethods)?.GetGetMethod(nonPublic: true);
			if (executeDeath == null || preUpdate == null || clearCurse == null || isCursed?.FieldType != typeof(bool)
				|| stopBoxes == null || resolving == null)
			{
				Mod.Logger.Warn("Calamity Overhaul's Siren Musical Box curse methods could not be resolved.");
				return;
			}

			stopMusicBoxes = stopBoxes.CreateDelegate<Action<Player>>();
			worldIsResolving = resolving.CreateDelegate<Func<bool>>();
			// Detours need the concrete instance type, which belongs to the optional mod.
			MethodInfo install = typeof(SirenCurseProtection).GetMethod(nameof(InstallHooks), InstanceMethods);
			try
			{
				install.MakeGenericMethod(playerType).Invoke(this, new object[] { executeDeath, preUpdate });
			}
			catch (TargetInvocationException ex)
			{
				Unload();
				Mod.Logger.Warn("Could not install Siren Musical Box curse protection.", ex.InnerException);
			}
		}

		private void InstallHooks<T>(MethodInfo executeDeath, MethodInfo preUpdate) where T : ModPlayer
		{
			deathHook = new Hook(executeDeath, (Action<Action<T>, T>)ExecuteDeath);
			updateHook = new Hook(preUpdate, (Action<Action<T>, T>)PreUpdateCurse);
		}

		public override void Unload()
		{
			updateHook?.Dispose();
			deathHook?.Dispose();
			updateHook = deathHook = null;
		}

		private void ExecuteDeath<T>(Action<T> orig, T siren) where T : ModPlayer
		{
			Player player = siren.Player;
			if (!player.active || player.dead || !player.TryGetModPlayer(out GhosteyePlayer eye) || !eye.ProtectsFromDeath)
			{
				orig(siren);
				return;
			}

			eye.RestoreFromDeath();
			eye.SirenCurseCleared = worldIsResolving();
			isCursed.SetValue(siren, false);

			if (Main.netMode == NetmodeID.SinglePlayer && eye.SirenCurseCleared)
				stopMusicBoxes(player);
			else
				clearCurse.Invoke(siren, new object[] { true });
		}

		private void PreUpdateCurse<T>(Action<T> orig, T siren) where T : ModPlayer
		{
			Player player = siren.Player;
			if (!player.TryGetModPlayer(out GhosteyePlayer eye))
			{
				orig(siren);
				return;
			}

			if (!worldIsResolving()) eye.SirenCurseCleared = false;
			// 客户端结算自己的代码杀，其他端停止给已经保护的玩家续上同一轮诅咒。
			else if (!eye.SirenCurseCleared && player.whoAmI != Main.myPlayer && eye.ProtectsFromDeath)
			{
				clearCurse.Invoke(siren, new object[] { false });
				eye.SirenCurseCleared = true;
			}

			if (eye.SirenCurseCleared)
			{
				isCursed.SetValue(siren, false);
				return;
			}

			orig(siren);
		}
	}
}
