namespace Pure.Editors.EditorUserInterface;

internal class MenuMain : Menu
{
    public MenuMain() : base(
        "Block� ",
        "  Add",
        "------ ",
        "Scene� ",
        "  Save",
        "  Load")
    {
        Size = (6, 6);

        OnItemInteraction(Interaction.Trigger, item =>
        {
            IsHidden = true;

            var index = IndexOf(item);
            if (index == 1)
                menus[MenuType.Add].Show(Position);
            else if (index == 3)
            {
                // saveLoad.IsSelectingFolders = true;
                // editUI.PromptOpen("Select folder:", saveLoad, 2, btnIndex =>
                // {
                // 	if(btnIndex == 0)
                // 	{
                // 		var directory = saveLoad.SelectedPaths.Length == 0
                // 			? saveLoad.CurrentDirectory
                // 			: saveLoad.SelectedPaths[0];
                // 		editUI.PromptClose();
                // 		editUI.PromptOpen("Enter file name:", fileName, 2, btnIndex2 =>
                // 		{
                // 			if(btnIndex2 == 0)
                // 			{
                // 				var bytes = ui.ToBytes();
                // 				File.WriteAllBytes(Path.Join(directory, fileName.Value), bytes);
                // 			}
                //
                // 			editUI.PromptClose();
                // 		});
                // 		return;
                // 	}
                //
                // 	editUI.PromptClose();
                // });
            }
            else if (index == 4)
            {
                // load
            }
        });
    }

    public override void Update()
    {
        base.Update();

        if (Mouse.IsButtonPressed(Mouse.Button.Right).Once("onRMB") == false)
            return;

        var (x, y) = MousePosition;

        foreach (var kvp in menus)
            kvp.Value.IsHidden = true;

        Position = ((int)x + 1, (int)y + 1);
        IsHidden = false;
    }
}