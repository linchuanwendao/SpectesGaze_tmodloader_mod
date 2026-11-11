using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace SpectesGaze.Content.Systems
{
	public partial class GhosteyeUI
	{
		private static readonly Color Bone = new(251, 226, 209);
		private static readonly Color Dust = new(191, 150, 155);
		private static readonly Color PulseRed = new(244, 104, 122);
		private static readonly Color Rim = new(100, 36, 54);
		private static readonly Color Ink = new(19, 8, 15);

		private static Texture2D Eye(int tier) =>
			ModContent.Request<Texture2D>($"SpectesGaze/Content/Textures/Eyes/Eye{tier}").Value;

		private static Texture2D Core =>
			ModContent.Request<Texture2D>("SpectesGaze/Content/Textures/Effects/DomainCore").Value;

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
				Fill(box, hovered ? new Color(101, 30, 48) : new Color(52, 17, 30));
				Fill(new Rectangle(box.X, box.Y, box.Width, 1), hovered ? PulseRed : Rim);
				Label(label, box.Center.X, box.Y + 5, Bone, 0.79f, box.Width - 8, 0.5f);
			}

			public void Frame(Rectangle box, bool compact = false)
			{
				Rectangle shadow = box;
				shadow.Offset(0, 4);
				Fill(shadow, Color.Black * 0.59f);
				Fill(box, Ink * 0.97f);
				Fill(new Rectangle(box.X, box.Y, box.Width, 2), Rim);
				Fill(new Rectangle(box.X, box.Bottom - 2, box.Width, 2), Rim);
				Fill(new Rectangle(box.X, box.Y, 1, box.Height), Rim);
				Fill(new Rectangle(box.Right - 1, box.Y, 1, box.Height), Rim);

				int corners = compact ? 2 : 4;
				for (int i = 0; i < corners; i++)
				{
					float inward = i % 2 == 0 ? 1f : -1f;
					float down = i < 2 ? 1f : -1f;
					Vector2 corner = new(i % 2 == 0 ? box.Left + 1 : box.Right - 1, i < 2 ? box.Top + 1 : box.Bottom - 1);
					Vector2 split = corner + new Vector2(inward * 12f, down * 6f);
					Line(corner, split, compact ? 2.4f : 3.7f, PulseRed * 0.74f);
					Line(split, split + new Vector2(inward * 26f, down * 6f), 1.3f, PulseRed * 0.58f);
					Line(split, split + new Vector2(inward * 11f, down * (compact ? 12f : 22f)), 1.4f, Rim);
				}
			}
		}
	}
}
