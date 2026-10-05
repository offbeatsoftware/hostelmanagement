using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Builds the standard two column "label: input" layout used on entry screens.
/// Required fields are marked with an asterisk.
/// </summary>
public static class FormFields
{
    public static TableLayoutPanel CreateTable(int labelWidth = 130, int inputWidth = 420)
    {
        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = Padding.Empty,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, inputWidth));
        return table;
    }

    public static TextBox AddTextBox(TableLayoutPanel table, string caption, int maxLength,
        bool required = false, bool multiline = false)
    {
        var textBox = new TextBox
        {
            Font = UiTheme.BodyFont,
            MaxLength = maxLength,
            Dock = DockStyle.Fill,
            Multiline = multiline,
            Height = multiline ? 60 : 27,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            Margin = new Padding(0, 4, 0, 4),
            TabIndex = table.Controls.Count,
        };
        AddRow(table, caption, textBox, required);
        return textBox;
    }

    public static void AddRow(TableLayoutPanel table, string caption, Control input, bool required = false)
    {
        var label = new Label
        {
            Text = required ? caption + " *" : caption,
            Font = UiTheme.BodyFont,
            ForeColor = UiTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 4),
        };
        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(label, 0, table.RowCount - 1);
        table.Controls.Add(input, 1, table.RowCount - 1);
    }

    /// <summary>Inline message label for validation errors and "Saved" confirmations (avoids popups).</summary>
    public static Label CreateMessageLabel() => new()
    {
        AutoSize = true,
        Font = UiTheme.BodyFont,
        Margin = new Padding(12, 9, 0, 0),
    };

    public static void ShowError(Label label, string message)
    {
        label.ForeColor = UiTheme.Danger;
        label.Text = message;
    }

    public static void ShowSuccess(Label label, string message)
    {
        label.ForeColor = UiTheme.Success;
        label.Text = message;
    }
}
