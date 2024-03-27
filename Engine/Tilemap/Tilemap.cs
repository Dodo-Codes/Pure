﻿namespace Pure.Engine.Tilemap;

using System.IO.Compression;
using System.Runtime.InteropServices;

/// <summary>
/// Specifies the alignment of the text in <see cref="Tilemap.SetTextRectangle"/>.
/// </summary>
public enum Alignment
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

/// <summary>
/// Represents a tilemap consisting of a grid of tiles.
/// </summary>
public class Tilemap
{
    public (int x, int y, int z) SeedOffset { get; set; }
    /// <summary>
    /// Gets the size of the tilemap in tiles.
    /// </summary>
    public (int width, int height) Size { get; }
    public Area View { get; set; }

    /// <summary>
    /// Initializes a new tilemap instance with the specified size.
    /// </summary>
    /// <param name="size">The size of the tilemap in tiles.</param>
    public Tilemap((int width, int height) size)
    {
        var (w, h) = size;
        w = Math.Max(w, 1);
        h = Math.Max(h, 1);

        Size = (w, h);
        data = new Tile[w, h];
        bundleCache = new (int, uint, sbyte, bool, bool)[w, h];
        ids = new int[w, h];
        View = (0, 0, size.width, size.height);
    }
    /// <summary>
    /// Initializes a new tilemap instance with the specified tileData.
    /// </summary>
    /// <param name="tileData">The tile data to use for the tilemap.</param>
    /// <exception cref="ArgumentNullException">Thrown if tileData is null.</exception>
    public Tilemap(Tile[,] tileData)
    {
        if (tileData == null)
            throw new ArgumentNullException(nameof(tileData));

        var w = tileData.GetLength(0);
        var h = tileData.GetLength(1);
        Size = (w, h);
        data = Duplicate(tileData);
        bundleCache = new (int, uint, sbyte, bool, bool)[w, h];
        ids = new int[w, h];
        View = (0, 0, w, h);

        for (var i = 0; i < h; i++)
            for (var j = 0; j < w; j++)
            {
                bundleCache[j, i] = tileData[j, i];
                ids[j, i] = tileData[j, i].Id;
            }
    }
    public Tilemap(byte[] bytes)
    {
        var b = Decompress(bytes);
        var offset = 0;
        var w = BitConverter.ToInt32(Get<int>());
        var h = BitConverter.ToInt32(Get<int>());

        data = new Tile[w, h];
        bundleCache = new (int, uint, sbyte, bool, bool)[w, h];
        ids = new int[w, h];
        Size = (w, h);
        View = (BitConverter.ToInt32(Get<int>()), BitConverter.ToInt32(Get<int>()),
            BitConverter.ToInt32(Get<int>()), BitConverter.ToInt32(Get<int>()));

        for (var i = 0; i < h; i++)
            for (var j = 0; j < w; j++)
            {
                var bTile = GetBytesFrom(b, Tile.BYTE_SIZE, ref offset);
                SetTile((j, i), new(bTile), null);
            }

        return;

        byte[] Get<T>()
        {
            return GetBytesFrom(b, Marshal.SizeOf(typeof(T)), ref offset);
        }
    }
    public Tilemap(string base64) : this(Convert.FromBase64String(base64))
    {
    }

    public string ToBase64()
    {
        return Convert.ToBase64String(ToBytes());
    }
    public byte[] ToBytes()
    {
        var result = new List<byte>();
        var (w, h) = Size;
        result.AddRange(BitConverter.GetBytes(w));
        result.AddRange(BitConverter.GetBytes(h));
        result.AddRange(BitConverter.GetBytes(View.X));
        result.AddRange(BitConverter.GetBytes(View.Y));
        result.AddRange(BitConverter.GetBytes(View.Width));
        result.AddRange(BitConverter.GetBytes(View.Height));

        for (var i = 0; i < h; i++)
            for (var j = 0; j < w; j++)
                result.AddRange(TileAt((j, i)).ToBytes());

        return Compress(result.ToArray());
    }
    /// <returns>
    /// A 2D array of the bundle tuples of the tiles in the tilemap.</returns>
    public (int id, uint tint, sbyte turns, bool isMirrored, bool isFlipped)[,] ToBundle()
    {
        return bundleCache;
    }

    /// <summary>
    /// Updates the view of the tilemap.
    /// </summary>
    /// <returns>The updated tilemap view.</returns>
    public Tilemap ViewUpdate()
    {
        var (vx, vy, vw, vh, _) = View.ToBundle();
        var newData = new Tile[vw, vh];
        var i = 0;
        for (var x = vx; x != vx + vw; x++)
        {
            var j = 0;
            for (var y = vy; y != vy + vh; y++)
            {
                newData[i, j] = TileAt((x, y));
                j++;
            }

            i++;
        }

        return new(newData);
    }

