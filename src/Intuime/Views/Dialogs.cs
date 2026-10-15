using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Intuime.Views;

public enum SaveChoice
{
    Save,
    Discard,
    Cancel
}

public static class Dialogs
{
    public static Task Alert(Window owner, string title, string message) =>
        Show(owner, title, message, new[] { ("OK", true) }, null);

    public static async Task<bool> Confirm(Window owner, string title, string message)
    {
        var choice = 0;
        await Show(owner, title, message, new[] { ("Cancel", false), ("Delete", true) }, index => choice = index);
        return choice == 1;
    }

    public static async Task<SaveChoice> AskSave(Window owner, string name)
    {
        var choice = SaveChoice.Cancel;
        await Show(
            owner,
            "Save changes?",
            $"Save changes to {name} before continuing?",
            new[] { ("Cancel", false), ("Don't save", false), ("Save", true) },
            index => choice = index switch
            {
                1 => SaveChoice.Discard,
                2 => SaveChoice.Save,
                _ => SaveChoice.Cancel
            });
        return choice;
    }

    public static async Task<string?> Prompt(Window owner, string title, string label, string value)
    {
        string? result = null;
        var box = new TextBox { Text = value, Watermark = label };
        var window = Base(title, 440);
        var ok = Button("OK", true);
        var cancel = Button("Cancel", false);
        ok.Click += (_, _) =>
        {
            result = box.Text ?? "";
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();
        window.Content = Stack(label, box, Buttons(cancel, ok));
        await window.ShowDialog(owner);
        return result;
    }

    public static async Task<(string Name, int Width, int Height)?> NewExperience(Window owner)
    {
        (string Name, int Width, int Height)? result = null;
        var name = new TextBox { Text = "Untitled experience" };
        var presets = new ComboBox
        {
            ItemsSource = new[] { "1920 × 1080", "1080 × 1920", "3840 × 2160", "1280 × 720", "1024 × 768" },
            SelectedIndex = 0
        };
        var window = Base("New experience", 440);
        var ok = Button("Create", true);
        var cancel = Button("Cancel", false);
        ok.Click += (_, _) =>
        {
            var size = (presets.SelectedIndex) switch
            {
                1 => (1080, 1920),
                2 => (3840, 2160),
                3 => (1280, 720),
                4 => (1024, 768),
                _ => (1920, 1080)
            };
            result = (string.IsNullOrWhiteSpace(name.Text) ? "Untitled experience" : name.Text.Trim(), size.Item1, size.Item2);
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();
        window.Content = Stack("Name", name, "Size", presets, Buttons(cancel, ok));
        await window.ShowDialog(owner);
        return result;
    }

    private static async Task Show(Window owner, string title, string message, (string Label, bool Accent)[] buttons, Action<int>? chosen)
    {
        var window = Base(title, 460);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        for (var index = 0; index < buttons.Length; index++)
        {
            var captured = index;
            var button = Button(buttons[index].Label, buttons[index].Accent);
            button.Click += (_, _) =>
            {
                chosen?.Invoke(captured);
                window.Close();
            };
            row.Children.Add(button);
        }
        window.Content = Stack(message, row);
        await window.ShowDialog(owner);
    }

    private static Window Base(string title, double width) => new()
    {
        Title = title,
        Width = width,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false,
        Background = Painting.Brush(Painting.Panel),
        Padding = new Thickness(0)
    };

    private static StackPanel Stack(params object[] items)
    {
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        foreach (var item in items)
        {
            switch (item)
            {
                case string text:
                    panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Painting.Brush(Painting.Text) });
                    break;
                case Control control:
                    panel.Children.Add(control);
                    break;
            }
        }
        return panel;
    }

    private static StackPanel Buttons(params Button[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        foreach (var button in buttons)
            row.Children.Add(button);
        return row;
    }

    private static Button Button(string label, bool accent) => new()
    {
        Content = label,
        Classes = { accent ? "accent" : "tool" },
        MinWidth = 88
    };
}
