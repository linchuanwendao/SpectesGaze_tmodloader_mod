using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace SpectesGaze.Content.Systems
{
	public partial class GhosteyeUI
	{
		private static readonly Color Bone = new(251, 226, 209);
		private static readonly Color Dust = new(191, 150, 155);
		private static readonly Color PulseRed = new(244, 104, 122);
		private static readonly Color Rim = new(100, 36, 54);
		private static readonly Color Ink = new(19, 8, 15);
		private static Texture2D hudFrame;
		private static Texture2D guideFrame;

		private static Texture2D FrameTexture(bool compact) => compact
			? hudFrame ??= CreateFrame(HudWidth / 2, HudHeight / 2, 407)
			: guideFrame ??= CreateFrame(GuideWidth / 2, GuideHeight / 2, 803);

		private static void DisposeFrames()
		{
			Texture2D hud = hudFrame;
			Texture2D guide = guideFrame;
			hudFrame = guideFrame = null;
			if (hud == null && guide == null) return;

			Main.QueueMainThreadAction(() =>
			{
				hud?.Dispose();
				guide?.Dispose();
			});
		}

		private static Texture2D CreateFrame(int width, int height, uint seed)
		{
			Color[] pixels = new Color[width * height];
			Color driedBlood = new(51, 0, 0);
			Color flesh = new(128, 0, 0);
			Color bruised = new(44, 13, 31);
			for (int y = 0; y < height; y++)
			{
				float left = 1.6f + 2.1f * FleshNoise.Perlin(9f, y * 0.16f, seed);
				float right = 1.8f + 2.8f * FleshNoise.Perlin(13f, y * 0.11f, seed + 31);
				for (int x = 0; x < width; x++)
				{
					float top = 1.9f + FleshNoise.Perlin(x * 0.17f, 2f, seed + 9) * 2.9f;
					float bottom = 2.4f + FleshNoise.Perlin(x * 0.12f, 5f, seed + 67) * 3.2f;
					float edge = Math.Min(Math.Min(x - left, width - 1 - x - right),
						Math.Min(y - top, height - 1 - y - bottom));
					if (edge < -0.35f) continue;

					float grain = FleshNoise.Perlin(x * 0.31f, y * 0.26f, seed + 131);
					float marbling = FleshNoise.Perlin(x * 0.074f + grain * 0.35f, y * 0.061f, seed + 719);
					float wetEdge = MathHelper.Clamp(1f - Math.Abs(edge - 1f) * 0.39f, 0f, 1f);
					float vein = MathHelper.Clamp((0.14f - Math.Abs(marbling + grain * 0.27f)) * 4.5f, 0f, 0.56f);
					Color body = Color.Lerp(Ink, bruised, 0.19f + 0.23f * Math.Abs(grain));
					body = Color.Lerp(body, driedBlood, vein);
					body = Color.Lerp(body, flesh, wetEdge * (0.63f + 0.31f * grain));
					pixels[y * width + x] = body * MathHelper.Clamp(edge + 0.45f, 0f, 0.98f);
				}
			}

			Texture2D texture = new(Main.instance.GraphicsDevice, width, height);
			texture.SetData(pixels);
			return texture;
		}

		private readonly struct Canvas(SpriteBatch batch, Layout layout)
		{
			private static readonly Rectangle PixelSource = new(0, 0, 1, 1);
			private Vector2 At(Vector2 point) => layout.Origin + point * layout.Scale;

			public void Fill(Rectangle rectangle, Color color)
			{
				if (rectangle.Width <= 0 || rectangle.Height <= 0) return;

				Vector2 position = At(rectangle.Location.ToVector2());
				Vector2 size = rectangle.Size() * layout.Scale;
				batch.Draw(TextureAssets.MagicPixel.Value, position, PixelSource, color,
					0f, Vector2.Zero, size, SpriteEffects.None, 0f);
			}

			public void Line(Vector2 start, Vector2 end, float width, Color color)
			{
				Vector2 delta = end - start;
				batch.Draw(TextureAssets.MagicPixel.Value, At(start), PixelSource, color,
					delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(delta.Length(), width) * layout.Scale, SpriteEffects.None, 0f);
			}

			public void Sprite(Texture2D texture, Vector2 center, Vector2 size, Color color, float rotation = 0f)
			{
				batch.Draw(texture, At(center), null, color, rotation, texture.Size() / 2f,
					size / texture.Size() * layout.Scale, SpriteEffects.None, 0f);
			}

			public void Eye(int tier, Vector2 center, Vector2 size, Color tint, int seed)
			{
				ProceduralGhosteye.Draw(batch, At(center), size * layout.Scale, tier,
					Main.GlobalTimeWrappedHourly, tint, seed: seed);
			}

			public void Seam(Vector2 start, Vector2 end, uint seed)
			{
				int segments = Math.Max(2, (int)((end.X - start.X) / 14f));
				Vector2 previous = start;
				for (int i = 1; i <= segments; i++)
				{
					Vector2 point = Vector2.Lerp(start, end, i / (float)segments);
					point.Y += 2.1f * FleshNoise.Perlin(i * 0.71f, 0.34f, seed);
					float stain = FleshNoise.Unit(seed + (uint)i * 61);
					Line(previous, point, 0.8f + stain * 0.9f, Rim * (0.44f + stain * 0.47f));
					previous = point;
				}
			}

			public void Label(string text, float x, float y, Color color, float size, float maxWidth = 0, float anchor = 0)
			{
				if (string.IsNullOrEmpty(text)) return;

				if (maxWidth > 0)
				{
					float width = FontAssets.MouseText.Value.MeasureString(text).X;
					if (width > 0) size = Math.Min(size, maxWidth / width);
				}

				Utils.DrawBorderString(batch, text, At(new Vector2(x, y)), color, size * layout.Scale, anchor);
			}

			public void Button(Rectangle box, string label, bool hovered)
			{
				Color tint = hovered ? Color.White : new Color(223, 167, 178);
				Sprite(FrameTexture(true), box.Center.ToVector2(), box.Size(), tint);
				Seam(new Vector2(box.Left + 4, box.Top + 2), new Vector2(box.Right - 4, box.Top + 3), 304);
				Label(label, box.Center.X, box.Y + 5, Bone, 0.79f, box.Width - 8, 0.5f);
			}

			public void Frame(Rectangle box, bool compact = false)
			{
				Texture2D texture = FrameTexture(compact);
				Vector2 center = box.Center.ToVector2();
				Sprite(texture, center + new Vector2(0f, 4f), box.Size(), Color.Black * 0.59f);
				Sprite(texture, center, box.Size(), Color.White);

				int corners = compact ? 2 : 4;
				for (int i = 0; i < corners; i++)
				{
					float inward = i % 2 == 0 ? 1f : -1f;
					float down = i < 2 ? 1f : -1f;
					Vector2 corner = new(i % 2 == 0 ? box.Left + 1 : box.Right - 1, i < 2 ? box.Top + 1 : box.Bottom - 1);
					Vector2 split = corner + new Vector2(inward * (13 + i * 3), down * (4 + i % 3));
					Vector2 fork = split + new Vector2(inward * (15 + i * 2), down * 4);
					Line(corner, split, compact ? 2.4f : 3.7f, Rim);
					Line(split, fork, 2f, PulseRed * 0.62f);
					Line(fork, fork + new Vector2(inward * 15f, down * 7f), 0.9f, PulseRed * 0.45f);
					Line(split, split + new Vector2(inward * 7f, down * (compact ? 11f : 18f)), 1.4f, Rim);
				}
			}
		}
	}
}
