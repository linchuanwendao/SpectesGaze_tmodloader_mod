using System;
using Microsoft.Xna.Framework;

namespace SpectesGaze.Content.Systems
{
	internal static class FleshNoise
	{
		private static uint Hash(uint value)
		{
			value ^= value >> 16;
			value = unchecked(value * 0x7feb352du);
			value ^= value >> 15;
			value = unchecked(value * 0x846ca68bu);
			return value ^ (value >> 16);
		}

		internal static float Unit(uint seed) => (Hash(seed) >> 8) / 16777216f;

		internal static float Perlin(float x, float y, uint seed = 0)
		{
			int left = (int)MathF.Floor(x);
			int top = (int)MathF.Floor(y);
			float dx = x - left;
			float dy = y - top;
			float row = Fade(dx);
			float column = Fade(dy);
			float first = MathHelper.Lerp(Gradient(left, top, dx, dy, seed),
				Gradient(left + 1, top, dx - 1f, dy, seed), row);
			float second = MathHelper.Lerp(Gradient(left, top + 1, dx, dy - 1f, seed),
				Gradient(left + 1, top + 1, dx - 1f, dy - 1f, seed), row);
			return MathHelper.Lerp(first, second, column);
		}

		private static float Fade(float t) => t * t * t * (t * (6f * t - 15f) + 10f);

		private static float Gradient(int x, int y, float dx, float dy, uint seed)
		{
			uint hash = Hash(unchecked((uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ seed));
			return (hash & 7u) switch
			{
				0 => dx,
				1 => -dx,
				2 => dy,
				3 => -dy,
				4 => (dx + dy) * 0.70710678f,
				5 => (dx - dy) * 0.70710678f,
				6 => (dy - dx) * 0.70710678f,
				_ => (-dx - dy) * 0.70710678f
			};
		}
	}
}
