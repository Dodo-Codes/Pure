namespace Pure.Editors.EditorMap;

internal class Inspector : Panel
{
	public readonly List layers;
	public readonly Palette paletteColor;
	public readonly Scroll paletteScrollV, paletteScrollH;
	public readonly Pages tools, edits;

	public Inspector(Editor editor, TilePalette tilePalette)
	{
		this.editor = editor;

		var (w, h) = (16, editor.MapsUi.Size.height);
		Text = "";
		Size = (w, h);
		IsMovable = false;
		IsResizable = false;

		OnDisplay(() => editor.MapsUi.SetPanel(this));
		Align((1f, 0.5f));

		//========

		layers = new(itemCount: 1) { ItemSize = (13, 1) };
		layers.OnDisplay(() => editor.MapsUi.SetList(layers));
		layers.OnItemDisplay(item => editor.MapsUi.SetListItem(layers, item));
		layers[0].Text = "Layer1";

		var create = new Button();
		create.OnInteraction(Interaction.Trigger, LayerCreate);
		create.OnDisplay(() =>
			editor.MapsUi.SetButtonIcon(create, new(Tile.CURSOR_CROSSHAIR, Color.Gray), 1));

		var remove = new Button();
		remove.OnInteraction(Interaction.Trigger, LayersRemove);
		remove.OnUpdate(() => ShowWhenLayerSelected(remove));
		remove.OnDisplay(() =>
			editor.MapsUi.SetButtonIcon(remove, new(Tile.ICON_DELETE, Color.Gray), 1));

		var rename = new InputBox { Value = "", Placeholder = "Rename…", IsSingleLine = true };
		rename.OnSubmit(() =>
		{
			LayersRename(rename.Value);
			rename.Value = "";
		});
		rename.OnUpdate(() => ShowWhenLayerSelected(rename));
		rename.OnDisplay(() => editor.MapsUi.SetInputBox(rename));

		var up = new Button();
		up.OnInteraction(Interaction.Trigger, LayersUp);
		up.OnUpdate(() => ShowWhenLayerSelected(up));
		up.OnDisplay(() => editor.MapsUi.SetButtonIcon(up, new(Tile.ARROW, Color.Gray, 3), 1));

		var down = new Button();
		down.OnInteraction(Interaction.Trigger, LayersDown);
		down.OnUpdate(() => ShowWhenLayerSelected(down));
		down.OnDisplay(() => editor.MapsUi.SetButtonIcon(down, new(Tile.ARROW, Color.Gray, 1), 1));

		//========

		tools = new(count: 8) { ItemWidth = 1, ItemGap = 0 };
		tools.OnItemDisplay(item =>
		{
			var graphics = new[]
			{
				Tile.SHAPE_SQUARE_SMALL_HOLLOW, Tile.GAME_DICE_6, Tile.SHAPE_SQUARE,
				Tile.PUNCTUATION_SLASH, Tile.SHAPE_CIRCLE, Tile.SHAPE_CIRCLE_HOLLOW,
				Tile.ICON_GRID, Tile.ICON_FILL
			};
			var id = graphics[tools.IndexOf(item)];
			editor.MapsUi.SetButtonIcon(item, new(id, item.IsSelected ? Color.Green : Color.Gray), 1);
		});
		tools.OnItemInteraction(Interaction.Trigger, item =>
		{
			var logs = new[]
			{
				"Group of tiles", "Single random tile", "Rectangle of random tiles",
				"Line of random tiles", "Filled ellipse of random tiles", "Hollow ellipse of random tiles",
				"Replace all tiles of a kind with random tiles", "Fill with random tiles"
			};
			editor.DisplayInfoText(logs[tools.IndexOf(item)]);
		});
		edits = new(count: 4) { ItemWidth = 1, ItemGap = 0 };
		edits.OnItemDisplay(item =>
		{
			var graphics = new[] { Tile.ICON_LOOP, Tile.ICON_MIRROR, Tile.ICON_FLIP, Tile.ICON_PALETTE };
			var id = graphics[edits.IndexOf(item)];
			editor.MapsUi.SetButtonIcon(item, new(id, item.IsSelected ? Color.Green : Color.Gray), 1);
		});
		edits.OnItemInteraction(Interaction.Trigger, item =>
		{
			var logs = new[]
			{
				"Rotate rectangle of tiles", "Mirror rectangle of tiles", "Flip rectangle of tiles",
				"Color rectangle of tiles"
			};
			editor.DisplayInfoText(logs[edits.IndexOf(item)]);
		});

		paletteColor = new();
		paletteColor.OnDisplay(() =>
		{
			editor.MapsUi.SetPalette(paletteColor, zOrder: 1);
			editor.MapsUi.SetSlider(paletteColor.Opacity, zOrder: 1);
			editor.MapsUi.SetPages(paletteColor.Brightness, zOrder: 1);
		});
		paletteColor.OnColorSampleDisplay((btn, color) =>
		{
			editor.MapsUi[1].SetTile(btn.Position, new Tile(Tile.SHADE_OPAQUE, color));
		});
		paletteColor.Brightness.OnItemDisplay(btn =>
			editor.MapsUi.SetPagesItem(paletteColor.Brightness, btn));

		paletteScrollH = new(isVertical: false) { Size = (14, 1) };
		paletteScrollV = new(isVertical: true) { Size = (1, 14) };
		paletteScrollH.OnDisplay(() => editor.MapsUi.SetScroll(paletteScrollH));
		paletteScrollV.OnDisplay(() => editor.MapsUi.SetScroll(paletteScrollV));

		//========

		var inspectorItems = new Block?[]
		{
			layers, create, null, remove, up, down, null, rename, null,
			paletteScrollV, paletteScrollH, null, paletteColor, null,
			tools, null, null, null, edits
		};
		var layout = new Layout((Position.x + 1, Position.y + 1)) { Size = (w - 2, h - 2) };
		layout.OnDisplaySegment((segment, i) =>
			UpdateInspectorItem(i, inspectorItems, segment, tilePalette));

		layout.Cut(0, Side.Bottom, 0.65f); // layers
		layout.Cut(1, Side.Bottom, 0.95f); // create
		layout.Cut(1, Side.Right, 0.9f); // empty
		layout.Cut(3, Side.Right, 0.9f); // remove
		layout.Cut(4, Side.Right, 0.9f); // up
		layout.Cut(5, Side.Right, 0.9f); // down
		layout.Cut(2, Side.Top, 0.05f); // rename

		layout.Cut(2, Side.Bottom, 0.52f); // tileset
		layout.Cut(8, Side.Right, 0.05f); // scroll V
		layout.Cut(8, Side.Bottom, 0.05f); // scroll H
		layout.Cut(2, Side.Bottom, 0.3f); // empty

		layout.Cut(2, Side.Bottom, 0.5f); // colors
		layout.Cut(2, Side.Bottom, 0.02f); // tools text
		layout.Cut(13, Side.Right, 0.6f); // tools
		layout.Cut(11, Side.Bottom, 0.1f); // hover info
		layout.Cut(12, Side.Top, 0.3f); // empty
		layout.Cut(2, Side.Bottom, 0.3f); // edit text
		layout.Cut(17, Side.Right, 0.6f); // edit

		editor.Ui.Add(this, layout, create, up, down, rename, remove,
			tools, edits, paletteColor, paletteScrollV, paletteScrollH, layers);
	}

