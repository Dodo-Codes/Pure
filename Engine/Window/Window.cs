﻿global using SFML.Graphics;
global using SFML.System;
global using SFML.Window;
global using System.Diagnostics.CodeAnalysis;
global using System.Diagnostics;
global using System.IO.Compression;
global using System.Text;
using SFML.Graphics.Glsl;

namespace Pure.Engine.Window;

/// <summary>
/// Possible window modes.
/// </summary>
public enum Mode
{
    Windowed, Borderless, Fullscreen
}

/// <summary>
/// Provides access to an OS window and its properties.
/// </summary>
public static class Window
{
    /// <summary>
    /// Gets the mode that the window was created with.
    /// </summary>
    public static Mode Mode
    {
        get => mode;
        set
        {
            if (mode != value && window != null)
                isRecreating = true;

            mode = value;
            if (mode == Mode.Fullscreen)
                monitor = 0;

            TryCreate();
        }
    }
    /// <summary>
    /// Gets or sets the title of the window.
    /// </summary>
    public static string Title
    {
        get => title;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                value = "Game";

            title = value;
            TryCreate();
            window.SetTitle(title);
        }
    }
    /// <summary>
    /// Gets the size of the window.
    /// </summary>
    public static (uint width, uint height) Size
    {
        get => window != null ? (window.Size.X, window.Size.Y) : (0, 0);
    }
    /// <summary>
    /// Gets a value indicating whether the window is focused.
    /// </summary>
    public static bool IsFocused
    {
        get => window != null && window.HasFocus();
    }
    /// <summary>
    /// Gets or sets a value indicating whether the window should use retro TV graphics.
    /// </summary>
    public static bool IsRetro
    {
        get => isRetro && retroShader != null && Shader.IsAvailable;
        set
        {
            if (value && retroShader == null && Shader.IsAvailable)
                retroShader = new EffectWindow().Shader;

            isRetro = value;
            TryCreate();
        }
    }
    public static uint BackgroundColor
    {
        get => backgroundColor;
        set
        {
            backgroundColor = value;
            TryCreate();
        }
    }
    public static uint Monitor
    {
        get => monitor;
        set
        {
            if (mode == Mode.Fullscreen)
                return;

            monitor = (uint)Math.Min(value, Engine.Window.Monitor.Monitors.Length - 1);
            RecreateRenderTexture();
            TryCreate();
            Center();
        }
    }
    public static float PixelScale
    {
        get => pixelScale;
        set
        {
            pixelScale = value;
            TryCreate();
            RecreateRenderTexture();
        }
    }
    public static bool IsVerticallySynced
    {
        get => isVerticallySynced;
        set
        {
            isVerticallySynced = value;
            TryCreate();
            window.SetVerticalSyncEnabled(value);
        }
    }
    public static uint MaximumFrameRate
    {
        get => maximumFrameRate;
        set
        {
            maximumFrameRate = value;
            TryCreate();
            window.SetFramerateLimit(value);
        }
    }
    public static string? Clipboard
    {
        get => SFML.Window.Clipboard.Contents;
        set => SFML.Window.Clipboard.Contents = value;
    }

    public static void FromBytes(byte[] bytes)
    {
        var b = Decompress(bytes);
        var offset = 0;

        mode = (Mode)GetBytesFrom(b, 1, ref offset)[0];
        var bTitleLength = GetInt();
        title = Encoding.UTF8.GetString(GetBytesFrom(b, bTitleLength, ref offset));
        isRetro = GetBool();
        backgroundColor = GetUInt();
        monitor = GetUInt();
        pixelScale = GetFloat();
        isVerticallySynced = GetBool();
        maximumFrameRate = GetUInt();
        var (x, y, w, h) = (GetInt(), GetInt(), GetUInt(), GetUInt());
        TryCreate();
        window.Position = new(x, y);
        window.Size = new(w, h);

        float GetFloat()
        {
            return BitConverter.ToSingle(GetBytesFrom(b, 4, ref offset));
        }
        int GetInt()
        {
            return BitConverter.ToInt32(GetBytesFrom(b, 4, ref offset));
        }
        uint GetUInt()
        {
            return BitConverter.ToUInt32(GetBytesFrom(b, 4, ref offset));
        }
        bool GetBool()
        {
            return BitConverter.ToBoolean(GetBytesFrom(b, 1, ref offset));
        }
    }
    public static void FromBase64(string base64)
    {
        FromBytes(Convert.FromBase64String(base64));
    }
    public static string ToBase64()
    {
        return Convert.ToBase64String(ToBytes());
    }
    public static byte[] ToBytes()
    {
        TryCreate();

        var result = new List<byte>();
        var bTitle = Encoding.UTF8.GetBytes(Title);
        result.Add((byte)Mode);
        result.AddRange(BitConverter.GetBytes(bTitle.Length));
        result.AddRange(bTitle);
        result.AddRange(BitConverter.GetBytes(IsRetro));
        result.AddRange(BitConverter.GetBytes(BackgroundColor));
        result.AddRange(BitConverter.GetBytes(Monitor));
        result.AddRange(BitConverter.GetBytes(PixelScale));
        result.AddRange(BitConverter.GetBytes(IsVerticallySynced));
        result.AddRange(BitConverter.GetBytes(MaximumFrameRate));
        result.AddRange(BitConverter.GetBytes(window.Position.X));
        result.AddRange(BitConverter.GetBytes(window.Position.Y));
        result.AddRange(BitConverter.GetBytes(window.Size.X));
        result.AddRange(BitConverter.GetBytes(window.Size.Y));
        return Compress(result.ToArray());
    }

    public static bool KeepOpen()
    {
        if (isRecreating)
            Recreate();

        TryCreate();

        Keyboard.Update();
        Mouse.Update();
        FinishDraw();

        if (hasClosed)
            return false;

        window.Display();
        window.DispatchEvents();
        window.Clear(new(BackgroundColor));
        window.SetActive();

        renderTexture?.Clear(new(BackgroundColor));
        return window.IsOpen;
    }
    public static void Draw(this Layer layer)
    {
        TryCreate();

        var tex = Layer.tilesets[layer.AtlasPath];
        var centerX = layer.TilemapPixelSize.w / 2f * layer.Zoom;
        var centerY = layer.TilemapPixelSize.h / 2f * layer.Zoom;
        var r = new RenderStates(BlendMode.Alpha, Transform.Identity, tex, null);
        var r2 = new RenderStates(BlendMode.Alpha, Transform.Identity, anotherPass?.Texture, layer.shader);

        r.Transform.Translate(layer.Offset.x - centerX, layer.Offset.y - centerY);
        r.Transform.Scale(layer.Zoom, layer.Zoom);

        var (w, h) = (renderTexture?.Size.X ?? 0, renderTexture?.Size.Y ?? 0);
        verts[0] = new(new(-w / 2f, -h / 2f), Color.White, new(0, 0));
        verts[1] = new(new(w / 2f, -h / 2f), Color.White, new(w, 0));
        verts[2] = new(new(w / 2f, h / 2f), Color.White, new(w, h));
        verts[3] = new(new(-w / 2f, h / 2f), Color.White, new(0, h));

        // x y w h | rect
        // r g b a | target color
        // r g b a | edge color
        // t - - - | type

        var view = anotherPass?.GetView();
        layer.edgeCount = 0;
        layer.waveCount = 0;
        layer.blurCount = 0;
        layer.shader?.SetUniform("viewSize", new Vec2(view?.Size.X ?? 0, view?.Size.Y ?? 0));
        layer.shader?.SetUniform("time", time.ElapsedTime.AsSeconds());
        anotherPass?.Clear(new(layer.BackgroundColor));
        anotherPass?.Draw(layer.verts, r);
        anotherPass?.Display();

        renderTexture?.Draw(verts, PrimitiveType.Quads, r2);

        layer.verts.Clear();
    }
    public static void Close()
    {
        if (window == null || renderTexture == null)
            return;

        if (IsRetro || isClosing)
        {
            isClosing = true;

            StartRetroAnimation();
            return;
        }

        hasClosed = true;
        close?.Invoke();
        window.Close();
    }
    public static void Scale(float scale)
    {
        scale = Math.Max(scale, 0.05f);

        TryCreate();
        var (mw, mh) = Engine.Window.Monitor.Current.Size;
        window.Size = new((uint)(mw * scale), (uint)(mh * scale));
    }
    public static void Center()
    {
        TryCreate();

        var current = Engine.Window.Monitor.Current;
        var (x, y) = current.position;
        var (w, h) = current.Size;
        var (ww, wh) = Size;

        x += w / 2 - (int)ww / 2;
        y += h / 2 - (int)wh / 2;

        window.Position = new(x, y);
    }
    public static void SetIconFromTile(Layer layer, (int id, uint tint) tile, (int id, uint tint) tileBack = default, bool saveAsFile = false)
    {
        TryCreate();

        const uint SIZE = 64;
        var rend = new RenderTexture(SIZE, SIZE);
        var texture = Layer.tilesets[layer.AtlasPath];
        var (bx, by) = IndexToCoords(tileBack.id, layer);
        var (fx, fy) = IndexToCoords(tile.id, layer);
        var (tw, th) = layer.AtlasTileSize;
        var (gw, gh) = layer.AtlasTileGap;
        tw += gw;
        th += gh;
        var verts = new Vertex[]
        {
            new(new(0, 0), new(tileBack.tint), new(tw * bx, th * by)),
            new(new(SIZE, 0), new(tileBack.tint), new(tw * (bx + 1), th * by)),
            new(new(SIZE, SIZE), new(tileBack.tint), new(tw * (bx + 1), th * (by + 1))),
            new(new(0, SIZE), new(tileBack.tint), new(tw * bx, th * (by + 1))),
            new(new(0, 0), new(tile.tint), new(tw * fx, th * fy)),
            new(new(SIZE, 0), new(tile.tint), new(tw * (fx + 1), th * fy)),
            new(new(SIZE, SIZE), new(tile.tint), new(tw * (fx + 1), th * (fy + 1))),
            new(new(0, SIZE), new(tile.tint), new(tw * fx, th * (fy + 1)))
        };
        rend.Draw(verts, PrimitiveType.Quads, new(texture));
        rend.Display();
        var image = rend.Texture.CopyToImage();
        window.SetIcon(SIZE, SIZE, image.Pixels);

        if (saveAsFile)
            image.SaveToFile("icon.png");

        rend.Dispose();
        image.Dispose();
    }

    public static void OnClose(Action method)
    {
        close += method;
    }

