using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static SpectesGaze.Content.Systems.FleshNoise;

namespace SpectesGaze.Content.Systems
{
	// Collect the blood-and-tissue strips and submit them as one primitive drawing pass.
	internal static class FleshPrimitives
	{
		private const int VertexLimit = 49152;
		private const int BrushSize = 128;
		private static readonly List<VertexPositionColorTexture> Vertices = new(4096);
		private static VertexPositionColorTexture[] buffer = Array.Empty<VertexPositionColorTexture>();
		private static BasicEffect effect;
		private static Texture2D brush;
		private static Vector2 screenOrigin;

		internal readonly record struct Section(Vector2 Center, Vector2 Normal, float Left, float Right,
			Color Color, Color? Ridge = null, float Stroke = 0f);

		internal static void Begin(Vector2 screenPosition)
		{
			screenOrigin = screenPosition;
			Vertices.Clear();
		}

		internal static void Connect(Section start, Section end)
		{
			bool raised = start.Ridge.HasValue || end.Ridge.HasValue;
			if (Vertices.Count + (raised ? 24 : 18) > VertexLimit) return;

			Vector2 a = start.Center - screenOrigin;
			Vector2 b = end.Center - screenOrigin;
			Vector2 aLeft = a - start.Normal * start.Left;
			Vector2 aRight = a + start.Normal * start.Right;
			Vector2 bLeft = b - end.Normal * end.Left;
			Vector2 bRight = b + end.Normal * end.Right;
			float skirt = Math.Min(1.4f, (start.Left + start.Right + end.Left + end.Right) * 0.13f);

			VertexPositionColorTexture a1 = Vertex(aLeft, start.Color, start.Stroke, raised ? 0.12f : 0f);
			VertexPositionColorTexture a2 = Vertex(aRight, start.Color, start.Stroke, raised ? 0.92f : 0f);
			VertexPositionColorTexture b1 = Vertex(bLeft, end.Color, end.Stroke, raised ? 0.12f : 0f);
			VertexPositionColorTexture b2 = Vertex(bRight, end.Color, end.Stroke, raised ? 0.92f : 0f);
			if (raised)
			{
				VertexPositionColorTexture aRidge = Vertex(a, start.Ridge ?? start.Color, start.Stroke, 0.52f);
				VertexPositionColorTexture bRidge = Vertex(b, end.Ridge ?? end.Color, end.Stroke, 0.52f);
				AddQuad(a1, aRidge, b1, bRidge);
				AddQuad(aRidge, a2, bRidge, b2);
			}
			else
			{
				AddQuad(a1, a2, b1, b2);
			}
			AddQuad(Vertex(aLeft - start.Normal * skirt, Color.Transparent), a1,
				Vertex(bLeft - end.Normal * skirt, Color.Transparent), b1);
			AddQuad(a2, Vertex(aRight + start.Normal * skirt, Color.Transparent),
				b2, Vertex(bRight + end.Normal * skirt, Color.Transparent));
		}

		private static VertexPositionColorTexture Vertex(Vector2 p, Color color, float u = 0f, float v = 0f) =>
			new(new Vector3(p, 0f), color, new Vector2(u, v));

		private static void AddQuad(VertexPositionColorTexture a, VertexPositionColorTexture b,
			VertexPositionColorTexture c, VertexPositionColorTexture d)
		{
			Vertices.Add(a);
			Vertices.Add(b);
			Vertices.Add(c);
			Vertices.Add(c);
			Vertices.Add(b);
			Vertices.Add(d);
		}

		internal static void Flush(GraphicsDevice device, Matrix view)
		{
			if (Vertices.Count == 0) return;

			if (buffer.Length < Vertices.Count)
				buffer = new VertexPositionColorTexture[Math.Min(VertexLimit, Math.Max(4096, Vertices.Count * 2))];
			Vertices.CopyTo(buffer);

			brush ??= CreateBrush(device);
			effect ??= new BasicEffect(device) { VertexColorEnabled = true, TextureEnabled = true, Texture = brush };
			effect.View = view;
			// Match SpriteBatch's viewport mapping rather than assuming a window or render-target size.
			effect.Projection = Matrix.CreateOrthographicOffCenter(0f, device.Viewport.Width, device.Viewport.Height,
				0f, 0f, 1f);

			BlendState oldBlend = device.BlendState;
			RasterizerState oldRasterizer = device.RasterizerState;
			DepthStencilState oldDepth = device.DepthStencilState;
			SamplerState oldSampler = device.SamplerStates[0];
			try
			{
				device.BlendState = BlendState.AlphaBlend;
				device.RasterizerState = RasterizerState.CullNone;
				device.DepthStencilState = DepthStencilState.None;
				effect.CurrentTechnique.Passes[0].Apply();
				device.SamplerStates[0] = SamplerState.LinearWrap;
				device.DrawUserPrimitives(PrimitiveType.TriangleList, buffer, 0, Vertices.Count / 3);
			}
			finally
			{
				device.BlendState = oldBlend;
				device.RasterizerState = oldRasterizer;
				device.DepthStencilState = oldDepth;
				device.SamplerStates[0] = oldSampler;
				Vertices.Clear();
			}
		}

		private static Texture2D CreateBrush(GraphicsDevice device)
		{
			Color[] pixels = new Color[BrushSize * BrushSize];
			for (int y = 0; y < BrushSize; y++)
			{
				float v = (y + 0.5f) / BrushSize;
				for (int x = 0; x < BrushSize; x++)
				{
					// Plain strips sample the white border. Raised paint samples the ridges in between.
					if (y < 3 || y >= BrushSize - 3)
					{
						pixels[y * BrushSize + x] = Color.White;
						continue;
					}

					float u = (x + 0.5f) / BrushSize;
					float drag = v + WrapStroke(u, v, 3f, 3.8f, 153u) * 0.17f;
					float pigment = WrapStroke(u, drag, 5f, 10f, 469u);
					float grooves = WrapStroke(u, drag, 6f, 38f, 951u);
					float flecks = WrapStroke(u, drag, 36f, 51f, 51u);
					float relief = MathHelper.Clamp(0.81f + pigment * 0.58f + grooves * 0.34f + flecks * 0.18f, 0.4f, 1f);
					byte tone = (byte)(relief * 255f);
					pixels[y * BrushSize + x] = new Color(tone, tone, tone, byte.MaxValue);
				}
			}

			Texture2D texture = new(device, BrushSize, BrushSize);
			texture.SetData(pixels);
			return texture;
		}

		private static float WrapStroke(float u, float v, float horizontal, float vertical, uint seed) =>
			MathHelper.Lerp(Perlin(u * horizontal, v * vertical, seed),
				Perlin((u - 1f) * horizontal, v * vertical, seed), MathHelper.SmoothStep(0f, 1f, u));

		internal static void Unload()
		{
			effect?.Dispose();
			effect = null;
			brush?.Dispose();
			brush = null;
			Vertices.Clear();
			buffer = Array.Empty<VertexPositionColorTexture>();
		}
	}
}
