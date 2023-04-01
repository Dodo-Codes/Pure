﻿namespace Pure.Window;

using SFML.Graphics;
using SFML.System;
using SFML.Window;

/// <summary>
/// Provides a simple way to create and interact with an OS window.
/// </summary>
public static class Window
{
	/// <summary>
	/// Whether the OS window exists. This is <see langword="true"/> even when it
	/// is minimized or <see cref="IsHidden"/>.
	/// </summary>
	public static bool IsExisting
	{
		get => window != null && window.IsOpen;
		set
		{
			if (value == false)
				Close();
		}
	}
	/// <summary>
	/// The title on the title bar of the OS window.
	/// </summary>
	public static string Title
	{
		get => title;
		set { title = value; window.SetTitle(title); }
	}
	/// <summary>
	/// The size of the OS window.
	/// </summary>
	public static (uint, uint) Size
	{
		get => (window.Size.X, window.Size.Y);
		set => window.Size = new(value.Item1, value.Item2);
	}
	/// <summary>
	/// Whether the OS window acts as a background process.
	/// </summary>
	public static bool IsHidden
	{
		get => isHidden;
		set { isHidden = value; window.SetVisible(value == false); }
	}
	/// <summary>
	/// Returns whether the OS window is currently focused.
	/// </summary>
	public static bool IsFocused => window.HasFocus();
	/// <summary>
	/// Whether the OS window has a retro TV/arcade screen effect over it.
	/// </summary>
	public static bool IsRetro
	{
		get => isRetro;
		set
		{
			if (value && retroScreen == null && Shader.IsAvailable)
				retroScreen = RetroShader.Create();

			isRetro = value;
		}
	}

	/// <summary>
	/// Determines whether the <see cref="Window"/> <paramref name="isActive"/>.
	/// An application loop should ideally activate it at the very start and
	/// deactivate it at the very end.
	/// </summary>
	public static void Activate(bool isActive)
	{
		if (isActive)
		{
			window.DispatchEvents();
			window.Clear();
			window.SetActive();
			return;
		}

		Mouse.Update();
		window.Display();
	}
	/// <summary>
	/// Terminates the OS window and closes the application.
	/// </summary>
	public static void Close()
	{
		if (IsRetro || isClosing)
		{
			isClosing = true;

			retroTurnoffTime = new();
			retroTurnoff = new(RETRO_TURNOFF_TIME * 1000);
			retroTurnoff.Start();
			retroTurnoff.Elapsed += (s, e) => window.Close();
			return;
		}

		window.Close();
	}

