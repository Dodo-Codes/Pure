namespace Pure.Examples.ExamplesSystems;

using Engine.Window;
using Engine.Tilemap;
using Engine.Utilities;

public static class DefaultGraphics
{
    public static void Run()
    {
        var tilemap = new Tilemap((16 * 3, 9 * 3));

        Window.Create();

        while (Window.IsOpen)
        {
            Window.Activate(true);

            tilemap.Flush();

            for (var i = 0; i < 26; i++)
                for (var j = 0; j < 26; j++)
                    tilemap.SetTile((j, i), new Indices(i, j).ToIndex(26));

            var (x, y) = Mouse.PixelToWorld(Mouse.CursorPosition);
            var id = tilemap.TileAt(((int)x, (int)y)).Id;
            tilemap.SetTextLine((27, 13), $"{id}");

            Window.DrawTiles(tilemap.ToBundle());
            Window.Activate(false);
        }
    }
}