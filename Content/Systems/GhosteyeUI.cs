using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public partial class GhosteyeUI : ModSystem
	{
		private const int HudWidth = 440;
		private const int HudHeight = 73;
		private const int GuideWidth = 776;
		private const int GuideHeight = 484;
		private static readonly Rectangle GuideButton = new(336, 12, 90, 26);
		private static readonly Rectangle CloseButton = new(728, 16, 32, 28);
		private UserInterface ui;
		private GhosteyeState state;
		private bool guideOpen;
		private int selectedSkill;

		internal bool GuideOpen => guideOpen && CanShow;

		private static bool CanShow => !Main.dedServ && !Main.gameMenu && !Main.hideUI
			&& !Main.playerInventory && !Main.ingameOptionsWindow && !Main.inFancyUI && !Main.mapFullscreen
			&& Main.LocalPlayer.active && !Main.LocalPlayer.dead
			&& Main.LocalPlayer.GetModPlayer<GhosteyePlayer>().Equipped;

		public override void Load()
		{
			if (Main.dedServ) return;

			ui = new UserInterface();
			state = new GhosteyeState(this);
		}

		public override void OnWorldUnload()
		{
			guideOpen = false;
			selectedSkill = 0;
			ui?.SetState(null);
		}

		public override void Unload()
		{
			ui = null;
			state = null;
		}

		internal void ToggleGuide()
		{
			if (CanShow) guideOpen = !guideOpen;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!CanShow)
			{
				guideOpen = false;
				ui?.SetState(null);
				return;
			}

			if (ui.CurrentState == null) ui.SetState(state);
			ui.Update(gameTime);
			if (guideOpen || GuideButton.Contains(HudLayout.ToLocal(ui.MousePosition)))
				Main.LocalPlayer.mouseInterface = true;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Resource Bars");
			if (index < 0) index = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");
			if (index < 0) return;

			layers.Insert(index, new LegacyGameInterfaceLayer("SpectesGaze: Ghosteye",
				() =>
				{
					if (CanShow && ui?.CurrentState != null) ui.Draw(Main.spriteBatch, new GameTime());
					return true;
				}, InterfaceScaleType.UI));
		}

		private readonly record struct Layout(Vector2 Origin, float Scale)
		{
			public Point ToLocal(Vector2 point) => ((point - Origin) / Scale).ToPoint();
		}

		private Layout HudLayout => GetLayout(HudWidth, HudHeight, centered: false);
		private Layout GuideLayout => GetLayout(GuideWidth, GuideHeight, centered: true);

		private Layout GetLayout(int width, int height, bool centered)
		{
			CalculatedStyle dimensions = ui.GetDimensions();
			Vector2 view = new(dimensions.Width, dimensions.Height);
			float scale = MathHelper.Clamp(Math.Min((view.X - 20f) / width, (view.Y - 20f) / height), 0.1f, 1f);
			Vector2 origin = new((view.X - width * scale) / 2f, centered ? (view.Y - height * scale) / 2f : 9f);
			return new Layout(origin, scale);
		}

		private void Click(Vector2 pointer)
		{
			if (!CanShow) return;

			if (!guideOpen)
			{
				if (GuideButton.Contains(HudLayout.ToLocal(pointer))) ToggleGuide();
				return;
			}

			Point position = GuideLayout.ToLocal(pointer);
			if (CloseButton.Contains(position))
			{
				guideOpen = false;
				return;
			}

			for (int i = 0; i < Skills.Length; i++)
			{
				if (!SkillRow(i).Contains(position)) continue;
				selectedSkill = i;
				return;
			}
		}

		private sealed class GhosteyeState(GhosteyeUI owner) : UIState
		{
			public override void LeftMouseDown(UIMouseEvent evt)
			{
				base.LeftMouseDown(evt);
				owner.Click(evt.MousePosition);
			}

			protected override void DrawSelf(SpriteBatch spriteBatch)
			{
				GhosteyePlayer eye = Main.LocalPlayer.GetModPlayer<GhosteyePlayer>();
				int tier = GhosteyePlayer.CalculateTier();
				Vector2 mouse = owner.ui.MousePosition;
				Layout layout = owner.guideOpen ? owner.GuideLayout : owner.HudLayout;
				if (owner.guideOpen)
					owner.DrawGuide(new Canvas(spriteBatch, layout), eye, tier, layout.ToLocal(mouse));
				else
					DrawHud(new Canvas(spriteBatch, layout), eye, tier, layout.ToLocal(mouse));
			}
		}

		private static string Text(string key) => Language.GetTextValue("Mods.SpectesGaze.GhosteyeUI." + key);

		private static string Text(string key, object arg) =>
			Language.GetTextValue("Mods.SpectesGaze.GhosteyeUI." + key, arg);

		private static string Binding(ModKeybind key)
		{
			InputMode mode = PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;
			var keys = key?.GetAssignedKeys(mode);
			return keys is { Count: > 0 } ? string.Join(" / ", keys) : Text("Unbound");
		}

		private static int Seconds(int ticks) => (ticks + 59) / 60;

		private static void DrawHud(Canvas art, GhosteyePlayer eye, int tier, Point mouse)
		{
			art.Frame(new Rectangle(0, 0, HudWidth, HudHeight), compact: true);
			art.Sprite(Eye(tier), new Vector2(32f, 34f), new Vector2(42f), Color.White);
			art.Line(new Vector2(59f, 13f), new Vector2(59f, 43f), 1f, Rim);

			bool mastered = tier >= GhosteyePlayer.AwakenedTier;
			art.Label(Text("Tier", tier), 71, 12, Bone, 0.84f, 85);
			art.Label(DomainStatus(eye, mastered), 163, 12, eye.DomainOn ? PulseRed : Dust, 0.78f, 157);
			art.Fill(new Rectangle(71, 34, 137, 5), new Color(61, 24, 38));
			float revival = MathHelper.Clamp(eye.Revival / GhosteyePlayer.MaxRevival, 0f, 1f);
			float fill = mastered ? 1f : revival;
			art.Fill(new Rectangle(71, 34, (int)(137 * fill), 5), mastered ? new Color(206, 166, 151) : PulseRed);
			art.Label(mastered ? Text("RevivalEnded") : Text("Revival", (int)eye.Revival), 217, 26, Bone, 0.72f, 105);
			art.Button(GuideButton, Text("OpenGuide"), GuideButton.Contains(mouse));

			art.Line(new Vector2(15f, 47f), new Vector2(425f, 47f), 1f, Rim);
			if (!eye.DomainOn)
			{
				art.Label(Text("OpenDomainWith", Binding(SpectesGaze.GazeKey)), 220, 53, Dust, 0.71f, 385, 0.5f);
				return;
			}
			if (tier < 5)
			{
				art.Label(Text("DomainDiameter", GhosteyePlayer.DomainRadiusOf(tier) / 8f), 220, 53, Dust, 0.71f, 385, 0.5f);
				return;
			}

			int count = Math.Min(tier, GhosteyePlayer.OverlapTier) - 4;
			float width = 410f / count;
			Slot(0, "BeamShort", eye.BeamCooldown);
			if (tier >= 6) Slot(1, "StillShort", eye.StillCooldown);
			if (tier >= 7) Slot(2, "RestartShort", eye.RestartCooldown);
			if (tier >= 8)
			{
				string status = Main.netMode != NetmodeID.SinglePlayer ? Text("Solo")
					: ModContent.GetInstance<DomainRewind>().IsRewinding ? Text("Rewinding") : null;
				Slot(3, "RewindShort", eye.RewindCooldown, status);
			}
			if (tier >= GhosteyePlayer.OverlapTier)
				Slot(4, "OverlapShort", eye.OverlapCooldown,
					eye.OverlapTime > 0 ? $"×{eye.AttackHitCount(tier)} · {Seconds(eye.OverlapTime)}s" : null);

			void Slot(int index, string label, int cooldown, string status = null)
			{
				bool ready = cooldown == 0 || mastered;
				string value = status ?? (ready ? Text("Ready") : $"{Seconds(cooldown)}s");
				float x = 15 + (index + 0.5f) * width;
				art.Label($"{Text(label)} {value}", x, 53, ready || status != null ? Bone : Dust, 0.73f, width - 7, 0.5f);
			}
		}

		private static string DomainStatus(GhosteyePlayer eye, bool mastered)
		{
			if (eye.DomainOn) return Text(mastered ? "DomainAwakened" : "DomainOpen");
			return eye.DomainCooldown > 0 && !mastered ? Text("DomainLock", Seconds(eye.DomainCooldown)) : Text("DomainClosed");
		}
	}
}
