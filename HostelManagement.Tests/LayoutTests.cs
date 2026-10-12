using HostelManagement.Forms;
using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

/// <summary>
/// Opens every screen (in a window the size of a 1366 x 768 laptop screen), every tab and every dialog, and checks
/// that no button, box or text is cut off, overlaps another control or has text that does not fit.
/// </summary>
public sealed class LayoutTests : TestDatabase
{
    private static readonly Size LaptopWindow = new(1366, 728);

    /// <summary>Students, rooms, fees, payments and attendance, so lists and labels show real content.</summary>
    private (Invoice Invoice, Payment Payment, Room Room) SeedData()
    {
        HostelService.Save(new Hostel { HostelId = HostelId, HostelName = "Shri Balaji Boys Hostel", Phone = "0141 2222222" });
        Room room = AddRoom("101", beds: 3);
        int aman = AddStudentWithParents("Aman Sharma", motherEmail: "sunita@example.com");
        Invoice invoice = CheckInWithFee(aman, room, 50_000m, 12_000m);
        Payment payment = Pay(invoice.InvoiceId, 10_000m, YearStart);
        CheckInWithFee(AddStudentWithParents("Ravi Kumar"), room, 45_000m);
        AddStudentWithParents("Karan Mehta");
        HostelContext.Select(HostelId);
        return (InvoiceService.GetInvoice(invoice.InvoiceId)!, payment, room);
    }