    /// <summary>
    /// Gets the tile at the specified position.
    /// </summary>
    /// <param name="position">The position to get the tile from.</param>
    /// <returns>The tile at the specified position, 
    /// or the default tile value if the position is out of bounds.</returns>
    public Tile TileAt((int x, int y) position)
    {
        return IndicesAreValid(position, null) ? data[position.x, position.y] : default;
    }
    /// <summary>
    /// Retrieves a rectangular region of tiles from the tilemap.
    /// </summary>
    /// <param name="area">A tuple representing the rectangle's position and size. 
    /// The x and y values represent the top-left corner of the rectangle, 
    /// while the width and height represent the size of the rectangle.</param>
    /// <returns>A 2D array of tiles representing the specified rectangular region in the tilemap. 
    /// If the rectangle's dimensions are negative, the method will reverse the direction of the iteration.</returns>
    public Tile[,] TilesIn(Area area)
    {
        var (rx, ry) = (area.X, area.Y);
        var (rw, rh) = (area.Width, area.Height);
        var xStep = rw < 0 ? -1 : 1;
        var yStep = rh < 0 ? -1 : 1;
        var result = new Tile[Math.Abs(rw), Math.Abs(rh)]; // Fixed array dimensions

        for (var x = 0; x < Math.Abs(rw); x++)
            for (var y = 0; y < Math.Abs(rh); y++)
            {
                var currentX = rx + x * xStep - (rw < 0 ? 1 : 0);
                var currentY = ry + y * yStep - (rh < 0 ? 1 : 0);

                result[x, y] = TileAt((currentX, currentY));
            }

        return result;
    }

