using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Systems
{
	internal sealed class ProceduralGhosteye : ModSystem
	{
		private const int AnatomySize = 104;
		private const int IrisSize = 64;
		private const int Frames = 8;
		private const int Variants = 3;
		private const int TwitchPeriod = 137;
		private static readonly Color DriedBlood = new(51, 0, 0);
		private static readonly Color ClottedBlood = new(85, 0, 0);
		private static readonly Color FreshBlood = new(128, 0, 0);
		private static readonly Color Bruise = new(25, 7, 24);
		private static Texture2D anatomy;
		private static Texture2D iris;

		public override void Load()
		{
			if (!Main.dedServ)
				Main.QueueMainThreadAction(EnsureTextures);
		}

		public override void Unload()
		{
			Texture2D oldAnatomy = anatomy;
			Texture2D oldIris = iris;
			anatomy = iris = null;
			Main.QueueMainThreadAction(() =>
			{
				oldAnatomy?.Dispose();
				oldIris?.Dispose();
			});
		}

		internal static void Draw(SpriteBatch batch, Vector2 center, Vector2 size, int tier, float time, Color tint,
			float rotation = 0f, int seed = 0)
		{
			if (Main.dedServ || tint.A == 0 || size.X <= 0f || size.Y <= 0f) return;
			EnsureTextures();

			int variant = tier >= 8 ? 2 : tier >= 4 ? 1 : 0;
			uint key = unchecked((uint)seed * 2654435761u);
			int tick = (int)MathF.Floor(time * 60f) + (int)(FleshNoise.Unit(key + 193u) * TwitchPeriod);
			int frame = (tick / 5) % Frames;
			if (frame < 0) frame += Frames;

			EyePose pose = GetPose(tick, key);
			Vector2 eyeSize = new(size.X, size.Y * (1f - 0.48f * pose.Blink));
			Vector2 pupilCenter = center + (pose.Gaze * eyeSize).RotatedBy(rotation);
			Vector2 pupilSize = eyeSize * new Vector2(0.54f, 0.47f);
			Rectangle anatomyFrame = new(frame * AnatomySize, variant * AnatomySize, AnatomySize, AnatomySize);
			Rectangle irisFrame = new(frame * IrisSize, variant * IrisSize, IrisSize, IrisSize);

			if (pose.Twitch > 0f && size.X >= 24f)
			{
				// 只有抽动的两三帧产生通道错位，且只落在眼睛贴图附近。
				Vector2 shift = new Vector2(0.049f * size.X, -0.018f * size.Y).RotatedBy(rotation);
				DrawFrame(batch, anatomy, anatomyFrame, center - shift, eyeSize, rotation,
					new Color(197, 18, 40) * (0.27f * tint.A / 255f * pose.Twitch));
				DrawFrame(batch, anatomy, anatomyFrame, center + shift, eyeSize, rotation,
					new Color(74, 27, 103) * (0.19f * tint.A / 255f * pose.Twitch));
			}

			DrawFrame(batch, anatomy, anatomyFrame, center, eyeSize, rotation, tint);
			DrawFrame(batch, iris, irisFrame, pupilCenter, pupilSize, rotation, tint * (1f - 0.7f * pose.Blink));
		}

		private static void DrawFrame(SpriteBatch batch, Texture2D texture, Rectangle source, Vector2 center, Vector2 size,
			float rotation, Color color)
		{
			Vector2 dimensions = new(source.Width, source.Height);
			batch.Draw(texture, center, source, color, rotation, dimensions / 2f, size / dimensions, SpriteEffects.None, 0f);
		}

		private readonly record struct EyePose(Vector2 Gaze, float Twitch, float Blink);

		private static EyePose GetPose(int tick, uint key)
		{
			int episode = (int)MathF.Floor(tick / (float)TwitchPeriod);
			int frame = tick - episode * TwitchPeriod;
			uint sequence = unchecked((uint)episode * 2246822519u + key);
			int first = 13 + (int)(FleshNoise.Unit(sequence + 17u) * 36f);
			int recoil = first + 6 + (int)(FleshNoise.Unit(sequence + 23u) * 8f);
			int settle = recoil + 11 + (int)(FleshNoise.Unit(sequence + 31u) * 29f);

			Vector2 previous = LookAt(unchecked(sequence - 2246822519u), 2u);
			Vector2 position = previous;
			float twitch = 0f;
			// 每轮先定格，三次抽动分别持续 1、2、3 帧，落稳后不插值漂移。
			for (int i = 0; i < 3; i++)
			{
				int onset = i == 0 ? first : i == 1 ? recoil : settle;
				if (frame < onset) break;

				int length = i + 1;
				int age = frame - onset;
				Vector2 destination = LookAt(sequence, (uint)i);
				float progress = Math.Min(1f, (age + 1f) / length);
				position = Vector2.Lerp(previous, destination, 1f - (1f - progress) * (1f - progress));
				twitch = MathHelper.Clamp(1f - age / (length + 1f), 0f, 1f);
				previous = destination;
			}

			int blinkAge = frame - recoil;
			float blink = blinkAge is >= 0 and < 5 ? 1f - MathF.Abs(blinkAge - 1f) / 4f : 0f;
			return new EyePose(position, twitch, blink);
		}

		private static Vector2 LookAt(uint sequence, uint stage)
		{
			uint key = sequence + stage * 83492791u;
			float spread = stage == 2 ? 0.071f : 0.126f;
			return new Vector2((2f * FleshNoise.Unit(key + 129u) - 1f) * spread,
				(2f * FleshNoise.Unit(key + 449u) - 1f) * 0.042f);
		}

		private static void EnsureTextures()
		{
			if (anatomy != null && iris != null) return;

			anatomy ??= CreateAtlas(AnatomySize, DrawAnatomy);
			iris ??= CreateAtlas(IrisSize, DrawIris);
		}

		private delegate void DrawFramePixels(Color[] sheet, int stride, int left, int top, int variant, float angle);

		private static Texture2D CreateAtlas(int size, DrawFramePixels draw)
		{
			int width = size * Frames;
			int height = size * Variants;
			Color[] pixels = new Color[width * height];
			for (int variant = 0; variant < Variants; variant++)
			{
				for (int frame = 0; frame < Frames; frame++)
					draw(pixels, width, frame * size, variant * size, variant, frame * MathHelper.TwoPi / Frames);
			}

			Texture2D result = new(Main.instance.GraphicsDevice, width, height);
			result.SetData(pixels);
			return result;
		}

		private static void DrawAnatomy(Color[] sheet, int stride, int left, int top, int variant, float angle)
		{
			uint seed = (uint)(67 + 5381 * variant);
			Vector2 phase = angle.ToRotationVector2() * 0.42f;
			float halfWidth = variant == 1 ? 0.83f : 0.9f;
			Color muscle = variant switch
			{
				1 => new Color(164, 28, 66),
				2 => new Color(151, 29, 43),
				_ => new Color(143, 38, 38)
			};
			Color wetRidge = variant == 2 ? new Color(229, 105, 97) : new Color(210, 49, 60);

			for (int y = 0; y < AnatomySize; y++)
			{
				for (int x = 0; x < AnatomySize; x++)
				{
					Vector2 p = NormalizePixel(x, y, AnatomySize);
					if (MathF.Abs(p.X) > 0.96f || MathF.Abs(p.Y) > 0.78f) continue;

					float coarse = FleshNoise.Perlin(p.X * 4.7f + phase.X, p.Y * 5.1f + phase.Y, seed);
					float grain = FleshNoise.Perlin(p.X * 22f + phase.Y, p.Y * 27f + phase.X, seed + 123u);
					float arc = 0.034f * p.X + 0.052f * MathF.Sin(p.X * 5.7f + angle * 0.8f) + coarse * 0.056f;
					float flank = MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs((p.X + 0.03f) / halfWidth), 1.6f)), 0.81f);
					float lidHeight = p.Y < arc ? 0.53f : 0.43f;
					if (variant == 1)
					{
						// 中层的上眼睑向左隆起，下缘切开一道缺口。
						lidHeight += p.Y < arc ? 0.19f * Clamp(1f - MathF.Abs(p.X + 0.32f) * 2.2f)
							: 0.065f - 0.12f * Clamp(1f - MathF.Abs(p.X - 0.42f) * 9f);
					}
					else if (variant == 2)
					{
						lidHeight += p.Y < arc ? 0.03f - 0.13f * Clamp(1f - MathF.Abs(p.X + 0.26f) * 10f)
							: 0.11f * Clamp(1f - MathF.Abs(p.X - 0.19f) * 2f);
					}

					float outer = flank * lidHeight - MathF.Abs(p.Y - arc) + coarse * 0.13f + grain * 0.038f;
					outer = Math.Min(outer, 0.91f - MathF.Abs(p.X + 0.025f) + coarse * 0.035f);
					if (variant == 2)
					{
						// 高层右下缘留一片翻开的眼睑，和主体共用血肉底色。
						float foldX = (p.X - 0.45f) / 0.29f;
						float foldY = (p.Y - 0.41f - 0.28f * (p.X - 0.32f)) / 0.18f;
						float fold = (1f - MathF.Abs(foldX) - MathF.Abs(foldY) + coarse * 0.44f) * 0.20f;
						outer = Math.Max(outer, fold);
					}
					float coverage = Clamp(0.52f + outer * 45f);
					if (coverage == 0f) continue;

					float opening = 0.337f * MathF.Pow(Math.Max(0f, 1f - MathF.Pow(MathF.Abs(p.X / 0.76f), 1.74f)), 0.77f)
						- MathF.Abs(p.Y - arc * 0.57f) + coarse * 0.06f;
					float cut = Clamp(0.54f + opening * 66f);
					float fibers = 0.5f + 0.5f * MathF.Sin(p.Y * 51f + p.X * p.X * 19f + coarse * 6f);
					float recess = Clamp(outer * 11f);
					float meatLight = Clamp(0.36f + coarse * 0.71f + grain * 0.28f + fibers * 0.22f);
					Color lid = Color.Lerp(Bruise, ClottedBlood, recess);
					lid = Color.Lerp(lid, muscle, meatLight * recess);
					float rim = Clamp(1f - MathF.Abs(opening + 0.041f) * 23f);
					lid = Color.Lerp(lid, wetRidge, rim * Clamp(0.68f + coarse + grain * 0.7f));

					Color socket = Color.Lerp(new Color(19, 3, 13), new Color(124, 30, 39),
						Clamp(opening * 3.4f + coarse * 0.51f + grain * 0.32f));
					float glint = Clamp((grain - 0.16f) * 3f) * Clamp((coarse + 0.1f) * 2.5f) * rim;
					Color meat = Color.Lerp(Color.Lerp(lid, socket, cut), new Color(255, 145, 126), glint * 0.68f);
					sheet[(top + y) * stride + left + x] = meat * coverage;
				}
			}

			DrawVeins(sheet, stride, left, top, variant, seed, phase);
		}

		private static void DrawVeins(Color[] sheet, int stride, int left, int top, int variant, uint seed, Vector2 phase)
		{
			for (int vein = 0; vein < 18; vein++)
			{
				uint key = seed + (uint)vein * 541u;
				float a = FleshNoise.Unit(key + 1u) * MathHelper.TwoPi;
				Vector2 dir = a.ToRotationVector2();
				Vector2 normal = new(-dir.Y, dir.X);
				Vector2 start = dir * new Vector2(0.65f, 0.39f);
				Vector2 previous = start;
				int segments = 5 + (int)(FleshNoise.Unit(key + 17u) * 4f);
				float width = 0.011f + FleshNoise.Unit(key + 55u) * 0.015f;
				float reach = 0.57f + FleshNoise.Unit(key + 71u) * 0.26f;
				for (int segment = 1; segment <= segments; segment++)
				{
					float u = segment / (float)segments;
					float curl = FleshNoise.Perlin(u * 3.7f + phase.X, phase.Y + 0.38f, key);
					Vector2 point = start * (1f - reach * u) + normal * (curl * (0.12f + variant * 0.016f) * MathF.Sin(u * MathHelper.Pi));
					float taper = width * (1.08f - u * 0.79f);
					RasterVein(sheet, stride, left, top, previous, point, taper * 1.45f, DriedBlood, 0.84f);
					RasterVein(sheet, stride, left, top, previous, point, taper * 0.61f, new Color(180, 28, 46), 0.72f);

					if (segment == 2 + vein % 3)
					{
						float side = vein % 2 == 0 ? -1f : 1f;
						Vector2 fork = point + normal * (side * 0.12f) - dir * 0.065f;
						Vector2 tip = fork + normal * (side * 0.066f) - dir * 0.09f;
						RasterVein(sheet, stride, left, top, point, fork, taper * 0.6f, FreshBlood, 0.68f);
						RasterVein(sheet, stride, left, top, fork, tip, taper * 0.34f, new Color(159, 25, 49), 0.56f);
					}
					previous = point;
				}
			}
		}

		private static void RasterVein(Color[] sheet, int stride, int left, int top, Vector2 from, Vector2 to,
			float width, Color color, float strength)
		{
			float scale = AnatomySize / 2f;
			Vector2 a = (from + Vector2.One) * scale;
			Vector2 b = (to + Vector2.One) * scale;
			Vector2 delta = b - a;
			float lengthSquared = delta.LengthSquared();
			if (lengthSquared <= 0.01f) return;

			float thickness = width * scale + 0.65f;
			int minX = Math.Max(0, (int)MathF.Floor(Math.Min(a.X, b.X) - thickness));
			int minY = Math.Max(0, (int)MathF.Floor(Math.Min(a.Y, b.Y) - thickness));
			int maxX = Math.Min(AnatomySize - 1, (int)MathF.Ceiling(Math.Max(a.X, b.X) + thickness));
			int maxY = Math.Min(AnatomySize - 1, (int)MathF.Ceiling(Math.Max(a.Y, b.Y) + thickness));
			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					int index = (top + y) * stride + left + x;
					Color previous = sheet[index];
					if (previous.A == 0) continue;

					Vector2 p = new(x + 0.5f, y + 0.5f);
					float t = Clamp(Vector2.Dot(p - a, delta) / lengthSquared);
					float distance = Vector2.Distance(p, a + delta * t);
					float opacity = Clamp(thickness - distance) * strength;
					sheet[index] = Color.Lerp(previous, color * (previous.A / 255f), opacity);
				}
			}
		}

		private static void DrawIris(Color[] sheet, int stride, int left, int top, int variant, float angle)
		{
			uint seed = 7301u + (uint)variant * 1291u;
			Vector2 phase = angle.ToRotationVector2() * 0.36f;
			for (int y = 0; y < IrisSize; y++)
			{
				for (int x = 0; x < IrisSize; x++)
				{
					Vector2 p = NormalizePixel(x, y, IrisSize);
					float radial = (p * new Vector2(1.10f, 0.93f)).Length();
					if (radial > 1.04f) continue;

					float coarse = FleshNoise.Perlin(p.X * 5.1f + phase.X, p.Y * 6.8f + phase.Y, seed);
					float fine = FleshNoise.Perlin(p.X * 18f + phase.Y, p.Y * 23f + phase.X, seed + 90u);
					float polar = MathF.Atan2(p.Y, p.X);
					float tears = MathF.Abs(MathF.Sin(polar * (11 + variant * 3) + coarse * 4f));
					float boundary = 0.83f + coarse * 0.21f + fine * 0.08f;
					float coverage = Math.Min(Clamp(0.55f + (boundary - radial) * 26f),
						Clamp((0.976f - Math.Max(MathF.Abs(p.X), MathF.Abs(p.Y))) * 52f));
					if (coverage == 0f) continue;

					float shine = Clamp((1f - radial) * 1.32f + coarse * 0.71f + fine * 0.34f + tears * 0.25f);
					Color tissue = Color.Lerp(Bruise, FreshBlood, shine);
					tissue = Color.Lerp(tissue, new Color(243, 61, 53), shine * shine);
					float brow = -0.35f + p.X * 0.29f + coarse * 0.093f;
					float gloss = Clamp(1f - MathF.Abs(p.Y - brow) * 9f)
						* Clamp(1.1f - MathF.Abs(p.X + 0.28f) * 2.45f) * Clamp(0.55f + fine * 1.9f);
					tissue = Color.Lerp(tissue, new Color(255, 192 + variant * 16, 159 + variant * 22), gloss * 0.98f);

					// 瞳孔是一道缺角的裂缝，宽度随高度和血丝改变。
					float pupilCenter = 0.086f * MathF.Sin(p.Y * 7.4f + variant) + coarse * 0.092f;
					float taper = Math.Max(0f, 1f - MathF.Abs(p.Y + 0.08f) * 1.29f);
					float pupilWidth = taper * (0.21f + coarse * 0.21f + fine * 0.10f);
					float cut = Math.Min(pupilWidth - MathF.Abs(p.X - pupilCenter), 0.71f - MathF.Abs(p.Y + 0.08f));
					float fissure = Clamp(0.55f + cut * 36f);
					tissue = Color.Lerp(tissue, new Color(12, 3, 15), fissure);
					sheet[(top + y) * stride + left + x] = tissue * coverage;
				}
			}
		}

		private static Vector2 NormalizePixel(int x, int y, int size) =>
			new Vector2((x + 0.5f) * 2f / size - 1f, (y + 0.5f) * 2f / size - 1f);

		private static float Clamp(float value) => MathHelper.Clamp(value, 0f, 1f);
	}
}
