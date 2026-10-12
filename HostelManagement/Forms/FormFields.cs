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
        // A label must stay on one line ("Room number *" wrapped its asterisk): widen the column when needed.
        int needed = TextRenderer.MeasureText(label.Text, label.Font).Width + label.Margin.Horizontal + 4;
        if (table.ColumnStyles[0] is { SizeType: SizeType.Absolute } column && column.Width < needed)
        {
            column.Width = needed;
        }
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

    /// <summary>White bordered section with a bold heading, used to group a screen into parts.</summary>
    public static Panel CreateCard(string title, Control body)
    {
        var card = new Panel
        {
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16, 8, 16, 12),
        };
        var heading = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Text = title,
        };
        card.Controls.Add(body);
        card.Controls.Add(heading);
        return card;
    }

    /// <summary>
    /// A card like <see cref="CreateCard"/> whose height follows its content (for cards docked at the top), so
    /// nothing is cut off when the text is longer or the window narrower. The body should size itself
    /// (AutoSize), for example a table from <see cref="CreateTable"/>.
    /// </summary>
    public static TableLayoutPanel CreateAutoCard(string title, Control body)
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16, 8, 16, 12),
            Dock = DockStyle.Top,
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var heading = new Label
        {
            AutoSize = true,
            Font = UiTheme.BodyBoldFont,
            ForeColor = UiTheme.TextPrimary,
            Margin = new Padding(0, 6, 0, 10),
            Text = title,
        };
        card.RowCount = 2;
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.Controls.Add(heading, 0, 0);
        card.Controls.Add(body, 0, 1);
        return card;
    }

    /// <summary>Controls stacked from top to bottom, as tall as their content.</summary>
    public static FlowLayoutPanel CreateStack(params Control[] controls)
    {
        var stack = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        stack.Controls.AddRange(controls);
        return stack;
    }

    /// <summary>Horizontal row of buttons and other controls (search box, filters, messages).</summary>
    public static FlowLayoutPanel CreateButtonRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Height = 46,
            Padding = new Padding(0, 6, 0, 6),
            Margin = Padding.Empty,
        };
        foreach (Control control in controls)
        {
            if (control is Button)
            {
                control.Margin = new Padding(0, 0, 8, 0);
            }
            row.Controls.Add(control);
        }
        return row;
    }

    /// <summary>Adds a column bound to a model property. Pass a format such as "C2" for money.</summary>
    public static DataGridViewTextBoxColumn AddGridColumn(DataGridView grid, string property, string header,
        int fillWeight, string? format = null, bool alignRight = false)
    {
        var column = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property,
            Name = property,
            HeaderText = header,
            FillWeight = fillWeight,
        };
        if (format is not null)
        {
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.FormatProvider = Money.Culture;
        }
        if (alignRight)
        {
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        grid.Columns.Add(column);
        return column;
    }
}