    public void Flush()
    {
        var (w, h) = Size;
        data = new Tile[w, h];
        ids = new int[w, h];
        bundleCache = new (int, uint, sbyte, bool, bool)[w, h];
    }
    public void Fill(Area? mask = null, params Tile[]? tiles)
    {
        if (tiles == null || tiles.Length == 0)
        {
            Flush();
            return;
        }

        for (var y = 0; y < Size.height; y++)
            for (var x = 0; x < Size.width; x++)
            {
                var tile = tiles.Length == 1 ? tiles[0] : ChooseOne(tiles, ToSeed((x, y)));
                SetTile((x, y), tile, mask);
            }
    }
    public void Flood((int x, int y) position, bool isExactTile, Area? mask = null, params Tile[]? tiles)
    {
        if (tiles == null || tiles.Length == 0)
            return;

        var stack = new System.Collections.Generic.Stack<(int x, int y)>();
        var initialTile = TileAt(position);
        stack.Push(position);

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            var curTile = TileAt((x, y));
            var tile = tiles.Length == 1 ? tiles[0] : ChooseOne(tiles, ToSeed((x, y)));
            var exactTile = curTile == tile || curTile != initialTile;
            var onlyId = curTile.Id == tile.Id || curTile.Id != initialTile.Id;

            if ((isExactTile && exactTile) ||
                (isExactTile == false && onlyId))
                continue;

            SetTile((x, y), tile, mask);

            stack.Push((x - 1, y));
            stack.Push((x + 1, y));
            stack.Push((x, y - 1));
            stack.Push((x, y + 1));
        }
    }
    public void Replace(
        Area area,
        Tile targetTile,
        Area? mask = null,
        params Tile[] tiles)
    {
        if (tiles.Length == 0)
            return;

        for (var i = 0; i < Math.Abs(area.Width * area.Height); i++)
        {
            var x = area.X + i % Math.Abs(area.Width) * (area.Width < 0 ? -1 : 1);
            var y = area.Y + i / Math.Abs(area.Width) * (area.Height < 0 ? -1 : 1);

            if (TileAt((x, y)).Id != targetTile.Id)
                continue;

            var tile = tiles.Length == 1 ? tiles[0] : ChooseOne(tiles, ToSeed((x, y)));
            SetTile((x, y), tile, mask);
        }
    }

    /// <summary>
    /// Sets the tile at the specified position 
    /// to the specified tile.
    /// </summary>
    /// <param name="position">The position to set the tile at.</param>
    /// <param name="tile">The tile to set.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetTile((int x, int y) position, Tile tile, Area? mask = null)
    {
        if (IndicesAreValid(position, mask) == false)
            return;

        data[position.x, position.y] = tile;
        ids[position.x, position.y] = tile.Id;
        bundleCache[position.x, position.y] = tile;
    }
    public void SetArea(Area area, Area? mask = null, params Tile[]? tiles)
    {
        if (tiles == null || tiles.Length == 0)
            return;

        var xStep = area.Width < 0 ? -1 : 1;
        var yStep = area.Height < 0 ? -1 : 1;
        var i = 0;
        for (var x = area.X; x != area.X + area.Width; x += xStep)
            for (var y = area.Y; y != area.Y + area.Height; y += yStep)
            {
                if (i > Math.Abs(area.Width * area.Height))
                    return;

                var tile = tiles.Length == 1 ? tiles[0] : ChooseOne(tiles, ToSeed((x, y)));
                SetTile((x, y), tile, mask);
                i++;
            }
    }
    /// <summary>
    /// Sets a group of tiles starting at the specified position to the 
    /// specified 2D tile array.
    /// </summary>
    /// <param name="position">The position to start setting tiles from.</param>
    /// <param name="tiles">The 2D array of tiles to set.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetGroup((int x, int y) position, Tile[,] tiles, Area? mask = null)
    {
        if (tiles.Length == 0)
            return;

        for (var i = 0; i < tiles.GetLength(1); i++)
            for (var j = 0; j < tiles.GetLength(0); j++)
                SetTile((position.x + j, position.y + i), tiles[j, i], mask);
    }
    /// <summary>
    /// Sets a single line of text starting from a position with optional tint and optional shortening.
    /// </summary>
    /// <param name="position">The starting position to place the text.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="tint">Optional tint color value (defaults to white).</param>
    /// <param name="maxLength">Optional shortening that adds ellipsis '…' if exceeded
    /// (defaults to none). Negative values reduce the text from the back.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetTextLine(
        (int x, int y) position,
        string? text,
        uint tint = uint.MaxValue,
        int maxLength = int.MaxValue,
        Area? mask = null)
    {
        var errorOffset = 0;

        if (maxLength == 0)
            return;

        if (text != null)
        {
            var abs = Math.Abs(maxLength);
            if (maxLength > 0 && text.Length > maxLength)
                text = text[..Math.Max(abs - 1, 0)] + "…";
            else if (maxLength < 0 && text.Length > abs)
                text = "…" + text[^(abs - 1)..];
        }

        for (var i = 0; i < text?.Length; i++)
        {
            var symbol = text[i];
            var index = TileIdFrom(symbol);

            if (index == default && symbol != ' ')
            {
                errorOffset++;
                continue;
            }

            if (symbol == ' ')
                continue;

            SetTile((position.x + i - errorOffset, position.y), new(index, tint), mask);
        }
    }
    /// <summary>
    /// Sets a rectangle of text with optional alignment, scrolling, and word wrapping.
    /// </summary>
    /// <param name="area">The rectangle.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="tint">Optional tint color value (defaults to white).</param>
    /// <param name="isWordWrapping">Optional flag for enabling word wrapping.</param>
    /// <param name="alignment">Optional text alignment.</param>
    /// <param name="scrollProgress">Optional scrolling value (between 0 and 1).</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetTextRectangle(
        Area area,
        string? text,
        uint tint = uint.MaxValue,
        bool isWordWrapping = true,
        Alignment alignment = Alignment.TopLeft,
        float scrollProgress = 0,
        Area? mask = null)
    {
        if (string.IsNullOrEmpty(text) || area.Width <= 0 || area.Height <= 0)
            return;

        var x = area.X;
        var y = area.Y;
        var lineList = text.TrimEnd().Split(Environment.NewLine).ToList();

        if (lineList.Count == 0)
            return;

        for (var i = 0; i < lineList.Count; i++)
        {
            var line = lineList[i];

            if (line.Length <= area.Width) // line is valid length
                continue;

            var lastLineIndex = area.Width - 1;
            var newLineIndex = isWordWrapping ?
                GetSafeNewLineIndex(line, (uint)lastLineIndex) :
                lastLineIndex;

            // end of line? can't word wrap, proceed to symbol wrap
            if (newLineIndex == 0)
            {
                lineList[i] = line[..area.Width];
                lineList.Insert(i + 1, line[area.Width..line.Length]);
                continue;
            }

            // otherwise wordwrap
            var endIndex = newLineIndex + (isWordWrapping ? 0 : 1);
            lineList[i] = line[..endIndex].TrimStart();
            lineList.Insert(i + 1, line[(newLineIndex + 1)..line.Length]);
        }

        var yDiff = area.Height - lineList.Count;

        if (alignment is Alignment.Left or Alignment.Center or Alignment.Right)
            for (var i = 0; i < yDiff / 2; i++)
                lineList.Insert(0, string.Empty);
        else if (alignment is Alignment.BottomLeft or Alignment.Bottom or Alignment.BottomRight)
            for (var i = 0; i < yDiff; i++)
                lineList.Insert(0, string.Empty);

        // new lineList.Count
        yDiff = area.Height - lineList.Count;

        var startIndex = 0;
        var end = area.Height;
        var scrollValue = (int)Math.Round(scrollProgress * (lineList.Count - area.Height));

        if (yDiff < 0)
        {
            startIndex += scrollValue;
            end += scrollValue;
        }

        var e = lineList.Count - area.Height;
        startIndex = Math.Clamp(startIndex, 0, Math.Max(e, 0));
        end = Math.Clamp(end, 0, lineList.Count);

        for (var i = startIndex; i < end; i++)
        {
            var line = lineList[i];

            if (alignment is Alignment.TopRight or Alignment.Right or Alignment.BottomRight)
                line = line.PadLeft(area.Width);
            else if (alignment is Alignment.Top or Alignment.Center or Alignment.Bottom)
                line = PadLeftAndRight(line, area.Width);

            SetTextLine((x, y), line, tint, mask: mask);
            NewLine();
        }

        return;

        void NewLine()
        {
            x = area.X;
            y++;
        }
        int GetSafeNewLineIndex(string line, uint endLineIndex)
        {
            for (var i = (int)endLineIndex; i >= 0; i--)
                if (line[i] == ' ' && i <= area.Width)
                    return i;

            return default;
        }
    }
    /// <summary>
    /// Sets the tint of the tiles in a rectangular area of the tilemap to 
    /// highlight a specific text (if found).
    /// </summary>
    /// <param name="area">The rectangle.</param>
    /// <param name="text">The text to search for and highlight.</param>
    /// <param name="tint">The color to tint the matching tiles.</param>
    /// <param name="isMatchingWord">Whether to only match the text 
    /// as a whole word or any symbols.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetTextRectangleTint(
        Area area,
        string? text,
        uint tint = uint.MaxValue,
        bool isMatchingWord = false,
        Area? mask = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var xStep = area.Width < 0 ? -1 : 1;
        var yStep = area.Height < 0 ? -1 : 1;
        var tileList = TileIdsFrom(text).ToList();

        for (var x = area.X; x != area.X + area.Width; x += xStep)
            for (var y = area.Y; y != area.Y + area.Height; y += yStep)
            {
                if (tileList[0] != TileAt((x, y)).Id)
                    continue;

                var correctSymbolCount = 0;
                var curX = x;
                var curY = y;
                var startPos = (x - 1, y);

                for (var i = 0; i < text.Length; i++)
                {
                    if (tileList[i] != TileAt((curX, curY)).Id)
                        break;

                    correctSymbolCount++;
                    curX++;

                    // try new line
                    if (curX <= x + area.Width)
                        continue;

                    curX = area.X;
                    curY++;
                }

                var endPos = (curX, curY);
                var left = TileAt(startPos).Id == 0 || curX == area.X;
                var right = TileAt(endPos).Id == 0 || curX == area.X + area.Width;
                var isWord = left && right;

                if (isWord ^ isMatchingWord)
                    continue;

                if (text.Length != correctSymbolCount)
                    continue;

                curX = x;
                curY = y;
                for (var i = 0; i < text.Length; i++)
                {
                    if (curX > x + area.Width) // try new line
                    {
                        curX = area.X;
                        curY++;
                    }

                    SetTile((curX, curY), new(TileAt((curX, curY)).Id, tint), mask);
                    curX++;
                }
            }
    }
    public void SetEllipse(
        (int x, int y) center,
        (int width, int height) radius,
        bool isFilled,
        Area? mask = null,
        params Tile[]? tiles)
    {
        if (tiles == null || tiles.Length == 0)
            return;

        var sqrRx = radius.width * radius.width;
        var sqrRy = radius.height * radius.height;
        var x = 0;
        var y = radius.height;
        var px = 0;
        var py = sqrRx * 2 * y;

        // Region 1
        var p = (int)(sqrRy - sqrRx * radius.height + 0.25f * sqrRx);
        while (px < py)
        {
            Set();

            x++;
            px += sqrRy * 2;

            if (p < 0)
                p += sqrRy + px;
            else
            {
                y--;
                py -= sqrRx * 2;
                p += sqrRy + px - py;
            }
        }

        // Region 2
        p = (int)(sqrRy * (x + 0.5f) * (x + 0.5f) + sqrRx * (y - 1) * (y - 1) - sqrRx * sqrRy);
        while (y >= 0)
        {
            Set();

            y--;
            py -= sqrRx * 2;

            if (p > 0)
                p += sqrRx - py;
            else
            {
                x++;
                px += sqrRy * 2;
                p += sqrRx - py + px;
            }
        }

        return;

        void Set()
        {
            var c = center;
            var o = tiles.Length == 1;
            if (isFilled == false)
            {
                SetTile((c.x + x, c.y - y), o ? tiles[0] : ChooseOne(tiles, ToSeed((c.x + x, c.y - y))),
                    mask);
                SetTile((c.x - x, c.y - y), o ? tiles[0] : ChooseOne(tiles, ToSeed((c.x - x, c.y - y))),
                    mask);
                SetTile((c.x - x, c.y + y), o ? tiles[0] : ChooseOne(tiles, ToSeed((c.x - x, c.y + y))),
                    mask);
                SetTile((c.x + x, c.y + y), o ? tiles[0] : ChooseOne(tiles, ToSeed((c.x + x, c.y + y))),
                    mask);
                return;
            }

            for (var i = c.x - x; i <= c.x + x; i++)
            {
                SetTile((i, c.y - y), o ? tiles[0] : ChooseOne(tiles, ToSeed((i, c.y - y))), mask);
                SetTile((i, c.y + y), o ? tiles[0] : ChooseOne(tiles, ToSeed((i, c.y + y))), mask);
            }
        }
    }

    public void SetLine(
        (int x, int y) pointA,
        (int x, int y) pointB,
        Area? mask = null,
        params Tile[]? tiles)
    {
        if (tiles == null || tiles.Length == 0)
            return;

        var (x0, y0) = pointA;
        var (x1, y1) = pointB;
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            var tile = tiles.Length == 1 ? tiles[0] : ChooseOne(tiles, ToSeed((x0, y0)));
            SetTile((x0, y0), tile, mask);

            if (x0 == x1 && y0 == y1)
                break;

            var e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
    /// <summary>
    /// Sets the tiles in a rectangular area of the tilemap to create a box with corners, borders
    /// and filling.
    /// </summary>
    /// <param name="area">The area of the rectangular box.</param>
    /// <param name="tileFill">The tile to use for the filling of the box.</param>
    /// <param name="borderTileId">The identifier of the tile to use for the 
    /// straight edges of the box.</param>
    /// <param name="cornerTileId">The identifier of the tile to use for the corners of the box.</param>
    /// <param name="borderTint">The color to tint the border tiles.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetBox(
        Area area,
        Tile tileFill,
        int cornerTileId,
        int borderTileId,
        uint borderTint = uint.MaxValue,
        Area? mask = null)
    {
        var (x, y) = (area.X, area.Y);
        var (w, h) = (area.Width, area.Height);

        if (w <= 0 || h <= 0)
            return;

        if (w == 1 || h == 1)
        {
            SetArea(area, mask, tileFill);
            return;
        }

        SetTile((x, y), new(cornerTileId, borderTint), mask);
        SetArea((x + 1, y, w - 2, 1), mask, new Tile(borderTileId, borderTint));
        SetTile((x + w - 1, y), new(cornerTileId, borderTint, 1), mask);

        if (h != 2)
        {
            SetArea((x, y + 1, 1, h - 2), mask, new Tile(borderTileId, borderTint, 3));

            if (tileFill.Id != Tile.SHADE_TRANSPARENT)
                SetArea((x + 1, y + 1, w - 2, h - 2), mask, tileFill);

            SetArea((x + w - 1, y + 1, 1, h - 2), mask, new Tile(borderTileId, borderTint, 1));
        }

        SetTile((x, y + h - 1), new(cornerTileId, borderTint, 3), mask);
        SetTile((x + w - 1, y + h - 1), new(cornerTileId, borderTint, 2), mask);
        SetArea((x + 1, y + h - 1, w - 2, 1), mask, new Tile(borderTileId, borderTint, 2));
    }
    /// <summary>
    /// Sets the tiles in a rectangular area of the tilemap to create a vertical or horizontal bar.
    /// </summary>
    /// <param name="position">The position of the top-left corner of the rectangular area 
    /// to create the bar.</param>
    /// <param name="tileIdEdge">The identifier of the tile to use for the edges of the bar.</param>
    /// <param name="tileId">The identifier of the tile to use for the 
    /// straight part of the bar.</param>
    /// <param name="tint">The color to tint the bar tiles.</param>
    /// <param name="size">The length of the bar in tiles.</param>
    /// <param name="isVertical">Whether the bar should be vertical or horizontal.</param>
    /// <param name="mask">An optional mask that skips any tile outside of it.</param>
    public void SetBar(
        (int x, int y) position,
        int tileIdEdge,
        int tileId,
        uint tint = uint.MaxValue,
        int size = 5,
        bool isVertical = false,
        Area? mask = null)
    {
        var (x, y) = position;
        var off = size == 1 ? 0 : 1;

        if (isVertical)
        {
            if (size > 1)
            {
                SetTile(position, new(tileIdEdge, tint, 1), mask);
                SetTile((x, y + size - 1), new(tileIdEdge, tint, 3), mask);
            }

            if (size != 2)
                SetArea((x, y + off, 1, size - 2), mask, new Tile(tileId, tint, 1));

            return;
        }

        if (size > 1)
        {
            SetTile(position, new(tileIdEdge, tint), mask);
            SetTile((x + size - 1, y), new(tileIdEdge, tint, 2), mask);
        }

        if (size != 2)
            SetArea((x + off, y, size - 2, 1), mask, new Tile(tileId, tint));
    }
    public void SetAutoTile(Area area, Tile[] rule, Tile substitute, Area? mask = null)
    {
        if (rule is not { Length: 9 })
            return;

        var changes = new Dictionary<(int x, int y), Tile>();

        for (var i = area.X; i < area.Height; i++)
            for (var j = area.Y; j < area.Width; j++)
            {
                var isMatch = true;

                for (var k = 0; k < rule.Length; k++)
                {
                    var (ox, oy) = (k % 3 - 1, k / 3 - 1);
                    var offTile = TileAt((j + ox, i + oy));

                    if (offTile.Id == rule[k].Id || rule[k] < 0)
                        continue;

                    isMatch = false;
                    break;
                }

                if (isMatch)
                    changes.Add((j, i), substitute);
            }

        foreach (var kvp in changes)
            SetTile(kvp.Key, kvp.Value, mask);
    }

    /// <summary>
    /// Configures the tile identifiers for text characters and numbers assuming they are sequential.
    /// </summary>
    /// <param name="lowercase">The tile identifier for the lowercase 'a' character.</param>
    /// <param name="uppercase">The tile identifier for the uppercase 'A' character.</param>
    /// <param name="numbers">The tile identifier for the '0' character.</param>
    public void ConfigureText(
        int lowercase = Tile.LOWERCASE_A,
        int uppercase = Tile.UPPERCASE_A,
        int numbers = Tile.NUMBER_0)
    {
        textIdLowercase = lowercase;
        textIdUppercase = uppercase;
        textIdNumbers = numbers;
    }

    /// <summary>
    /// Configures the tile identifiers for a set of symbols sequentially.
    /// </summary>
    /// <param name="symbols">The string of symbols to configure.</param>
    /// <param name="startId">The starting tile identifier for the symbols.</param>
    public void ConfigureText(string symbols, int startId)
    {
        for (var i = 0; i < symbols.Length; i++)
            symbolMap[symbols[i]] = startId + i;
    }

    /// <summary>
    /// Checks if a position is overlapping with the tilemap.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns>True if the position is overlapping with the tilemap, false otherwise.</returns>
    public bool IsOverlapping((int x, int y) position)
    {
        return position is { x: >= 0, y: >= 0 } &&
               position.x <= Size.width - 1 &&
               position.y <= Size.height - 1;
    }

    /// <summary>
    /// Converts a symbol to its corresponding tile identifier.
    /// </summary>
    /// <param name="symbol">The symbol to convert.</param>
    /// <returns>The tile identifier corresponding to the given symbol.</returns>
    public int TileIdFrom(char symbol)
    {
        var id = default(int);
        if (symbol is >= 'A' and <= 'Z')
            id = symbol - 'A' + textIdUppercase;
        else if (symbol is >= 'a' and <= 'z')
            id = symbol - 'a' + textIdLowercase;
        else if (symbol is >= '0' and <= '9')
            id = symbol - '0' + textIdNumbers;
        else if (symbolMap.TryGetValue(symbol, out var value))
            id = value;

        return id;
    }
    /// <summary>
    /// Converts a text to an array of tile identifiers.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>An array of tile identifiers corresponding to the given symbols.</returns>
    public int[] TileIdsFrom(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<int>();

        var result = new int[text.Length];
        for (var i = 0; i < text.Length; i++)
            result[i] = TileIdFrom(text[i]);

        return result;
    }

    public Tilemap Duplicate()
    {
        return new(ToBytes());
    }

    /// <summary>
    /// Implicitly converts a 2D array of tiles to a tilemap object.
    /// </summary>
    /// <param name="data">The 2D array of tiles to convert.</param>
    /// <returns>A new tilemap object containing the given tiles.</returns>
    public static implicit operator Tilemap(Tile[,] data)
    {
        return new(data);
    }
    /// <summary>
    /// Implicitly converts a tilemap object to a 2D array of tiles.
    /// </summary>
    /// <param name="tilemap">The tilemap object to convert.</param>
    /// <returns>A new 2D array of tiles containing the tiles from the tilemap object.</returns>
    public static implicit operator Tile[,](Tilemap tilemap)
    {
        return Duplicate(tilemap.data);
    }
    /// <summary>
    /// Implicitly converts a tilemap object to a 2D array of tile bundles.
    /// </summary>
    /// <param name="tilemap">The tilemap object to convert.</param>
    /// <returns>A new 2D array of tile bundles containing the tiles from the tilemap object.</returns>
    public static implicit operator (int id, uint tint, sbyte turns, bool isMirrored, bool isFlipped)[,](
        Tilemap tilemap)
    {
        return tilemap.ToBundle();
    }
    public static implicit operator Area(Tilemap tilemap)
    {
        return (0, 0, tilemap.Size.width, tilemap.Size.height);
    }
    public static implicit operator int[,](Tilemap tilemap)
    {
        return tilemap.ids;
    }
    public static implicit operator byte[](Tilemap tilemap)
    {
        return tilemap.ToBytes();
    }
    public static implicit operator Tilemap(byte[] bytes)
    {
        return new(bytes);
    }

