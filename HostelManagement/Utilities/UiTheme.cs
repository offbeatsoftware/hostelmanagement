namespace HostelManagement.Utilities;

/// <summary>
/// Shared fonts, colours and control styling. Every screen should use these
/// helpers instead of setting colours and fonts individually.
/// </summary>
public static class UiTheme
{
    // Fonts
    public static readonly Font BodyFont = new("Segoe UI", 9.75f);
    public static readonly Font BodyBoldFont = new("Segoe UI", 9.75f, FontStyle.Bold);
    public static readonly Font HeadingFont = new("Segoe UI Semibold", 15f);
    public static readonly Font SubHeadingFont = new("Segoe UI", 9.75f);
    public static readonly Font NavFont = new("Segoe UI", 10f);
    public static readonly Font NavGroupFont = new("Segoe UI", 8f, FontStyle.Bold);
    public static readonly Font BrandFont = new("Segoe UI Semibold", 12f);

    // Colours
    public static readonly Color NavBackground = Color.FromArgb(33, 47, 61);
    public static readonly Color NavText = Color.FromArgb(220, 226, 232);
    public static readonly Color NavGroupText = Color.FromArgb(140, 155, 170);
    public static readonly Color NavHover = Color.FromArgb(47, 64, 80);
    public static readonly Color NavSelected = Color.FromArgb(0, 102, 170);

    public static readonly Color HeaderBackground = Color.White;
    public static readonly Color ContentBackground = Color.FromArgb(244, 246, 248);
    public static readonly Color TextPrimary = Color.FromArgb(33, 37, 41);
    public static readonly Color TextMuted = Color.FromArgb(108, 117, 125);
    public static readonly Color Border = Color.FromArgb(222, 226, 230);

    public static readonly Color Primary = Color.FromArgb(0, 102, 170);
    public static readonly Color Danger = Color.FromArgb(192, 57, 43);
    public static readonly Color Success = Color.FromArgb(25, 135, 84);

    // Standard sizes
    public static readonly Size ButtonSize = new(110, 34);

    /// <summary>Main action on a screen (Save, Add).</summary>
    public static void StylePrimaryButton(Button button) => StyleButton(button, Primary, Color.White);

    /// <summary>Secondary action (Cancel, Clear, Refresh).</summary>
    public static void StyleSecondaryButton(Button button)
    {
        StyleButton(button, Color.White, TextPrimary);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Border;
    }

    /// <summary>Destructive action (Delete, Check-out). Always pair with Dialogs.Confirm.</summary>
    public static void StyleDangerButton(Button button) => StyleButton(button, Danger, Color.White);

    /// <summary>Standard read only list grid used on every listing screen.</summary>
    public static void StyleGrid(DataGridView grid)
    {
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Border;
        grid.Font = BodyFont;
        grid.RowTemplate.Height = 28;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 32;
        grid.ColumnHeadersDefaultCellStyle.BackColor = ContentBackground;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = BodyBoldFont;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ContentBackground;

        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(209, 231, 245);
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 252);
    }

    private static void StyleButton(Button button, Color back, Color fore)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = back;
        button.ForeColor = fore;
        button.Font = BodyFont;
        button.Size = ButtonSize;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }
}
