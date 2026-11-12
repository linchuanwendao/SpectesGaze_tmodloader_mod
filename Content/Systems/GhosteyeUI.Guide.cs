using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using SpectesGaze.Content.Players;

namespace SpectesGaze.Content.Systems
{
	public partial class GhosteyeUI
	{
		private sealed record Skill(string Id, int Tier, Func<ModKeybind> Key = null, bool InDomain = false, bool SoloOnly = false)
		{
			public string Name => Text($"Skills.{Id}.Name");
			public string Effect => Text($"Skills.{Id}.Effect");
			public string Input => Key != null ? Binding(Key()) : Text(Id == "Teleport" ? "RightClick" : "Automatic");
		}

		private static readonly Skill[] Skills =
		[
			new("Domain", 1, () => SpectesGaze.GazeKey),
			new("Teleport", 1, InDomain: true),
			new("Grab", 1, () => SpectesGaze.GrabKey, InDomain: true, SoloOnly: true),
			new("Sight", 3),
			new("Flame", 4),
			new("Bolt", 5, InDomain: true),
			new("Beam", 5, () => SpectesGaze.BeamKey, InDomain: true),
			new("Still", 6, () => SpectesGaze.StillKey, InDomain: true),
			new("Restart", 7, () => SpectesGaze.RestartKey, InDomain: true),
			new("Rewind", DomainRewind.RequiredTier, () => SpectesGaze.RewindKey, InDomain: true, SoloOnly: true),
			new("Overlap", GhosteyePlayer.OverlapTier, () => SpectesGaze.OverlapKey, InDomain: true),
			new("Awaken", GhosteyePlayer.AwakenedTier, InDomain: true)
		];

		private static Rectangle SkillRow(int index) => new(15, 91 + index * 29, 204, 26);

		private void DrawGuide(Canvas art, GhosteyePlayer eye, int tier, Point mouse)
		{
			art.Frame(new Rectangle(0, 0, GuideWidth, GuideHeight));
			art.Fill(new Rectangle(8, 5, GuideWidth - 16, 56), new Color(35, 11, 24));
			art.Seam(new Vector2(15, 63), new Vector2(761, 63), 71);
			art.Eye(tier, new Vector2(37, 31), new Vector2(58, 47), Color.White, seed: 286);
			art.Label(Text("Title"), 68, 9, Bone, 1.20f, 285);
			string domain = DomainStatus(eye, tier >= GhosteyePlayer.AwakenedTier);
			art.Label($"{Text("Tier", tier)}  ·  {Text("DomainDiameter", GhosteyePlayer.DomainRadiusOf(tier) / 8f)}  ·  {domain}",
				69, 37, Dust, 0.78f, 530);
			art.Button(CloseButton, "×", CloseButton.Contains(mouse));

			art.Label(Text("SkillList"), 17, 71, Dust, 0.72f);
			for (int i = 0; i < Skills.Length; i++)
			{
				Skill skill = Skills[i];
				Rectangle row = SkillRow(i);
				bool selected = i == selectedSkill;
				bool unlocked = tier >= skill.Tier;
				Color tint = unlocked ? Bone : Dust;
				if (selected || row.Contains(mouse))
				{
					art.Fill(row, selected ? new Color(86, 23, 42) : new Color(49, 15, 29));
					art.Fill(new Rectangle(row.X, row.Y, 2, row.Height), selected ? PulseRed : Rim);
				}
				else
				{
					art.Fill(row, new Color(29, 11, 21));
				}

				art.Eye(skill.Tier, new Vector2(row.X + 17, row.Y + 13), new Vector2(26, 20),
					Color.White * (unlocked ? 0.9f : 0.42f), seed: 31 + i * 47);
				art.Label(skill.Name, row.X + 34, row.Y + 4, tint, 0.78f, 110);
				art.Label(Text("Tier", skill.Tier), row.Right - 29, row.Y + 5, tint, 0.65f, 49, 0.5f);
			}

			art.Fill(new Rectangle(233, 72, 528, 367), new Color(26, 10, 19));
			DrawEyeArtwork(art, Skills[selectedSkill], tier);
			DrawSkill(art, Skills[selectedSkill], eye, tier);
			art.Seam(new Vector2(15, 451), new Vector2(761, 451), 137);
			art.Label(Text("SelectSkill"), 16, 459, Dust, 0.72f, 335);
			art.Label(Text("ToggleWith", Binding(SpectesGaze.GuideKey)), 759, 459, Dust, 0.72f, 335, 1f);
		}