	/// <summary>
	/// Draws a tilemap onto the OS window. Its graphics image is loaded from a
	/// <paramref name="path"/> (default graphics if <see langword="null"/>) using a
	/// <paramref name="tileSize"/> and <paramref name="tileGaps"/>, then it is cached
	/// for future draws. The tilemap's contents are decided by <paramref name="tiles"/>,
	/// <paramref name="tints"/>, <paramref name="angles"/> and <paramref name="flips"/>
	/// (flip first, rotation second - order matters).
	/// </summary>
	public static void DrawTilemap(int[,] tiles, uint[,] tints, sbyte[,] angles, (bool, bool)[,] flips, (uint, uint) tileSize, (uint, uint) tileGaps = default, string? path = default)
	{
		if (tiles == null || tints == null || tiles.Length != tints.Length)
			return;

		path ??= "default";

		TryLoadGraphics(path);
		var verts = Vertices.GetTilemap(tiles, tints, angles, flips, tileSize, tileGaps, path);
		var tex = graphics[path];
		var shader = IsRetro ? retroScreen : null;
		var rend = new RenderStates(BlendMode.Alpha, Transform.Identity, tex, shader);
		var randVec = new Vector2f(retroRand.Next(0, 10) / 10f, retroRand.Next(0, 10) / 10f);

		if (IsRetro)
		{
			shader?.SetUniform("time", retroScreenTimer.ElapsedTime.AsSeconds());
			shader?.SetUniform("randomVec", randVec);
			shader?.SetUniform("viewSize", window.GetView().Size);

			if (isClosing && retroTurnoffTime != null)
			{
				var timing = retroTurnoffTime.ElapsedTime.AsSeconds() / RETRO_TURNOFF_TIME;
				shader?.SetUniform("turnoffAnimation", timing);
			}
		}
		window.Draw(verts, PrimitiveType.Quads, rend);
	}
	/// <summary>
	/// Draws a <paramref name="tilemap"/> onto the OS window. Its graphics image is loaded from a
	///  (default graphics if <see langword="null"/>) using a
	/// <paramref name="tileSize"/> and <paramref name="tileGaps"/>, then it is cached
	/// for future draws.
	/// </summary>
	public static void DrawTilemap((int[,], uint[,], sbyte[,], (bool, bool)[,]) tilemap, (uint, uint) tileSize, (uint, uint) tileGaps = default, string? path = default)
	{
		var (tiles, tints, angles, flips) = tilemap;
		DrawTilemap(tiles, tints, angles, flips, tileSize, tileGaps, path);
	}
	/// <summary>
	/// Draws a sprite onto the OS window. Its graphics are decided by a <paramref name="tile"/>
	/// from the last <see cref="DrawTilemap"/> call, a <paramref name="tint"/>, an
	/// <paramref name="angle"/> and a <paramref name="flip"/>
	/// (flip first, rotation second - order matters).
	/// The sprite's <paramref name="position"/> is also relative to the previously drawn tilemap.
	/// </summary>
	public static void DrawSprite((float, float) position, int tile, uint tint = uint.MaxValue, sbyte angle = 0, (bool, bool) flip = default)
	{
		if (Vertices.prevDrawTilesetGfxPath == null)
			return;

		var verts = Vertices.GetSprite(position, tile, tint, angle, flip);
		var tex = graphics[Vertices.prevDrawTilesetGfxPath];
		var rend = new RenderStates(BlendMode.Alpha, Transform.Identity, tex, IsRetro ? retroScreen : null);
		window.Draw(verts, PrimitiveType.Quads, rend);
	}
	public static void DrawSprite((float, float) position, int tile, (int, int) tileCount, uint tint = uint.MaxValue, sbyte angle = 0, (bool, bool) flip = default)
	{
		if (Vertices.prevDrawTilesetGfxPath == null)
			return;

		var (w, h) = tileCount;
		var tiles = new int[w, h];
		var texW = graphics[Vertices.prevDrawTilesetGfxPath].Size.X;
		var gapW = Vertices.prevDrawTilesetTileGap.Item1;
		var tilesW = (int)(texW / (Vertices.prevDrawTilesetTileSz.Item1 + gapW));

		for (int i = 0; i < w; i++)
			for (int j = 0; j < h; j++)
				tiles[i, j] = tile + (j * tilesW + i);

		var rotated = Vertices.Rotate(tiles, angle);
		var vertsArr = new VertexArray(PrimitiveType.Quads);

		for (int i = 0; i < w; i++)
			for (int j = 0; j < h; j++)
			{
				var (x, y) = (position.Item1 + i, position.Item2 + j);
				var verts = Vertices.GetSprite((x, y), tiles[i, j], tint, angle, flip);
				vertsArr.Append(verts[0]);
				vertsArr.Append(verts[1]);
				vertsArr.Append(verts[2]);
				vertsArr.Append(verts[3]);
				DrawSprite((x, y), tiles[i, j], tint, angle, flip);
			}

		var tex = graphics[Vertices.prevDrawTilesetGfxPath];
		var rend = new RenderStates(BlendMode.Alpha, Transform.Identity, tex, IsRetro ? retroScreen : null);
		//window.Draw(vertsArr, rend);
	}
	/// <summary>
	/// Draws single pixel points with <paramref name="tint"/> onto the OS window.
	/// Their <paramref name="positions"/> are relative to the previously drawn tilemap.
	/// </summary>
	public static void DrawPoints(uint tint, params (float, float)[] positions)
	{
		if (positions == null || positions.Length == 0)
			return;

		var verts = Vertices.GetPoints(tint, positions);
		window.Draw(verts, PrimitiveType.Quads, Rend);
	}
	/// <summary>
	/// Draws a rectangle with <paramref name="tint"/> onto the OS window.
	/// Its <paramref name="position"/> and <paramref name="size"/> are relative
	/// to the previously drawn tilemap.
	/// </summary>
	public static void DrawRectangle((float, float) position, (float, float) size, uint tint = uint.MaxValue)
	{
		var verts = Vertices.GetRectangle(position, size, tint);
		window.Draw(verts, PrimitiveType.Quads);
	}
	/// <summary>
	/// Draws a line between <paramref name="pointA"/> and <paramref name="pointB"/> with
	/// <paramref name="tint"/> onto the OS window.
	/// Its points are relative to the previously drawn tilemap.
	/// </summary>
	public static void DrawLine((float, float) pointA, (float, float) pointB, uint tint = uint.MaxValue)
	{
		var verts = Vertices.GetLine(pointA, pointB, tint);
		window.Draw(verts, PrimitiveType.Quads, Rend);
	}

