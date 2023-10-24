﻿global using Pure.Engine.Utilities;
global using Pure.Engine.Tilemap;
global using Pure.Engine.UserInterface;
global using Pure.Engine.Window;
global using static Pure.Editors.EditorUserInterface.Program;
global using static Pure.Default.RendererUserInterface.Default;

namespace Pure.Editors.EditorUserInterface;

public static class Program
{
    internal enum Layer
    {
        Grid,
        UiBack,
        UiMiddle,
        UiFront,
        EditBack,
        EditMiddle,
        EditFront,
        PromptFade,
        PromptBack,
        PromptMiddle,
        PromptFront,
        Count
    }

    internal enum MenuType
    {
        Main,
        Add,
        AddList
    }

    internal static Block? Selected
    {
        get;
        set;
    }

    internal static readonly Dictionary<MenuType, Menu> menus = new();
    internal static readonly TilemapPack maps;
    internal static readonly BlockPack ui;
    internal static readonly RendererEdit editUI;
    internal static readonly Prompt prompt;
    internal static readonly Slider promptSlider;
    internal static readonly EditPanel editPanel;
    internal static readonly FileViewer saveLoad;
    internal static readonly InputBox fileName;

    internal static (float x, float y) MousePosition
    {
        get;
        set;
    }
    internal static (int x, int y) CameraPosition
    {
        get;
        set;
    }
    internal static (int w, int h) CameraSize
    {
        get;
        set;
    }

    public static void Run()
    {
        while (Window.IsOpen)
        {
            Window.Activate(true);
            Time.Update();
            maps.Clear();

            //Window.LayerAdd(offset: offset, zoom: zoom);
            Update();
            RendererEdit.DrawGrid();

            Mouse.CursorGraphics = (Mouse.Cursor)Input.MouseCursorResult;
            for (var i = 0; i < maps.Count; i++)
            {
                var t = maps[i];
                prevMousePos = MousePosition;
                MousePosition = Mouse.PixelToWorld(Mouse.CursorPosition);
                Window.DrawTiles(t.ToBundle());
            }

            Window.Activate(false);
        }
    }

    internal static void DisplayInfoText(string text)
    {
        var infoTextSplit = infoText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        if (infoTextSplit.Length > 0 && infoTextSplit[^1].Contains(text))
        {
            var lastLine = infoTextSplit[^1];
            if (lastLine.Contains(')'))
            {
                var split = lastLine.Split("(")[1].Replace(")", "");
                var number = int.Parse(split) + 1;
                text += $" ({number})";
            }
            else
                text += " (2)";
        }

        infoText += text + Environment.NewLine;
        infoTextTimer = 2f;
    }

#region Backend
    private static string infoText = "";
    private static float infoTextTimer;
    private static float zoom = 1f;
    private static (float x, float y) prevMousePos, offset;

    static Program()
    {
        Window.Create();
        Window.Title = "Pure - User Interface Editor";

        var (width, height) = Window.MonitorAspectRatio;
        maps = new((int)Layer.Count, (width * 10, height * 10));
        ui = new();
        editUI = new();
        editPanel = new((int.MaxValue, int.MaxValue));

        prompt = new() { ButtonCount = 2 };
        prompt.OnDisplay(() => maps.SetPrompt(prompt, zOrder: (int)Layer.PromptFade));
        prompt.OnItemDisplay(item => maps.SetPromptItem(prompt, item, zOrder: (int)Layer.PromptMiddle));
        promptSlider = new() { Size = (15, 1) };
        promptSlider.OnDisplay(() => maps.SetSlider(promptSlider, (int)Layer.PromptBack));

        saveLoad = new() { Size = (20, 10) };
        saveLoad.OnDisplay(() => maps.SetFileViewer(saveLoad, (int)Layer.PromptBack));
        saveLoad.FilesAndFolders.IsSingleSelecting = true;
        saveLoad.FilesAndFolders.OnItemDisplay(item =>
            maps.SetFileViewerItem(saveLoad, item, (int)Layer.PromptMiddle));
        fileName = new() { Size = (20, 1), IsSingleLine = true };
        fileName.OnDisplay(() => maps.SetInputBox(fileName, (int)Layer.PromptBack));

        // submenus need higher update priority to not close upon parent menu opening them
        menus[MenuType.AddList] = new MenuAddList();
        menus[MenuType.Add] = new MenuAdd();
        menus[MenuType.Main] = new MenuMain();
    }

