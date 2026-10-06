using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>Edit the agreement text. {Fields} are filled in for each student; "# " starts the title and "## " a heading.</summary>
public sealed class AgreementTextForm : Form
{
    private readonly TextBox _textBox;
    private readonly Label _messageLabel;

    public AgreementTextForm()
    {
        Text = "Agreement Text";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(900, 640);
        MinimumSize = new Size(700, 500);
        Padding = new Padding(16);

        var help = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            ForeColor = UiTheme.TextMuted,
            Text = "Each line is a paragraph. A line starting with \"# \" is the title and \"## \" a heading. " +
                   "These fields are filled in for each student (in bold): " +
                   string.Join("  ", AgreementService.Fields.Select(f => "{" + f + "}")),
        };

        _textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 9.75f),
        };

        var saveButton = new Button { Text = "Save" };
        UiTheme.StylePrimaryButton(saveButton);
        saveButton.Click += (_, _) => Save();

        var restoreButton = new Button { Text = "Restore Default" };
        UiTheme.StyleSecondaryButton(restoreButton);
        restoreButton.Width = 150;
        restoreButton.Click += (_, _) => _textBox.Text = AgreementService.DefaultTemplate.ReplaceLineEndings("\r\n");

        var cancelButton = new Button { Text = "Cancel" };
        UiTheme.StyleSecondaryButton(cancelButton);
        cancelButton.DialogResult = DialogResult.Cancel;
        CancelButton = cancelButton;

        _messageLabel = FormFields.CreateMessageLabel();
        FlowLayoutPanel buttons = FormFields.CreateButtonRow(saveButton, restoreButton, cancelButton, _messageLabel);
        buttons.Dock = DockStyle.Bottom;

        Controls.Add(_textBox);
        Controls.Add(help);
        Controls.Add(buttons);

        Load += (_, _) =>
        {
            try
            {
                _textBox.Text = AgreementService.GetTemplate().ReplaceLineEndings("\r\n");
                _textBox.Select(0, 0);
            }
            catch (Exception ex)
            {
                ErrorHandler.Handle(ex, "The agreement text could not be loaded.");
            }
        };
    }

    private void Save()
    {
        try
        {
            AgreementService.SaveTemplate(_textBox.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The agreement text could not be saved.");
        }
    }
}