	#region Backend
	private static bool isHidden, isRetro, isClosing;
	private static string title;

	private static Shader? retroScreen;
	private static RenderStates Rend => IsRetro ? new(retroScreen) : default;
	private static readonly SFML.System.Clock retroScreenTimer = new();
	private static Random retroRand = new();
	private static System.Timers.Timer? retroTurnoff;
	private static Clock? retroTurnoffTime;
	private const float RETRO_TURNOFF_TIME = 0.5f;

	internal static readonly Dictionary<string, Texture> graphics = new();
	internal static readonly RenderWindow window;

	static Window()
	{
		//var str = DefaultGraphics.PNGToBase64String(
		//	"/home/gojur/code/Pure/Examples/bin/Debug/net6.0/graphics.png");

		graphics["default"] = DefaultGraphics.CreateTexture();

		title = "Game";

		window = new(new VideoMode(1280, 720), title);
		window.Closed += (s, e) => Close();
		window.Resized += (s, e) => UpdateWindowAndView();
		window.LostFocus += (s, e) =>
		{
			Mouse.CancelInput();
			Keyboard.CancelInput();
		};

		window.DispatchEvents();
		window.Clear();
		window.Display();

		UpdateWindowAndView();
	}

	private static void TryLoadGraphics(string path)
	{
		if (graphics.ContainsKey(path))
			return;

		graphics[path] = new(path);
	}
	private static void UpdateWindowAndView()
	{
		var view = window.GetView();
		var (w, h) = (RoundToMultipleOfTwo((int)Size.Item1), RoundToMultipleOfTwo((int)Size.Item2));
		view.Size = new(w, h);
		view.Center = new(RoundToMultipleOfTwo((int)(Size.Item1 / 2f)), RoundToMultipleOfTwo((int)(Size.Item2 / 2f)));
		window.SetView(view);
		window.Size = new((uint)w, (uint)h);
	}

	private static int RoundToMultipleOfTwo(int n)
	{
		var rem = n % 2;
		var result = n - rem;
		if (rem >= 1)
			result += 2;
		return result;
	}
	internal static (float, float) PositionFrom((int, int) screenPixel)
	{
		var x = Map(screenPixel.Item1, 0, Size.Item1, 0, Vertices.prevDrawTilemapCellCount.Item1);
		var y = Map(screenPixel.Item2, 0, Size.Item2, 0, Vertices.prevDrawTilemapCellCount.Item2);

		return (x, y);
	}
	private static float Map(float number, float a1, float a2, float b1, float b2)
	{
		var value = (number - a1) / (a2 - a1) * (b2 - b1) + b1;
		return float.IsNaN(value) || float.IsInfinity(value) ? b1 : value;
	}

	#endregion
}