#region Backend
    internal static RenderWindow? window;
    internal static RenderTexture? renderTexture, anotherPass;
    internal static (int w, int h) renderTextureViewSize;

    private static Action? close;
    private static Shader? retroShader;
    private static readonly Random retroRand = new();
    private static readonly Clock time = new();
    private static System.Timers.Timer? retroTurnoff;
    private static Clock? retroTurnoffTime;
    private const float RETRO_TURNOFF_TIME = 0.5f;
    private static readonly Vertex[] verts = new Vertex[4];

    private static bool isRetro, isClosing, hasClosed, isVerticallySynced, isRecreating;
    private static string title = "Game";
    private static uint backgroundColor, monitor, maximumFrameRate;
    private static Mode mode;
    private static float pixelScale = 5f;

    [MemberNotNull(nameof(window))]
    private static void TryCreate()
    {
        if (window != null)
            return;

        if (renderTexture == null)
            RecreateRenderTexture();

        Recreate();
    }
    [MemberNotNull(nameof(window))]
    private static void Recreate()
    {
        isRecreating = false;

        var prevSize = new Vector2u(960, 540);
        var prevPos = new Vector2i();
        if (window != null)
        {
            prevPos = window.Position;
            prevSize = window.Size;
            window.Dispose();
            window = null;
        }

        var style = Styles.Default;
        style = mode == Mode.Fullscreen ? Styles.Fullscreen : style;
        style = mode == Mode.Borderless ? Styles.None : style;

        window = new(new(prevSize.X, prevSize.Y), title, style) { Position = prevPos };
        window.SetKeyRepeatEnabled(false);
        window.Closed += (_, _) => Close();
        window.Resized += (_, e) =>
        {
            var view = window.GetView();
            view.Center = new(e.Width / 2f, e.Height / 2f);
            view.Size = new(e.Width, e.Height);
            window.SetView(view);
        };
        window.KeyPressed += Keyboard.OnPress;
        window.KeyReleased += Keyboard.OnRelease;
        window.TextEntered += Keyboard.OnType;
        window.MouseButtonPressed += Mouse.OnButtonPressed;
        window.MouseButtonReleased += Mouse.OnButtonReleased;
        window.MouseWheelScrolled += Mouse.OnWheelScrolled;
        window.MouseMoved += Mouse.OnMove;
        window.MouseEntered += Mouse.OnEnter;
        window.MouseLeft += Mouse.OnLeft;
        window.LostFocus += (_, _) =>
        {
            Mouse.CancelInput();
            Keyboard.CancelInput();
        };

        window.DispatchEvents();
        window.Clear();
        window.Display();

        SetIconFromTile(new(), (394, 16711935)); // green joystick

        // set values to the new window
        Title = title;
        IsVerticallySynced = isVerticallySynced;
        MaximumFrameRate = maximumFrameRate;
        Mouse.CursorCurrent = Mouse.CursorCurrent;
        Mouse.IsCursorBounded = Mouse.IsCursorBounded;
        Mouse.IsCursorVisible = Mouse.IsCursorVisible;
        Mouse.TryUpdateSystemCursor();

        Center();
    }
    [MemberNotNull(nameof(renderTexture))]
    private static void RecreateRenderTexture()
    {
        renderTexture?.Dispose();
        renderTexture = null;
        anotherPass?.Dispose();
        anotherPass = null;

        var currentMonitor = Engine.Window.Monitor.Monitors[Monitor];
        var (w, h) = currentMonitor.Size;
        renderTexture = new((uint)(w / pixelScale), (uint)(h / pixelScale));
        var view = renderTexture.GetView();
        view.Center = new();
        renderTextureViewSize = ((int)view.Size.X, (int)view.Size.Y);
        renderTexture.SetView(view);

        anotherPass = new(renderTexture.Size.X, renderTexture.Size.Y);
        anotherPass.SetView(view);
    }

    private static void StartRetroAnimation()
    {
        retroTurnoffTime = new();
        retroTurnoff = new(RETRO_TURNOFF_TIME * 1000);
        retroTurnoff.Start();
        retroTurnoff.Elapsed += (_, _) =>
        {
            hasClosed = true;
            close?.Invoke();
            window?.Close();
        };
    }
    private static void FinishDraw()
    {
        if (renderTexture == null || hasClosed)
            return;

        TryCreate();
        renderTexture.Display();

        var (ww, wh, ow, oh) = GetRenderOffset();
        var (tw, th) = (renderTexture.Size.X, renderTexture.Size.Y);
        var shader = IsRetro ? retroShader : null;
        var rend = new RenderStates(BlendMode.Alpha, Transform.Identity, renderTexture.Texture, shader);
        verts[0] = new(new(ow, oh), Color.White, new(0, 0));
        verts[1] = new(new(ww + ow, oh), Color.White, new(tw, 0));
        verts[2] = new(new(ww + ow, wh + oh), Color.White, new(tw, th));
        verts[3] = new(new(ow, wh + oh), Color.White, new(0, th));

        if (IsRetro)
        {
            var randVec = new Vector2f(retroRand.Next(0, 10) / 10f, retroRand.Next(0, 10) / 10f);
            shader?.SetUniform("time", time.ElapsedTime.AsSeconds());
            shader?.SetUniform("randomVec", randVec);
            shader?.SetUniform("viewSize", new Vector2f(ww, wh));
            shader?.SetUniform("offScreen", new Vector2f(ow, oh));

            if (isClosing && retroTurnoffTime != null)
            {
                var timing = retroTurnoffTime.ElapsedTime.AsSeconds() / RETRO_TURNOFF_TIME;
                shader?.SetUniform("turnoffAnimation", timing);
            }
        }

        window.Draw(verts, PrimitiveType.Quads, rend);
    }

    private static (int, int) IndexToCoords(int index, Layer layer)
    {
        var (tw, th) = layer.AtlasTileCount;
        index = index < 0 ? 0 : index;
        index = index > tw * th - 1 ? tw * th - 1 : index;

        return (index % tw, index / tw);
    }

    internal static (float winW, float winH, float offW, float offH) GetRenderOffset()
    {
        TryCreate();

        var (rw, rh) = Engine.Window.Monitor.Current.AspectRatio;
        var ratio = rw / (float)rh;
        var (ww, wh) = (window.Size.X, window.Size.Y);

        if (ww / (float)wh < ratio)
            wh = (uint)(ww / ratio);
        else
            ww = (uint)(wh * ratio);

        return (ww, wh, (window.Size.X - ww) / 2f, (window.Size.Y - wh) / 2f);
    }

    private static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Optimal))
        {
            stream.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
    private static byte[] Decompress(byte[] data)
    {
        var input = new MemoryStream(data);
        var output = new MemoryStream();
        using (var stream = new DeflateStream(input, CompressionMode.Decompress))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }
    private static byte[] GetBytesFrom(byte[] fromBytes, int amount, ref int offset)
    {
        var result = fromBytes[offset..(offset + amount)];
        offset += amount;
        return result;
    }
#endregion
}