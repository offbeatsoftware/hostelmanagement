using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms;

/// <summary>
/// Sign Agreement: the student, the parent or guardian, the hostel owner and a witness sign on screen (client
/// decision). The owner's signature can be kept for the hostel and is then filled in on every agreement.
/// DialogResult.OK means the signatures were saved; <see cref="CreatePdf"/> asks for the agreement PDF next.
/// </summary>
public sealed class AgreementSignForm : Form
{
    private readonly int _studentId;
    private readonly int _hostelId;
    private readonly Dictionary<AgreementSigner, SignaturePad> _pads = [];
    private readonly CheckBox _keepOwnerBox;
    private readonly TextBox _witnessNameBox;
    private readonly Label _messageLabel;

    public AgreementSignForm(Student student)
    {
        _studentId = student.StudentId;
        _hostelId = student.HostelId;

        Text = $"Sign Agreement: {student.StudentName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Font = UiTheme.BodyFont;
        BackColor = Color.White;
        ClientSize = new Size(800, 600);

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 0, 0, 8),
            ForeColor = UiTheme.TextMuted,
            Text = "Each person signs in their box with the mouse, touchpad, pen or a touch screen. Clear starts a box " +
                   "again. The signatures are printed on the agreement PDF above the names, with today's date.",
        };

        _keepOwnerBox = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Use for every agreement of this hostel",
            Margin = new Padding(0, 4, 0, 0),
        };
        _witnessNameBox = new TextBox { Width = 250, MaxLength = 100, PlaceholderText = "Witness name", Margin = new Padding(0, 2, 0, 0) };

        string parent = student.FatherName.Length > 0 ? student.FatherName : student.MotherName;
        var boxes = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, AutoSize = true, Margin = Padding.Empty };
        boxes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 384));
        boxes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 384));
        boxes.Controls.Add(SignBox(AgreementSigner.Student, $"Second Party (Student): {student.StudentName}", null), 0, 0);
        boxes.Controls.Add(SignBox(AgreementSigner.Parent,
            "Parent / Guardian" + (parent.Length > 0 ? $": {parent}" : string.Empty), null), 1, 0);
        boxes.Controls.Add(SignBox(AgreementSigner.Owner, "First Party (Hostel owner)", _keepOwnerBox), 0, 1);
        boxes.Controls.Add(SignBox(AgreementSigner.Witness, "Witness", _witnessNameBox), 1, 1);

        _messageLabel = FormFields.CreateMessageLabel();
        _messageLabel.MaximumSize = new Size(380, 0);
        var saveButton = new Button { Text = "Save" };
        UiTheme.StyleSecondaryButton(saveButton);
        saveButton.Click += (_, _) => Save(createPdf: false);
        var savePdfButton = new Button { Text = "Save and Make PDF" };
        UiTheme.StylePrimaryButton(savePdfButton);
        savePdfButton.Width = 170;
        savePdfButton.Click += (_, _) => Save(createPdf: true);
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        UiTheme.StyleSecondaryButton(cancelButton);
        CancelButton = cancelButton;
        FlowLayoutPanel buttons = FormFields.CreateButtonRow(savePdfButton, saveButton, cancelButton, _messageLabel);

        FlowLayoutPanel stack = FormFields.CreateStack(intro, boxes, buttons);
        stack.Location = new Point(16, 14);
        Controls.Add(stack);

        Shown += (_, _) => ShowSaved();
    }

    /// <summary>True when the admin chose Save and Make PDF.</summary>
    public bool CreatePdf { get; private set; }

    private Control SignBox(AgreementSigner signer, string title, Control? extra)
    {
        var titleLabel = new Label
        {
            AutoSize = true,
            AutoEllipsis = false,
            MaximumSize = new Size(370, 0),
            Font = UiTheme.BodyBoldFont,
            Margin = new Padding(0, 6, 0, 4),
            Text = title,
        };
        var pad = new SignaturePad { Size = new Size(370, 150), Font = UiTheme.BodyFont, Margin = new Padding(0) };
        pad.InkChanged += (_, _) => _messageLabel.Text = string.Empty;
        _pads[signer] = pad;

        var clearButton = new Button { Text = "Clear" };
        UiTheme.StyleSecondaryButton(clearButton);
        clearButton.Width = 80;
        clearButton.Click += (_, _) => pad.Clear();

        FlowLayoutPanel row = extra is null
            ? FormFields.CreateButtonRow(clearButton)
            : FormFields.CreateButtonRow(clearButton, extra);
        return FormFields.CreateStack(titleLabel, pad, row);
    }

    private void ShowSaved()
    {
        try
        {
            AgreementSignatures saved = AgreementSignatureService.Get(_studentId, _hostelId);
            foreach ((AgreementSigner signer, SignaturePad pad) in _pads)
            {
                using Image? image = AgreementSignatureService.Load(saved.For(signer));
                pad.ShowSignature(image);
            }
            _witnessNameBox.Text = saved.WitnessName;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The saved signatures could not be shown.");
        }
    }

    private void Save(bool createPdf)
    {
        var signatures = _pads.ToDictionary(p => p.Key, p => (Image?)p.Value.GetSignature());
        try
        {
            AgreementSignatureService.Save(_studentId, _hostelId, signatures, _witnessNameBox.Text, DateTime.Today,
                _keepOwnerBox.Checked);
            CreatePdf = createPdf;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException ex)
        {
            FormFields.ShowError(_messageLabel, ex.Message);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The signatures could not be saved.");
        }
        finally
        {
            foreach (Image? image in signatures.Values)
            {
                image?.Dispose();
            }
        }
    }
}
