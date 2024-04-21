namespace Pure.Examples.ExamplesUserInterface;

public static class Layouts
{
    public static Block[] Create(TilemapPack maps)
    {
        Window.Title = "Pure - Layouts Example";

        var panel = new Panel { Size = (21, 21) };
        panel.AlignInside((0.9f, 0.5f));

        //============

        var nl = Environment.NewLine;
        var layoutElements = new Block[] { new Button(), new Button(), new Slider(), new InputBox() };
        var layoutFull = new Layout { Size = (20, 20) };
        layoutFull.Cut(index: 0, side: Side.Right, rate: 0.4f);
        layoutFull.Cut(index: 0, side: Side.Bottom, rate: 0.3f);
        layoutFull.Cut(index: 2, side: Side.Top, rate: 0.5f);
        layoutFull.Cut(index: 1, side: Side.Top, rate: 0.4f);
        layoutFull.OnDisplay(() =>
        {
            layoutFull.Size = (
                25 + (int)(Math.Sin(Time.Clock / 2) * 10),
                20 + (int)(Math.Cos(Time.Clock / 2) * 3));
            layoutFull.AlignInside((0.9f, 0.5f));
        });
        layoutFull.OnDisplaySegment((segment, index) =>
        {
            if (index == 0)
            {
                maps.SetLayoutSegment(segment, index, false);
                var text = $"- Useful for containing structured elements{nl}{nl}" +
                           $"- Can be dynamically resized without losing its ratios";
                text = text.Constrain((segment.width, segment.height));
                maps[1].SetText((segment.x, segment.y), text);
                return;
            }

            var e = layoutElements[index - 1];
            e.Position = (segment.x, segment.y);
            e.Size = (segment.width, segment.height);

            if (e is Button button)
                maps.SetButton(button);
            else if (e is Slider slider)
                maps.SetSlider(slider);
            else if (e is InputBox inputBox)
                maps.SetInputBox(inputBox);
        });

        //============

        var layoutEmpty = new Layout();
        layoutEmpty.AlignInside((0.05f, 0.5f));
        layoutEmpty.Cut(index: 0, side: Side.Right, rate: 0.4f);
        layoutEmpty.Cut(index: 0, side: Side.Bottom, rate: 0.6f);
        layoutEmpty.Cut(index: 1, side: Side.Top, rate: 0.25f);
        layoutEmpty.Cut(index: 1, side: Side.Bottom, rate: 0.4f);
        layoutEmpty.OnDisplaySegment((segment, index) =>
            maps.SetLayoutSegment(segment, index, true));

        var elements = new List<Block>
        {
            layoutEmpty,
            layoutFull
        };
        elements.AddRange(layoutElements);
        return elements.ToArray();
    }
}