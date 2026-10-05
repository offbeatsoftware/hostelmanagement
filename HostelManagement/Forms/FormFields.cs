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
