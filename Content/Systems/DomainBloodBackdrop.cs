using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static SpectesGaze.Content.Systems.FleshNoise;

namespace SpectesGaze.Content.Systems
{
	internal static class DomainBloodBackdrop
	{
		private const int Size = 224;
		private static readonly Color Dregs = new(55, 3, 18);
		private static readonly Color PooledBlood = new(178, 12, 30);
		private static readonly Color FreshBlood = new(228, 31, 40);
		private static readonly Vector2 Origin = new(Size * 0.5f);
		private static Texture2D sediment;
		private static Texture2D current;

		internal static void Draw(SpriteBatch batch, Vector2 center, float radius, float time, float opacity)
		{
			sediment ??= CreateLayer(false);
			current ??= CreateLayer(true);

			// The two world-space layers shear past one another beneath the rim.
			float clock = time * (MathHelper.TwoPi / 3600f);
			float surge = 0.5f + MathF.Sin(clock * 144f) * 0.5f;
			Vector2 drift = new Vector2(MathF.Sin(clock * 163f + 0.6f), MathF.Cos(clock * 211f)) * (radius * 0.048f);
			Vector2 position = center - Main.screenPosition;
			float scale = 2f * radius / Size;
			batch.Draw(sediment, position, null, Color.White * opacity,
				clock * -8f, Origin, scale, SpriteEffects.None, 0f);
			batch.Draw(current, position + drift, null, Color.White * (opacity * (0.75f + surge * 0.25f)),
				clock * 47f, Origin, scale, SpriteEffects.None, 0f);
		}

		private static Texture2D CreateLayer(bool flowing)
		{
			Color[] pixels = new Color[Size * Size];
			for (int y = 0; y < Size; y++)
			{
				float v = (y + 0.5f) * (2f / Size) - 1f;
				for (int x = 0; x < Size; x++)
				{
					float u = (x + 0.5f) * (2f / Size) - 1f;
					pixels[y * Size + x] = Sample(u, v, flowing);
				}
			}

			Texture2D texture = new(Main.instance.GraphicsDevice, Size, Size);
			texture.SetData(pixels);
			return texture;
		}

		private static Color Sample(float u, float v, bool flowing)
		{
			float distance = MathF.Sqrt(u * u + v * v);
			if (distance >= 0.99f) return Color.Transparent;

			uint seed = flowing ? 6079u : 2719u;
			float warpX = Perlin(u * 3.1f + 1.37f, v * 2.8f, seed);
			float warpY = Perlin(u * 2.5f, v * 3.4f - 1.41f, seed + 12u);
			float edge = 1f - SmoothBand(0.68f, 0.94f, distance + warpX * 0.11f + warpY * 0.08f);
			float x = u + warpX * 0.44f;
			float y = v + warpY * 0.41f;
			float pools = SmoothBand(-0.39f, 0.41f, Perlin(x * 3.6f, y * 4.4f, seed + 83u));
			float grain = Perlin(x * 36f, y * 58f, seed + 112u);
			float comb = Perlin(x * 5.3f + y * 2f, y * 21f - x * 7.2f, seed + 68u);
			float streaks = SmoothBand(0.04f, 0.47f, comb + grain * 0.21f);
			float pigment = MathHelper.Clamp(pools * 0.83f + streaks * 0.32f + grain * 0.2f, 0f, 1f);

			if (flowing)
			{
				Color wet = Color.Lerp(PooledBlood, FreshBlood, 0.24f + pigment * 0.76f);
				return wet * (edge * (0.027f + pigment * 0.195f));
			}

			Color baseColor = Color.Lerp(Dregs, PooledBlood, 0.36f + pigment * 0.64f);
			return baseColor * (edge * (0.155f + pigment * 0.145f));
		}

		private static float SmoothBand(float start, float end, float value) =>
			MathHelper.SmoothStep(0f, 1f, (value - start) / (end - start));

		internal static void Unload()
		{
			sediment?.Dispose();
			current?.Dispose();
			sediment = null;
			current = null;
		}
	}
}