    private static void Update()
    {
        Input.Update(
            Mouse.IsButtonPressed(Mouse.Button.Left),
            MousePosition,
            Mouse.ScrollDelta,
            Keyboard.KeyIDsPressed,
            Keyboard.KeyTyped);

        TryControlCamera();

        editPanel.IsHidden = Selected == null;

        IsInteractable = false;
        ui.Update();
        IsInteractable = true;

        editUI.Update();
        editPanel.Update();

        if (prompt.IsHidden == false)
            prompt.Update();

        foreach (var kvp in menus)
            kvp.Value.Update();

        UpdateInfoText();

        var onLmbRelease = (Mouse.IsButtonPressed(Mouse.Button.Left) == false).Once("on-lmb-deselect");
        if (onLmbRelease && GetHovered() == null && editPanel.IsHovered == false &&
            prompt.IsHidden)
            Selected = null;
    }
    private static void UpdateInfoText()
    {
        /*
        infoTextTimer -= Time.Delta;

        const int TEXT_WIDTH = 32;
        const int TEXT_HEIGHT = 2;
        var x = CameraPosition.x + CameraSize.w / 2 - TEXT_WIDTH / 2;
        var topY = CameraPosition.y;
        var bottomY = topY + CameraSize.h;
        var (mx, my) = MousePosition;

        maps[(int)Layer.EditFront]
            .SetTextRectangle((x, bottomY - 1), (TEXT_WIDTH, 1), $"Cursor {(int)mx}, {(int)my}",
                alignment: Alignment.Center);

        if (infoTextTimer <= 0)
        {
            infoText = "";
            return;
        }

        maps[(int)Layer.EditFront]
            .SetTextRectangle((x, topY), (TEXT_WIDTH, TEXT_HEIGHT), infoText,
                alignment: Alignment.Top, scrollProgress: 1f);
        */
    }

    private static void TryControlCamera()
    {
        if (prompt.IsHidden == false)
            return;

        /*
        var prevSz = CameraSize;
        var prevPos = CameraPosition;

        if (Mouse.ScrollDelta != 0 && editPanel.IsHovered == false)
        {
            var prevZoom = zoom;
            zoom -= Mouse.ScrollDelta;
            zoom = Math.Clamp(zoom, 0.3f, 2f);

            var cx = (float)Mouse.CursorPosition.x;
            var cy = (float)Mouse.CursorPosition.y;
            cx = cx.Map((0, Window.Size.width), (0, 1)) * 16f;
            cy = cy.Map((0, Window.Size.height), (0, 1)) * 9.5f;
            var newX = CameraPosition.x + cx;
            var newY = CameraPosition.y + cy;

            if (prevZoom != zoom)
                CameraPosition = Mouse.ScrollDelta > 0 ?
                    ((int)newX, (int)newY) :
                    (CameraPosition.x - 8, CameraPosition.y - 5);
        }

        var mousePos = Mouse.PixelToWorld(Mouse.CursorPosition);
        var tmapCameraAspectX = (float)maps.Size.width / CameraSize.w;
        var tmapCameraAspectY = (float)maps.Size.height / CameraSize.h;
        mousePos.x /= tmapCameraAspectX;
        mousePos.y /= tmapCameraAspectY;
        var (mx, my) = ((int)mousePos.x, (int)mousePos.y);
        var (px, py) = ((int)prevMousePos.x, (int)prevMousePos.y);
        var mmb = Mouse.IsButtonPressed(Mouse.Button.Middle);

        if (mmb && (px != mx || py != my))
        {
            var (deltaX, deltaY) = (mx - px, my - py);
            CameraPosition = (CameraPosition.x - deltaX, CameraPosition.y - deltaY);
        }

        var (w, h) = CameraSize;
        if (prevPos != CameraPosition)
        {
            DisplayInfoText($"Camera {CameraPosition.x + w / 2}, {CameraPosition.y + h / 2}");
        }

        if (prevSz != CameraSize)
        {
            DisplayInfoText($"Camera {CameraSize.w}x{CameraSize.h}");
        }

        prevMousePos = (mx, my);
        */
    }

    private static Block? GetHovered()
    {
        for (var i = editUI.Count - 1; i >= 0; i--)
        {
            if (editUI[i].IsOverlapping(MousePosition))
                return editUI[i];
        }

        return null;
    }
#endregion
}