#region Backend
    private int textIdNumbers = Tile.NUMBER_0,
        textIdUppercase = Tile.UPPERCASE_A,
        textIdLowercase = Tile.LOWERCASE_A;
    private readonly Dictionary<char, int> symbolMap = new()
    {
        { '░', 2 }, { '▒', 5 }, { '▓', 7 }, { '█', 10 },

        { '⅛', 140 }, { '⅐', 141 }, { '⅙', 142 }, { '⅕', 143 }, { '¼', 144 },
        { '⅓', 145 }, { '⅜', 146 }, { '⅖', 147 }, { '½', 148 }, { '⅗', 149 },
        { '⅝', 150 }, { '⅔', 151 }, { '¾', 152 }, { '⅘', 153 }, { '⅚', 154 }, { '⅞', 155 },

        { '₀', 156 }, { '₁', 157 }, { '₂', 158 }, { '₃', 159 }, { '₄', 160 },
        { '₅', 161 }, { '₆', 162 }, { '₇', 163 }, { '₈', 164 }, { '₉', 165 },

        { '⁰', 169 }, { '¹', 170 }, { '²', 171 }, { '³', 172 }, { '⁴', 173 },
        { '⁵', 174 }, { '⁶', 175 }, { '⁷', 176 }, { '⁸', 177 }, { '⁹', 178 },

        { '+', 182 }, { '-', 183 }, { '×', 184 }, { '―', 185 }, { '÷', 186 }, { '%', 187 },
        { '=', 188 }, { '≠', 189 }, { '≈', 190 }, { '√', 191 }, { '∫', 193 }, { 'Σ', 194 },
        { 'ε', 195 }, { 'γ', 196 }, { 'ϕ', 197 }, { 'π', 198 }, { 'δ', 199 }, { '∞', 200 },
        { '≪', 204 }, { '≫', 205 }, { '≤', 206 }, { '≥', 207 }, { '<', 208 }, { '>', 209 },
        { '(', 210 }, { ')', 211 }, { '[', 212 }, { ']', 213 }, { '{', 214 }, { '}', 215 },
        { '⊥', 216 }, { '∥', 217 }, { '∠', 218 }, { '∟', 219 }, { '~', 220 }, { '°', 221 },
        { '℃', 222 }, { '℉', 223 }, { '*', 224 }, { '^', 225 }, { '#', 226 }, { '№', 227 },
        { '$', 228 }, { '€', 229 }, { '£', 230 }, { '¥', 231 }, { '¢', 232 }, { '¤', 233 },

        { '!', 234 }, { '?', 235 }, { '.', 236 }, { ',', 237 }, { '…', 238 },
        { ':', 239 }, { ';', 240 }, { '"', 241 }, { '\'', 242 }, { '`', 243 }, { '–', 244 },
        { '_', 245 }, { '|', 246 }, { '/', 247 }, { '\\', 248 }, { '@', 249 }, { '&', 250 },
        { '®', 251 }, { '℗', 252 }, { '©', 253 }, { '™', 254 },

        //{ '→', 282 }, { '↓', 283 }, { '←', 284 }, { '↑', 285 },
        //{ '⇨', 330 }, { '⇩', 331 }, { '⇦', 332 }, { '⇧', 333 },
        //{ '➡', 334 }, { '⬇', 335 }, { '⬅', 336 }, { '⬆', 337 },

        { '─', 260 }, { '┌', 261 }, { '├', 262 }, { '┼', 263 },
        { '═', 272 }, { '╔', 273 }, { '╠', 274 }, { '╬', 275 },

        { '♩', 357 }, { '♪', 358 }, { '♫', 359 }, { '♬', 360 }, { '♭', 361 }, { '♮', 362 },
        { '♯', 363 },

        { '★', 333 }, { '☆', 334 }, { '✓', 338 }, { '⏎', 339 },

        { '●', 423 }, { '○', 427 }, { '■', 417 }, { '□', 420 }, { '▲', 428 }, { '△', 430 },

        { '♟', 404 }, { '♜', 405 }, { '♞', 406 }, { '♝', 407 }, { '♛', 408 }, { '♚', 409 },
        { '♙', 410 }, { '♖', 411 }, { '♘', 412 }, { '♗', 413 }, { '♕', 414 }, { '♔', 415 },
        { '♠', 396 }, { '♥', 397 }, { '♣', 398 }, { '♦', 399 },
        { '♤', 400 }, { '♡', 401 }, { '♧', 402 }, { '♢', 403 },

        { '▕', 432 },
    };
    private static readonly Dictionary<int, Random> randomCache = new();

    private Tile[,] data;
    private (int, uint, sbyte, bool, bool)[,] bundleCache;
    private int[,] ids;

    public static (int, int) FromIndex(int index, (int width, int height) size)
    {
        index = index < 0 ? 0 : index;
        index = index > size.width * size.height - 1 ? size.width * size.height - 1 : index;

        return (index % size.width, index / size.width);
    }
    private bool IndicesAreValid((int x, int y) indices, Area? mask)
    {
        var (x, y) = indices;
        mask ??= new(0, 0, Size.width, Size.height);
        var (mx, my, mw, mh, _) = mask.Value.ToBundle();

        return x >= mx && y >= my && x < mx + mw && y < my + mh;
    }
    private static string PadLeftAndRight(string text, int length)
    {
        var spaces = length - text.Length;
        var padLeft = spaces / 2 + text.Length;
        return text.PadLeft(padLeft).PadRight(length);
    }
    private static T[,] Duplicate<T>(T[,] array)
    {
        var copy = new T[array.GetLength(0), array.GetLength(1)];
        Array.Copy(array, copy, array.Length);
        return copy;
    }
    private static float Random(float rangeA, float rangeB, float precision = 0, float seed = float.NaN)
    {
        if (rangeA > rangeB)
            (rangeA, rangeB) = (rangeB, rangeA);

        precision = (int)Limit(precision, 0, 5);
        precision = MathF.Pow(10, precision);

        rangeA *= precision;
        rangeB *= precision;

        var s = float.IsNaN(seed) ? Guid.NewGuid().GetHashCode() : (int)seed;
        Random random;

        if (randomCache.TryGetValue(s, out var r))
            random = r;
        else
        {
            random = new(s);
            randomCache[s] = random;
        }

        var randInt = random.Next((int)rangeA, Limit((int)rangeB, (int)rangeA, (int)rangeB) + 1);

        return randInt / precision;
    }
    private static float Limit(float number, float rangeA, float rangeB, bool isOverflowing = false)
    {
        if (rangeA > rangeB)
            (rangeA, rangeB) = (rangeB, rangeA);

        if (isOverflowing)
        {
            var d = rangeB - rangeA;
            return ((number - rangeA) % d + d) % d + rangeA;
        }
        else
        {
            if (number < rangeA)
                return rangeA;
            else if (number > rangeB)
                return rangeB;
            return number;
        }
    }
    private static int Limit(int number, int rangeA, int rangeB, bool isOverflowing = false)
    {
        return (int)Limit((float)number, rangeA, rangeB, isOverflowing);
    }
    private static T ChooseOne<T>(IList<T> collection, float seed)
    {
        return collection[(int)Random(0, collection.Count - 1, 0, seed)];
    }

    internal static byte[] Compress(byte[] data)
    {
        var output = new MemoryStream();
        using (var stream = new DeflateStream(output, CompressionLevel.Optimal))
            stream.Write(data, 0, data.Length);

        return output.ToArray();
    }
    internal static byte[] Decompress(byte[] data)
    {
        var input = new MemoryStream(data);
        var output = new MemoryStream();
        using (var stream = new DeflateStream(input, CompressionMode.Decompress))
            stream.CopyTo(output);

        return output.ToArray();
    }
    internal static byte[] GetBytesFrom(byte[] fromBytes, int amount, ref int offset)
    {
        var result = fromBytes[offset..(offset + amount)];
        offset += amount;
        return result;
    }

    private int ToSeed((int a, int b) parameters)
    {
        var (a, b) = parameters;
        var (x, y, z) = SeedOffset;
        return HashCode.Combine(a + x, b + y, z);
    }
#endregion
}