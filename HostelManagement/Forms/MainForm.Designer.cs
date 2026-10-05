namespace HostelManagement.Forms;

partial class MainForm
{
    /// <summary>Required designer variable.</summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>Clean up any resources being used.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        navPanel = new Panel();
        navButtonsPanel = new FlowLayoutPanel();
        brandPanel = new Panel();
        brandLabel = new Label();
        headerPanel = new Panel();
        pageDescriptionLabel = new Label();
        pageTitleLabel = new Label();
        contentPanel = new Panel();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        dateStatusLabel = new ToolStripStatusLabel();
        navPanel.SuspendLayout();
        brandPanel.SuspendLayout();
        headerPanel.SuspendLayout();
        statusStrip.SuspendLayout();
        SuspendLayout();
        //
        // navPanel
        //
        navPanel.Controls.Add(navButtonsPanel);
        navPanel.Controls.Add(brandPanel);
        navPanel.Dock = DockStyle.Left;
        navPanel.Location = new Point(0, 0);
        navPanel.Name = "navPanel";
        navPanel.Size = new Size(230, 676);
        navPanel.TabIndex = 0;
        //
        // navButtonsPanel
        //
        navButtonsPanel.AutoScroll = true;
        navButtonsPanel.Dock = DockStyle.Fill;
        navButtonsPanel.FlowDirection = FlowDirection.TopDown;
        navButtonsPanel.Location = new Point(0, 60);
        navButtonsPanel.Name = "navButtonsPanel";
        navButtonsPanel.Padding = new Padding(0, 4, 0, 8);
        navButtonsPanel.Size = new Size(230, 616);
        navButtonsPanel.TabIndex = 1;
        navButtonsPanel.WrapContents = false;
        //
        // brandPanel
        //
        brandPanel.Controls.Add(brandLabel);
        brandPanel.Dock = DockStyle.Top;
        brandPanel.Location = new Point(0, 0);
        brandPanel.Name = "brandPanel";
        brandPanel.Size = new Size(230, 60);
        brandPanel.TabIndex = 0;
        //
        // brandLabel
        //
        brandLabel.Dock = DockStyle.Fill;
        brandLabel.Location = new Point(0, 0);
        brandLabel.Name = "brandLabel";
        brandLabel.Padding = new Padding(16, 0, 0, 0);
        brandLabel.Size = new Size(230, 60);
        brandLabel.TabIndex = 0;
        brandLabel.Text = "Hostel Management";
        brandLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // headerPanel
        //
        headerPanel.Controls.Add(pageDescriptionLabel);
        headerPanel.Controls.Add(pageTitleLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Location = new Point(230, 0);
        headerPanel.Name = "headerPanel";
        headerPanel.Padding = new Padding(20, 10, 20, 8);
        headerPanel.Size = new Size(954, 70);
        headerPanel.TabIndex = 1;
        //
        // pageDescriptionLabel
        //
        pageDescriptionLabel.Dock = DockStyle.Top;
        pageDescriptionLabel.Location = new Point(20, 40);
        pageDescriptionLabel.Name = "pageDescriptionLabel";
        pageDescriptionLabel.Size = new Size(914, 22);
        pageDescriptionLabel.TabIndex = 1;
        //
        // pageTitleLabel
        //
        pageTitleLabel.Dock = DockStyle.Top;
        pageTitleLabel.Location = new Point(20, 10);
        pageTitleLabel.Name = "pageTitleLabel";
        pageTitleLabel.Size = new Size(914, 30);
        pageTitleLabel.TabIndex = 0;
        //
        // contentPanel
        //
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(230, 70);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(20);
        contentPanel.Size = new Size(954, 606);
        contentPanel.TabIndex = 2;
        //
        // statusStrip
        //
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, dateStatusLabel });
        statusStrip.Location = new Point(0, 676);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(1184, 25);
        statusStrip.SizingGrip = false;
        statusStrip.TabIndex = 3;
        //
        // statusLabel
        //
        statusLabel.Name = "statusLabel";
        statusLabel.Spring = true;
        statusLabel.Text = "Ready";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // dateStatusLabel
        //
        dateStatusLabel.Name = "dateStatusLabel";
        dateStatusLabel.Text = "";
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 17F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1184, 701);
        Controls.Add(contentPanel);
        Controls.Add(headerPanel);
        Controls.Add(navPanel);
        Controls.Add(statusStrip);
        MinimumSize = new Size(1000, 640);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Hostel Management System";
        WindowState = FormWindowState.Maximized;
        navPanel.ResumeLayout(false);
        brandPanel.ResumeLayout(false);
        headerPanel.ResumeLayout(false);
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Panel navPanel;
    private FlowLayoutPanel navButtonsPanel;
    private Panel brandPanel;
    private Label brandLabel;
    private Panel headerPanel;
    private Label pageTitleLabel;
    private Label pageDescriptionLabel;
    private Panel contentPanel;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
    private ToolStripStatusLabel dateStatusLabel;
}