		private static void DrawEyeArtwork(Canvas art, Skill skill, int tier)
		{
			float time = Main.GlobalTimeWrappedHourly;
			Vector2 center = new(663 + FleshNoise.Perlin(time * 0.5f, 4f, 12) * 4f,
				147 + FleshNoise.Perlin(5f, time * 0.7f, 71) * 5f);

			for (uint i = 1; i <= 9; i++)
			{
				uint seed = i * 7411;
				float angle = FleshNoise.Unit(seed) * MathHelper.TwoPi + FleshNoise.Perlin(time * 0.78f, i, seed + 3) * 0.37f;
				Vector2 radial = angle.ToRotationVector2();
				float length = 0.62f + 0.38f * FleshNoise.Unit(seed + 13);
				Vector2 root = center + radial * new Vector2(32, 21);
				Vector2 bend = center + radial * new Vector2(61, 36) * length
					+ new Vector2(FleshNoise.Perlin(time, i, seed + 52) * 11f, 7f);
				Vector2 end = center + radial * new Vector2(91, 53) * length;
				art.Line(root, bend, 2.4f + FleshNoise.Unit(seed + 29) * 4.4f, new Color(73, 17, 30));
				art.Line(bend, end, 1.2f, Rim);
				art.Line(bend, bend + (angle + 0.71f).ToRotationVector2() * (9f + 12f * length), 0.8f, PulseRed * 0.43f);
			}

			Color eyeColor = tier >= skill.Tier ? Color.White : new Color(170, 101, 114);
			art.Eye(skill.Tier, center, new Vector2(158, 120), eyeColor, seed: 401 + skill.Tier * 59);
		}

		private static void DrawSkill(Canvas art, Skill skill, GhosteyePlayer eye, int tier)
		{
			bool unlocked = tier >= skill.Tier;
			art.Label(Text(unlocked ? "Unlocked" : "Locked"), 252, 88, unlocked ? PulseRed : Dust, 0.75f);
			art.Label(skill.Name, 251, 112, Bone, 1.38f, 305);
			art.Label(Text("Control"), 252, 158, Dust, 0.72f);
			art.Label(skill.Input, 252, 177, Bone, 0.95f, 303);
			art.Seam(new Vector2(251, 218), new Vector2(743, 218), 42);
			art.Label(Text("Effect"), 252, 229, PulseRed, 0.79f);

			float lineY = 256f;
			foreach (string line in skill.Effect.Split('\n', StringSplitOptions.TrimEntries))
			{
				art.Label(line, 252, lineY, Bone, 0.94f, 491);
				lineY += 25f;
			}

			art.Seam(new Vector2(251, 376), new Vector2(743, 376), 93);
			art.Label(Text("Requirement"), 252, 388, Dust, 0.72f);
			art.Label(Text("Tier", skill.Tier), 252, 408, Bone, 0.91f, 215);
			art.Label(Text("CurrentState"), 494, 388, Dust, 0.72f);
			art.Label(SkillState(skill, eye, tier), 494, 408, Bone, 0.91f, 247);
		}

		private static string SkillState(Skill skill, GhosteyePlayer eye, int tier)
		{
			if (tier < skill.Tier) return Text("Locked");
			if (skill.SoloOnly && Main.netMode != NetmodeID.SinglePlayer) return Text("Solo");
			if (skill.Id == "Domain") return DomainStatus(eye, tier >= GhosteyePlayer.AwakenedTier);
			if (skill.InDomain && !eye.DomainOn) return Text("NeedsDomain");
			if (skill.Id == "Rewind" && ModContent.GetInstance<DomainRewind>().IsRewinding) return Text("Rewinding");
			if (skill.Id == "Overlap" && eye.OverlapTime > 0) return $"×{eye.AttackHitCount(tier)} · {Seconds(eye.OverlapTime)}s";

			int cooldown = skill.Id switch
			{
				"Beam" => eye.BeamCooldown,
				"Still" => eye.StillCooldown,
				"Restart" => eye.RestartCooldown,
				"Rewind" => eye.RewindCooldown,
				"Overlap" => eye.OverlapCooldown,
				_ => 0
			};
			return cooldown > 0 && tier < GhosteyePlayer.AwakenedTier
				? Text("Cooling", Seconds(cooldown))
				: Text("Ready");
		}
	}
}