    [Fact]
    public void EveryScreen_FitsWithoutCutOffOrOverlappingControls()
    {
        SeedData();
        var problems = new List<string>();

        MainFormTests.RunOnStaThread(() =>
        {
            using var form = new MainForm { WindowState = FormWindowState.Normal };
            form.Show();
            form.Size = LaptopWindow;
            Application.DoEvents();

            Control menu = form.Controls.Find("navButtonsPanel", searchAllChildren: true).Single();
            foreach (Button button in menu.Controls.OfType<Button>().ToList())
            {
                button.PerformClick();
                Application.DoEvents();
                problems.AddRange(LayoutChecker.Check(form, button.Text));
            }
            form.Close();
        });

        Assert.True(problems.Count == 0, "Layout problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void EveryDialog_FitsWithoutCutOffOrOverlappingControls()
    {
        (Invoice invoice, Payment payment, Room room) = SeedData();
        Hostel hostel = HostelService.GetHostel(HostelId)!;
        List<College> colleges = CollegeService.Search(HostelId);
        Student student = StudentService.GetStudents(HostelId).First();
        RoomAllocation allocation = AllocationService.GetAllocations(HostelId).First();
        List<Student> withoutRoom = AllocationService.GetStudentsWithoutRoom(HostelId);
        List<SharingType> sharingTypes = RoomService.GetSharingTypes(HostelId);
        PaymentService.Delete(Pay(invoice.InvoiceId, 500m).PaymentId, "Entered twice");
        List<PaymentChange> changes = PaymentService.GetChanges(HostelId);
        List<Invoice> pending = PaymentService.GetInvoicesWithPending(HostelId);
        var problems = new List<string>();

        MainFormTests.RunOnStaThread(() =>
        {
            var dialogs = new List<(string Name, Func<Form> Create)>
            {
                ("Sign in", () => new LoginForm(HostelService.GetHostels())),
                ("Add hostel", () => new HostelEditForm()),
                ("Edit hostel", () => new HostelEditForm(hostel)),
                ("Add college", () => new CollegeEditForm(HostelId)),
                ("Add room", () => new RoomEditForm(HostelId, sharingTypes)),
                ("Edit room", () => new RoomEditForm(HostelId, sharingTypes, room)),
                ("Add student", () => new StudentEditForm(colleges)),
                ("Edit student", () => new StudentEditForm(colleges, student)),
                ("Check-in", () => new AllocationForm(AllocationAction.CheckIn, HostelId, withoutRoom)),
                ("Transfer", () => new AllocationForm(AllocationAction.Transfer, HostelId, current: allocation)),
                ("Check-out", () => new AllocationForm(AllocationAction.CheckOut, HostelId, current: allocation)),
                ("Agreement text", () => new AgreementTextForm()),
                ("Edit fee", () => new FeeEditForm(invoice)),
                ("New year fees", () => new NewYearFeesForm(hostel)),
                ("Record payment", () => new PaymentForm(pending, invoice.InvoiceId)),
                ("Edit payment", () => new PaymentEditForm(payment, invoice)),
                ("Delete payment", () => new PaymentDeleteForm(payment)),
                ("Payment history", () => new PaymentHistoryForm("Payment change history", changes)),
                ("Take photo", () => new CameraForm()),
            };

            foreach ((string name, Func<Form> create) in dialogs)
            {
                using Form form = create();
                form.Show();
                Application.DoEvents();
                problems.AddRange(LayoutChecker.Check(form, name));
                form.Close();
                Application.DoEvents();
            }
        });

        Assert.True(problems.Count == 0, "Layout problems:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }
}

/// <summary>Finds controls that are cut off by their containers, overlap each other or cannot show their text.</summary>
internal static class LayoutChecker
{
    private const int Tolerance = 2;

    public static List<string> Check(Form form, string screen)
    {
        var problems = new List<string>();
        CheckContainer(form, form, screen, problems);
        return problems.Distinct().ToList();
    }

    private static void CheckContainer(Form form, Control container, string screen, List<string> problems)
    {
        if (container is TabControl tabs)
        {
            // Only the selected page is laid out: check every page in turn.
            int selected = tabs.SelectedIndex;
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                tabs.SelectedIndex = i;
                Application.DoEvents();
                CheckContainer(form, tabs.TabPages[i], $"{screen} > {tabs.TabPages[i].Text}", problems);
            }
            tabs.SelectedIndex = selected;
            return;
        }

        List<Control> visible = container.Controls.Cast<Control>().Where(c => c.Visible).ToList();
        foreach (Control control in visible)
        {
            if (CutOffBy(form, control) is Control clip)
            {
                problems.Add($"{screen}: {Describe(control)} {control.Size.Width}x{control.Size.Height} in " +
                             $"{Describe(control.Parent!)} is cut off by {Describe(clip)} {clip.ClientSize.Width}x{clip.ClientSize.Height}");
            }
            if (TextDoesNotFit(control) is string text)
            {
                problems.Add($"{screen}: {Describe(control)} cannot show its text ({text})");
            }
            if (WrapsUnexpectedly(control))
            {
                problems.Add($"{screen}: {Describe(control)} breaks onto a second line (its column is too narrow)");
            }
            if (control is ListBox list && WidestItemDoesNotFit(list) is string item)
            {
                problems.Add($"{screen}: list {Describe(list)} cuts off '{item}'");
            }
        }

        List<Control> leaves = visible.Where(IsLeaf).ToList();
        for (int i = 0; i < leaves.Count; i++)
        {
            for (int j = i + 1; j < leaves.Count; j++)
            {
                Rectangle overlap = Rectangle.Intersect(leaves[i].Bounds, leaves[j].Bounds);
                if (overlap.Width > Tolerance && overlap.Height > Tolerance)
                {
                    problems.Add($"{screen}: {Describe(leaves[i])} overlaps {Describe(leaves[j])}");
                }
            }
        }

        foreach (Control child in visible.Where(c => !IsOpaque(c)))
        {
            CheckContainer(form, child, screen, problems);
        }
    }

    /// <summary>Controls whose inside is drawn by Windows or by the control itself (not checked further).</summary>
    private static bool IsOpaque(Control control) =>
        control is DataGridView or ComboBox or NumericUpDown or DateTimePicker or TextBoxBase or ListBox or PictureBox
            or UpDownBase or HostelManagement.Forms.Views.PaymentsChart;

    private static bool IsLeaf(Control control) =>
        control is Label or ButtonBase or TextBoxBase or ComboBox or DateTimePicker or NumericUpDown or PictureBox;

    /// <summary>The container that hides part of the control (and cannot scroll to it), else null.</summary>
    private static Control? CutOffBy(Form form, Control control)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return null;
        }
        Rectangle bounds = form.RectangleToClient(control.Parent!.RectangleToScreen(control.Bounds));
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ScrollableControl { AutoScroll: true } || parent is TabPage { AutoScroll: true })
            {
                return null;
            }
            Rectangle visible = form.RectangleToClient(parent.RectangleToScreen(parent.ClientRectangle));
            if (bounds.Right > visible.Right + Tolerance || bounds.Bottom > visible.Bottom + Tolerance ||
                bounds.Left < visible.Left - Tolerance || bounds.Top < visible.Top - Tolerance)
            {
                return parent;
            }
            if (parent == form)
            {
                break;
            }
        }
        return null;
    }

    /// <summary>The text of a button, label or tick box that is wider or taller than the control, else null.</summary>
    private static string? TextDoesNotFit(Control control)
    {
        string text = control.Text;
        if (text.Length == 0 || control.Width <= 0)
        {
            return null;
        }
        switch (control)
        {
            case Button button:
            {
                Size size = TextRenderer.MeasureText(text, button.Font);
                return size.Width + 8 > button.Width || size.Height + 4 > button.Height ? text : null;
            }
            case CheckBox or RadioButton when !control.AutoSize:
            {
                Size size = TextRenderer.MeasureText(text, control.Font);
                return size.Width + 20 > control.Width ? text : null;
            }
            // An AutoSize label sizes itself (it can only be cut off by its panel, checked above).
            case Label label when !label.AutoSize:
            {
                int width = Math.Max(label.ClientSize.Width - label.Padding.Horizontal, 1);
                Size size = TextRenderer.MeasureText(text, label.Font, new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                bool ellipsis = label.AutoEllipsis;
                return !ellipsis && size.Height > label.ClientSize.Height - label.Padding.Vertical + Tolerance
                    ? Shorten(text)
                    : null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// A one line label (AutoSize, no MaximumSize, no line breaks) squeezed by a table cell wraps its text:
    /// "Room number *" showed its asterisk on a second line.
    /// </summary>
    private static bool WrapsUnexpectedly(Control control)
    {
        if (control is not Label { AutoSize: true } label || label.MaximumSize != Size.Empty ||
            label.Text.Length == 0 || label.Text.Contains('\n'))
        {
            return false;
        }
        // The label's own one line width, measured the way the label draws its text.
        return label.Width + Tolerance < label.GetPreferredSize(Size.Empty).Width;
    }

    /// <summary>The first item of a list that is wider than the list, else null.</summary>
    private static string? WidestItemDoesNotFit(ListBox list)
    {
        // Owner drawn lists (the report list) draw their text 8 pixels in.
        int indent = list.DrawMode == DrawMode.Normal ? 2 : 8;
        foreach (object item in list.Items)
        {
            string text = list.GetItemText(item);
            if (TextRenderer.MeasureText(text, list.Font).Width + indent > list.ClientSize.Width)
            {
                return text;
            }
        }
        return null;
    }

    private static string Describe(Control control)
    {
        string text = control.Text.Length > 0 ? $" '{Shorten(control.Text)}'" : "";
        string name = control.Name.Length > 0 ? $" {control.Name}" : "";
        return $"{control.GetType().Name}{name}{text}";
    }

    private static string Shorten(string text)
    {
        string oneLine = text.ReplaceLineEndings(" ");
        return oneLine.Length <= 50 ? oneLine : oneLine[..50] + "...";
    }
}