	#region Backend
	private readonly Editor editor;

	private void LayerCreate()
	{
		var item = new Button { Text = "NewLayer" };
		var size = editor.MapsEditor.Size;
		var isEmpty = editor.MapsEditor.Count == 0;
		size = isEmpty ? (50, 50) : size;

		layers.Add(item);
		editor.MapsEditor.Add(new Tilemap(size));

		if(isEmpty)
			editor.MapsEditor.ViewSize = (50, 50);
	}
	private void LayersRename(string name)
	{
		var selected = layers.ItemsSelected;
		foreach(var item in selected)
			item.Text = name;
	}
	private void LayersRemove()
	{
		var selected = layers.ItemsSelected;
		foreach(var item in selected)
		{
			var index = layers.IndexOf(item);
			editor.MapsEditor.Remove(editor.MapsEditor[index]);
			layers.Remove(item);
		}
	}
	private void LayersUp()
	{
		var selected = layers.ItemsSelected;
		var maps = new Tilemap[selected.Length];
		for(var i = 0; i < selected.Length; i++)
			maps[i] = editor.MapsEditor[layers.IndexOf(selected[i])];

		layers.Shift(-1, layers.ItemsSelected);
		editor.MapsEditor.Shift(-1, maps);
	}
	private void LayersDown()
	{
		var selected = layers.ItemsSelected;
		var maps = new Tilemap[selected.Length];
		for(var i = 0; i < selected.Length; i++)
			maps[i] = editor.MapsEditor[layers.IndexOf(selected[i])];

		layers.Shift(1, layers.ItemsSelected);
		editor.MapsEditor.Shift(1, maps);
	}

	private void UpdateInspectorItem(
		int i,
		Block?[] inspectorItems,
		(int x, int y, int width, int height) segment,
		TilePalette palette)
	{
		//editor.MapsUi.SetLayoutSegment(segment, i, true, 5);

		if(i == 13)
		{
			editor.MapsUi[(int)Editor.LayerMapsUi.Front].SetTextLine(
				position: (segment.x, segment.y),
				text: "Tool:");
			return;
		}
		if(i == 17)
		{
			editor.MapsUi[(int)Editor.LayerMapsUi.Front].SetTextLine(
				position: (segment.x, segment.y),
				text: "Edit:");
			return;
		}

		if(i >= inspectorItems.Length)
			return;

		if(i == 15 && Mouse.IsHovering(palette.layer) && editor.Prompt.IsHidden)
		{
			var (mx, my) = palette.mousePos;
			var index = new Indices(my, mx).ToIndex(palette.map.Size.width);
			editor.MapsUi[(int)Editor.LayerMapsUi.Front].SetTextRectangle(
				position: (segment.x, segment.y),
				size: (segment.width, segment.height),
				text: $"{index} ({mx} {my})");
			return;
		}

		var item = inspectorItems[i];
		if(item == null)
			return;

		item.Position = (segment.x, segment.y);
		item.Size = (segment.width, segment.height);
	}

	private void ShowWhenLayerSelected(Block block)
	{
		block.IsHidden = layers.ItemsSelected.Length == 0;
		block.IsDisabled = block.IsHidden;
	}
	#endregion
}