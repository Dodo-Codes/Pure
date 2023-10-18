﻿namespace Pure.Default.RendererUserInterface;

using Engine.Tilemap;
using Engine.UserInterface;
using Engine.Utilities;

public static class Default
{
    public static bool IsInteractable
    {
        get;
        set;
    } = true;

    public static void SetCheckbox(this TilemapPack maps, Button checkbox, int zOrder = 0)
    {
        var color = checkbox.IsSelected ? Color.Green : Color.Red;
        var tileId = checkbox.IsSelected ? Tile.ICON_TICK : Tile.UPPERCASE_X;
        var tile = new Tile(tileId, GetColor(checkbox, color));

        Clear(maps, checkbox, zOrder);
        maps[zOrder].SetTile(checkbox.Position, tile);
        maps[zOrder].SetTextLine(
            position: (checkbox.Position.x + 2, checkbox.Position.y),
            text: checkbox.Text,
            tint: GetColor(checkbox, color));
    }
    public static void SetButton(
        this TilemapPack maps,
        Button button,
        int zOrder = 0,
        bool isDisplayingSelection = false)
    {
        var b = button;

        var (w, h) = b.Size;
        var offsetW = w / 2 - Math.Min(b.Text.Length, w - 2) / 2;
        var c = b.IsSelected && isDisplayingSelection ? Color.Green : Color.Yellow;
        var color = GetColor(b, c.ToDark());
        var colorBack = Color.Gray.ToDark(0.6f);

        Clear(maps, button, zOrder);
        maps[zOrder].SetBox(b.Position, b.Size,
            tileFill: new(Tile.SHADE_OPAQUE, colorBack),
            cornerTileId: Tile.BOX_CORNER_ROUND,
            borderTileId: Tile.SHADE_OPAQUE,
            borderTint: colorBack);
        maps[zOrder + 1].SetBox(b.Position, b.Size,
            tileFill: Tile.EMPTY,
            cornerTileId: Tile.BOX_DEFAULT_CORNER,
            borderTileId: Tile.BOX_DEFAULT_STRAIGHT,
            borderTint: color);
        maps[zOrder + 2].SetTextLine(
            position: (b.Position.x + offsetW, b.Position.y + h / 2),
            text: b.Text,
            tint: color,
            maxLength: w - 2);
    }
    public static void SetButtonSelect(this TilemapPack maps, Button button, int zOrder = 0)
    {
        var b = button;
        var (w, h) = b.Size;
        var offsetW = w / 2 - Math.Min(b.Text.Length, w - 2) / 2;
        var selColor = b.IsSelected ? Color.Green : Color.Gray;

        Clear(maps, button, zOrder);
        maps[zOrder].SetBar(b.Position,
            tileIdEdge: Tile.BAR_BIG_EDGE,
            tileId: Tile.SHADE_OPAQUE,
            tint: GetColor(b, Color.Brown.ToDark(0.3f)),
            size: w);
        maps[zOrder + 1].SetTextLine(
            position: (b.Position.x + offsetW, b.Position.y + h / 2),
            text: b.Text,
            tint: GetColor(b, selColor),
            maxLength: w - 2);
    }
    public static void SetInputBox(this TilemapPack maps, InputBox inputBox, int zOrder = 0)
    {
        var ib = inputBox;
        var bgColor = Color.Gray.ToDark(0.4f);
        var selectColor = ib.IsFocused ? Color.Blue : Color.Blue.ToBright();

        Clear(maps, inputBox, zOrder);
        maps[zOrder].SetRectangle(ib.Position, ib.Size, new(Tile.SHADE_OPAQUE, bgColor));
        maps[zOrder].SetTextRectangle(ib.Position, ib.Size, ib.Selection, selectColor, false);
        maps[zOrder + 1].SetTextRectangle(ib.Position, ib.Size, ib.Text, isWordWrapping: false);

        if (string.IsNullOrWhiteSpace(ib.Value))
            maps[zOrder + 1].SetTextRectangle(ib.Position, ib.Size, ib.Placeholder,
                tint: Color.Gray.ToBright(),
                alignment: Tilemap.Alignment.TopLeft);

        if (ib.IsCursorVisible)
            maps[zOrder + 2].SetTile(ib.PositionFromIndices(ib.CursorIndices),
                new(Tile.SHAPE_LINE, Color.White, 2));
    }
    public static void SetFileViewerItem(
        this TilemapPack maps,
        FileViewer fileViewer,
        Button item,
        int zOrder = 1)
    {
        var color = item.IsSelected ? Color.Green : Color.Gray.ToBright();
        var (x, y) = item.Position;
        var icon = fileViewer.IsFolder(item) ?
            new Tile(Tile.ICON_FOLDER, GetColor(item, Color.Yellow)) :
            new(Tile.ICON_FILE, GetColor(item, Color.Gray.ToBright()));

        maps[zOrder].SetTile((x, y), icon);
        maps[zOrder].SetTextLine(
            position: (x + 1, y),
            item.Text,
            GetColor(item, color),
            maxLength: item.Size.width - 1);
    }
    public static void SetFileViewer(this TilemapPack maps, FileViewer fileViewer, int zOrder = 0)
    {
        var f = fileViewer;
        var color = GetColor(f.Back, Color.Gray);
        var (x, y) = f.Back.Position;

        Clear(maps, f, zOrder);
        SetBackground(maps[zOrder], f);

        if (f.FilesAndFolders.Scroll.IsHidden == false)
            SetScroll(maps, f.FilesAndFolders.Scroll, zOrder);

        maps[zOrder + 1].SetTile((x, y), new(Tile.ICON_BACK, color));
        maps[zOrder + 1].SetTextLine(
            position: (x + 1, y),
            text: f.CurrentDirectory,
            color,
            maxLength: -f.Back.Size.width + 1);
    }
    public static void SetSlider(this TilemapPack maps, Slider slider, int zOrder = 0)
    {
        var s = slider;
        var (w, h) = s.Size;
        var text = s.IsVertical ? $"{s.Progress:F2}" : $"{s.Progress * 100f:F0}%";
        var isHandle = s.Handle.IsPressedAndHeld;
        var color = GetColor(isHandle ? s.Handle : s, Color.Gray.ToBright());

        Clear(maps, s, zOrder);
        SetBackground(maps[zOrder], s);
        maps[zOrder + 1].SetBar(s.Handle.Position,
            tileIdEdge: Tile.BAR_DEFAULT_EDGE,
            tileId: Tile.BAR_DEFAULT_STRAIGHT,
            color,
            size: s.Size.height,
            isVertical: s.IsVertical == false);
        maps[zOrder + 2].SetTextLine(
            position: (s.Position.x + w / 2 - text.Length / 2, s.Position.y + h / 2),
            text);
    }
    public static void SetScroll(this TilemapPack maps, Scroll scroll, int zOrder = 0)
    {
        var s = scroll;
        var scrollUpAng = (sbyte)(s.IsVertical ? 1 : 0);
        var scrollDownAng = (sbyte)(s.IsVertical ? 3 : 2);
        var scrollColor = Color.Gray.ToBright();
        var isHandle = s.Slider.Handle.IsPressedAndHeld;

        Clear(maps, s, zOrder);
        SetBackground(maps[zOrder], s, 0.4f);
        maps[zOrder + 1].SetTile(s.Increase.Position,
            new(Tile.ARROW, GetColor(s.Increase, scrollColor), scrollUpAng));
        maps[zOrder + 1].SetTile(s.Slider.Handle.Position,
            new(Tile.SHAPE_CIRCLE, GetColor(isHandle ? s.Slider.Handle : s.Slider, scrollColor)));
        maps[zOrder + 1].SetTile(s.Decrease.Position,
            new(Tile.ARROW, GetColor(s.Decrease, scrollColor), scrollDownAng));
    }
    public static void SetStepper(this TilemapPack maps, Stepper stepper, int zOrder = 0)
    {
        var s = stepper;
        var text = MathF.Round(s.Step, 2).Precision() == 0 ? $"{s.Value}" : $"{s.Value:F2}";
        var color = Color.Gray.ToBright();

        Clear(maps, s, zOrder);
        SetBackground(maps[zOrder], stepper);

        maps[zOrder + 1].SetTile(
            position: s.Decrease.Position,
            tile: new(Tile.ARROW, GetColor(s.Decrease, color), angle: 1));
        maps[zOrder + 1].SetTile(
            s.Increase.Position,
            tile: new(Tile.ARROW, GetColor(s.Increase, color), angle: 3));
        maps[zOrder + 1].SetTextLine(
            position: (s.Position.x + 2, s.Position.y),
            s.Text);
        maps[zOrder + 1].SetTextLine(
            position: (s.Position.x + 2, s.Position.y + 1),
            text);

        maps[zOrder + 1].SetTile(
            position: s.Minimum.Position,
            tile: new(Tile.MATH_MUCH_LESS, GetColor(s.Minimum, color)));
        maps[zOrder + 1].SetTile(
            position: s.Middle.Position,
            tile: new(Tile.PUNCTUATION_PIPE, GetColor(s.Middle, color)));
        maps[zOrder + 1].SetTile(
            position: s.Maximum.Position,
            tile: new(Tile.MATH_MUCH_GREATER, GetColor(s.Maximum, color)));
    }
    public static void SetPrompt(this TilemapPack maps, Prompt prompt, Button[] buttons, int zOrder = 0)
    {
        if (prompt.IsOpened)
        {
            var tile = new Tile(Tile.SHADE_OPAQUE, new Color(0, 0, 0, 127));
            maps[zOrder].SetRectangle((0, 0), maps.Size, tile);
            maps[zOrder + 1].SetBox(prompt.Position, prompt.Size,
                tileFill: new(Tile.SHADE_OPAQUE, Color.Gray.ToDark(0.6f)),
                cornerTileId: Tile.BOX_CORNER_ROUND,
                borderTileId: Tile.SHADE_OPAQUE,
                borderTint: Color.Gray.ToDark(0.6f));
        }

        var messageSize = (prompt.Size.width, prompt.Size.height - 1);
        maps[zOrder + 2].SetTextRectangle(prompt.Position, messageSize, prompt.Message,
            alignment: Tilemap.Alignment.Center);

        for (var i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            var tile = new Tile(Tile.ICON_TICK, GetColor(btn, Color.Green));
            if (i == 1)
            {
                tile.Id = Tile.ICON_CANCEL;
                tile.Tint = GetColor(btn, Color.Red);
            }

            maps[zOrder + 3].SetTile(btn.Position, tile);
        }
    }
    public static void SetPanel(this TilemapPack maps, Panel panel, int zOrder = 0)
    {
        var p = panel;
        var (x, y) = p.Position;
        var (w, _) = p.Size;

        Clear(maps, p, zOrder);
        SetBackground(maps[zOrder], p, 0.6f);

        maps[zOrder + 1].SetBox(p.Position, p.Size, Tile.EMPTY, Tile.BOX_GRID_CORNER,
            Tile.BOX_GRID_STRAIGHT, Color.Blue);
        maps[zOrder + 1].SetTextLine(
            position: (x + w / 2 - p.Text.Length / 2, y),
            p.Text,
            maxLength: Math.Min(w, p.Text.Length));
    }
    public static void SetPalette(this TilemapPack maps, Palette palette, int zOrder = 0)
    {
        var p = palette;
        var tile = new Tile(Tile.SHADE_OPAQUE, GetColor(p.Opacity, Color.Gray.ToBright()));

        Clear(maps, p, zOrder);
        maps[zOrder].SetRectangle(
            position: p.Opacity.Position,
            size: p.Opacity.Size,
            tile: new(Tile.SHADE_5, Color.Gray.ToDark()));
        maps[zOrder + 1].SetBar(
            p.Opacity.Position,
            tileIdEdge: Tile.BAR_BIG_EDGE,
            tileId: Tile.BAR_BIG_STRAIGHT,
            p.SelectedColor,
            p.Opacity.Size.width);
        maps[zOrder + 1].SetTile(p.Opacity.Handle.Position, tile);

        maps[zOrder + 1].SetTile(p.Pick.Position, new(Tile.MATH_PLUS, GetColor(p.Pick, Color.Gray)));
    }
    public static void SetPages(this TilemapPack maps, Pages pages, int zOrder = 0)
    {
        var p = pages;

        Clear(maps, p, zOrder);
        SetBackground(maps[zOrder], p);
        Button(p.First, Tile.MATH_MUCH_LESS, GetColor(p.First, Color.Red));
        Button(p.Previous, Tile.MATH_LESS, GetColor(p.Previous, Color.Yellow));
        Button(p.Next, Tile.MATH_GREATER, GetColor(p.Next, Color.Yellow));
        Button(p.Last, Tile.MATH_MUCH_GREATER, GetColor(p.Last, Color.Red));

        return;

        void Button(Button button, int tileId, Color color)
        {
            maps[zOrder].SetBar(
                button.Position,
                tileIdEdge: Tile.BAR_BIG_EDGE,
                tileId: Tile.SHADE_OPAQUE,
                tint: color.ToDark(0.75f),
                button.Size.height,
                isVertical: true);
            maps[zOrder + 1].SetTile(
                position: (button.Position.x, button.Position.y + button.Size.height / 2),
                tile: new(tileId, color));
        }
    }
    public static void SetPagesItem(this TilemapPack maps, Pages pages, Button item, int zOrder = 1)
    {
        var color = GetColor(item, item.IsSelected ? Color.Green : Color.Gray.ToBright(0.2f));
        var text = item.Text.ToNumber().PadZeros(-pages.ItemWidth);
        maps[zOrder].SetTextLine(item.Position, text, color);
    }
    public static void SetPagesIcon(this TilemapPack maps, Button item, int tileId, int zOrder = 1)
    {
        var color = GetColor(item, item.IsSelected ? Color.Green : Color.Gray.ToBright(0.2f));
        maps[zOrder].SetTile(
            item.Position,
            tile: new(tileId + int.Parse(item.Text), color));
    }
    public static void SetList(this TilemapPack maps, List list, int zOrder = 0)
    {
        Clear(maps, list, zOrder);
        maps[zOrder].SetRectangle(
            list.Position,
            list.Size,
            tile: new(Tile.SHADE_OPAQUE, Color.Gray.ToDark()));

        if (list.Scroll.IsHidden == false)
            SetScroll(maps, list.Scroll, zOrder + 1);

        if (list.IsCollapsed)
            maps[zOrder + 2].SetTile(
                position: (list.Position.x + list.Size.width - 1, list.Position.y),
                tile: new(Tile.MATH_GREATER, GetColor(list, Color.Gray.ToBright()), angle: 1));
    }
    public static void SetListItem(this TilemapPack maps, List list, Button item, int zOrder = 1)
    {
        var color = item.IsSelected ? Color.Green : Color.Gray.ToBright(0.3f);
        var (x, y) = item.Position;
        var (_, h) = item.Size;
        var isLeftCrop =
            list.Span == List.Spans.Horizontal &&
            item.Size.width < list.ItemSize.width &&
            item.Position == list.Position;

        SetBackground(maps[zOrder], item, 0.25f);
        maps[zOrder + 1].SetTextLine(
            position: (x, y + h / 2),
            item.Text,
            GetColor(item, color),
            maxLength: item.Size.width * (isLeftCrop ? -1 : 1));
    }
    public static void SetLayoutSegment(
        this TilemapPack maps,
        (int x, int y, int width, int height) segment,
        int index,
        bool isIndexVisible,
        int zOrder = 0)
    {
        var colors = new uint[]
        {
            Color.Red, Color.Blue, Color.Brown, Color.Violet, Color.Gray,
            Color.Orange, Color.Cyan, Color.Black, Color.Azure, Color.Purple,
            Color.Magenta, Color.Green, Color.Pink, Color.Yellow
        };

        maps[zOrder].SetBox(
            position: (segment.x, segment.y),
            size: (segment.width, segment.height),
            tileFill: new(Tile.SHADE_OPAQUE, colors[index]),
            cornerTileId: Tile.BOX_CORNER_ROUND,
            borderTileId: Tile.SHADE_OPAQUE,
            borderTint: colors[index]);

        if (isIndexVisible)
            maps[zOrder + 1].SetTextRectangle(
                position: (segment.x, segment.y),
                size: (segment.width, segment.height),
                text: index.ToString(),
                alignment: Tilemap.Alignment.Center);
    }

#region Backend
    private static Color GetColor(Block block, Color baseColor)
    {
        if (block.IsDisabled || IsInteractable == false) return baseColor;
        if (block.IsPressedAndHeld) return baseColor.ToDark();
        else if (block.IsHovered) return baseColor.ToBright();

        return baseColor;
    }
    private static void SetBackground(Tilemap map, Block block, float shade = 0.5f)
    {
        var e = block;
        var color = Color.Gray.ToDark(shade);
        var tile = new Tile(Tile.SHADE_OPAQUE, color);

        map.SetBox(e.Position, e.Size, tile, Tile.BOX_CORNER_ROUND, Tile.SHADE_OPAQUE, color);
    }
    private static void Clear(TilemapPack maps, Block block, int zOrder)
    {
        for (var i = zOrder; i < zOrder + 3; i++)
            if (i < maps.Count)
                maps[i].SetRectangle(block.Position, block.Size, Tile.SHADE_TRANSPARENT);
    }
#endregion
}