namespace Pure.Examples.ExamplesUserInterface;

public static class ButtonsAndCheckboxes
{
    public static Block[] Create(TilemapPack maps)
    {
        Window.Title = "Pure - Buttons & Checkboxes Example";

        var buttonSelect = new Button { Text = "Button Select" };
        buttonSelect.Size = (buttonSelect.Text.Length + 4, 1);
        buttonSelect.Align((0.5f, 0.2f));
        buttonSelect.OnDisplay(() => maps.SetButtonSelect(buttonSelect));

        // ==============

        var counter = 0;
        var button = new Button { Text = "Cool Button" };
        button.Size = (button.Text.Length + 2, 3);
        button.Align((0.5f, 0.4f));
        button.OnInteraction(Interaction.Trigger, () => counter++);
        button.OnDisplay(() =>
        {
            maps.SetButton(button);
            maps[1].SetTextLine((0, 0), $"The {button.Text} was pressed {counter} times.");
        });

        // ==============

        var checkbox = new Button { Text = "Checkbox" };
        checkbox.Size = (checkbox.Text.Length + 2, 1);
        checkbox.Align((0.5f, 0.6f));
        checkbox.OnDisplay(() => maps.SetCheckbox(checkbox));

        // ==============

        var buttonDisabled = new Button { IsDisabled = true, Text = "Disabled Button" };
        buttonDisabled.Size = (buttonDisabled.Text.Length, 1);
        buttonDisabled.Align((0.5f, 0.8f));
        buttonDisabled.OnDisplay(() =>
        {
            maps[0].SetTextLine(buttonDisabled.Position, buttonDisabled.Text,
                Color.Gray.ToDark(0.7f));
        });

        return new Block[] { buttonSelect, button, checkbox, buttonDisabled };
    }
}