using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public sealed class AwakenedDeathProtection : ModSystem
	{
		private readonly List<Hook> hooks = new();

		public override void Load()
		{
			// 直接拦在玩家入口，覆盖跳过 PreKill 的强制死亡。
			AddHook("KillMe", (On_Player.hook_KillMe)KillMe,
				typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool));
			AddHook("KillMeForGood", (On_Player.hook_KillMeForGood)KillMeForGood);
			AddHook("UpdateDead", (On_Player.hook_UpdateDead)UpdateDead);
			AddHook("Hurt", (On_Player.hook_Hurt_HurtInfo_bool)Hurt, typeof(Player.HurtInfo), typeof(bool));
			AddHook("Hurt", (On_Player.hook_Hurt_PlayerDeathReason_int_int_refHurtInfo_bool_bool_int_bool_float_float_float)Hurt,
				typeof(PlayerDeathReason), typeof(int), typeof(int), typeof(Player.HurtInfo).MakeByRefType(),
				typeof(bool), typeof(bool), typeof(int), typeof(bool), typeof(float), typeof(float), typeof(float));
		}

		public override void Unload()
		{
			foreach (Hook hook in hooks) hook.Dispose();
			hooks.Clear();
		}

		private void AddHook(string name, Delegate detour, params Type[] parameters)
		{
			MethodInfo method = typeof(Player).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null, types: parameters, modifiers: null) ?? throw new MissingMethodException(typeof(Player).FullName, name);
			hooks.Add(new Hook(method, detour, new DetourConfig("SpectesGaze.AwakenedDomain", int.MaxValue)));
		}

		private static bool PreventDeath(Player player)
		{
			if (!player.active || !player.TryGetModPlayer(out GhosteyePlayer eye) || !eye.ProtectsFromDeath) return false;
			eye.RestoreFromDeath();
			return true;
		}

		private static bool PreventDamage(Player player) =>
			player.active && player.TryGetModPlayer(out GhosteyePlayer eye) && eye.FullyAwakened;

		private static void KillMe(On_Player.orig_KillMe orig, Player player, PlayerDeathReason reason, double damage, int direction, bool pvp)
		{
			if (!PreventDeath(player)) orig(player, reason, damage, direction, pvp);
		}

		private static void KillMeForGood(On_Player.orig_KillMeForGood orig, Player player)
		{
			if (!PreventDeath(player)) orig(player);
		}

		private static void UpdateDead(On_Player.orig_UpdateDead orig, Player player)
		{
			if (!player.dead || !PreventDeath(player)) orig(player);
		}

		private static void Hurt(On_Player.orig_Hurt_HurtInfo_bool orig, Player player, Player.HurtInfo info, bool quiet)
		{
			if (!PreventDamage(player)) orig(player, info, quiet);
		}

		private static double Hurt(On_Player.orig_Hurt_PlayerDeathReason_int_int_refHurtInfo_bool_bool_int_bool_float_float_float orig,
			Player player, PlayerDeathReason source, int damage, int direction, out Player.HurtInfo info, bool pvp, bool quiet,
			int cooldownCounter, bool dodgeable, float armorPenetration, float scalingArmorPenetration, float knockback)
		{
			if (!PreventDamage(player))
				return orig(player, source, damage, direction, out info, pvp, quiet, cooldownCounter, dodgeable,
					armorPenetration, scalingArmorPenetration, knockback);
			info = default;
			return 0d;
		}
	}
}
