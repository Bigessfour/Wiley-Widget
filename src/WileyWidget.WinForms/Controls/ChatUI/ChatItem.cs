using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;
using Syncfusion.Drawing;

namespace WileyWidget.WinForms.Controls.ChatUI;

/// <summary>
/// Individual chat message bubble with automatic sizing based on content length.
/// Based on winforms-chat reference but simplified for text-only messages.
/// Supports incoming (left-aligned) and outgoing (right-aligned) message styles.
/// </summary>
public sealed class ChatItem : UserControl
{
    private readonly GradientPanelExt _authorPanel;
    private readonly Label _authorLabel;
    private readonly GradientPanelExt _bodyPanel;
    private readonly TextBoxExt _bodyTextBox;

    private bool _isIncoming = true;
    private string _message = string.Empty;
    private string _author = "System";
    private DateTime _timestamp = DateTime.Now;

    public ChatItem()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(12, 6, 12, 6);
        Margin = new Padding(0, 0, 0, 4);

        // Author panel (bottom, contains timestamp)
        _authorPanel = new GradientPanelExt
        {
            Dock = DockStyle.Bottom,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Height = 30,
            Padding = new Padding(0, 6, 0, 0),
            BorderStyle = BorderStyle.None
        };

        _authorLabel = new Label
        {
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            Text = "System - " + DateTime.Now.ToShortTimeString(),
            AutoSize = true,
#pragma warning disable WW0001 // Semantic color for UI element visibility
            ForeColor = Color.FromArgb(100, 100, 100)
#pragma warning restore WW0001
        };

        _authorPanel.Controls.Add(_authorLabel);

        // Body panel (contains message text)
        _bodyPanel = new GradientPanelExt
        {
            Dock = DockStyle.Left,
            Padding = new Padding(10, 8, 10, 8),
            Width = 424,
            Height = 46,
            BorderStyle = BorderStyle.FixedSingle,
#pragma warning disable WW0001 // Semantic color for message bubble border
            BorderColor = Color.FromArgb(210, 210, 210),
#pragma warning restore WW0001
            BorderSingle = ButtonBorderStyle.Solid
        };

        _bodyTextBox = new TextBoxExt
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI Emoji", 9F),
            Location = new Point(6, 6),
            Multiline = true,
            ReadOnly = true,
            Text = "No message",
            WordWrap = true
        };

        _bodyPanel.Controls.Add(_bodyTextBox);

        // Add panels to control
        Controls.Add(_bodyPanel);
        Controls.Add(_authorPanel);

        UpdateAppearance();
    }

    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public bool IsIncoming
    {
        get => _isIncoming;
        set
        {
            _isIncoming = value;
            UpdateAppearance();
        }
    }

    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string Message
    {
        get => _message;
        set
        {
            _message = value ?? string.Empty;
            _bodyTextBox.Text = _message;
            UpdateAppearance();
        }
    }

    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public string Author
    {
        get => _author;
        set
        {
            _author = value ?? "System";
            UpdateAppearance();
        }
    }

    [Browsable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            _timestamp = value;
            UpdateAppearance();
        }
    }

    /// <summary>
    /// Resize the message bubble to fit within the specified maximum width.
    /// Auto-calculates height based on text content and wrapping.
    /// </summary>
    /// <param name="maxWidth">Maximum width for the message bubble (typically 60% of parent width)</param>
    public void ResizeBubbles(int maxWidth)
    {
        UpdateMaxBubbleWidth(maxWidth);
    }

    /// <summary>
    /// Internal method to resize bubbles (kept for compatibility).
    /// </summary>
    internal void UpdateMaxBubbleWidth(int maxWidth)
    {
        if (maxWidth <= 0)
            return;

        SuspendLayout();

        try
        {
            var body = _message;
            if (string.IsNullOrWhiteSpace(body))
            {
                body = "No message";
            }

            using var gfx = CreateGraphics();

            // Set minimum size to prevent invisible bubbles
            _bodyPanel.MinimumSize = new Size(100, 40);

            // Calculate panel width (account for padding)
            var panelPadding = _bodyPanel.Padding.Horizontal;
            var availableWidth = maxWidth - panelPadding;

            // Measure text with proper wrapping
            var textSize = gfx.MeasureString(body, _bodyTextBox.Font, availableWidth);
            var textWidth = (int)Math.Ceiling(textSize.Width);
            var textHeight = (int)Math.Ceiling(textSize.Height);

            // Determine optimal panel width
            if (textWidth < availableWidth)
            {
                // Text fits comfortably - size panel to text width
                _bodyPanel.Width = Math.Max(100, textWidth + panelPadding + 10);
            }
            else
            {
                // Text needs wrapping - use max width
                _bodyPanel.Width = maxWidth;
            }

            // Calculate required heights
            var minTextHeight = _bodyTextBox.Font.Height + 12;
            var requiredTextHeight = Math.Max(minTextHeight, textHeight + 12);

            // Set body panel height
            _bodyPanel.Height = requiredTextHeight;

            // Set total control height
            Height = _bodyPanel.Height + _authorPanel.Height + Padding.Vertical + Margin.Vertical;
        }
        finally
        {
            ResumeLayout();
        }
    }

    private void UpdateAppearance()
    {
#pragma warning disable WW0001 // Semantic colors for message bubble styling - incoming/outgoing distinction
        // Update alignment and styling based on message direction
        if (_isIncoming)
        {
            _bodyPanel.Dock = DockStyle.Left;
            _authorLabel.Dock = DockStyle.Left;
            _bodyTextBox.TextAlign = HorizontalAlignment.Left;

            // Incoming messages: light blue background
            _bodyPanel.BackColor = Color.FromArgb(232, 240, 254);
            _bodyTextBox.BackColor = Color.FromArgb(232, 240, 254);
            _bodyTextBox.ForeColor = Color.FromArgb(33, 33, 33);
        }
        else
        {
            _bodyPanel.Dock = DockStyle.Right;
            _authorLabel.Dock = DockStyle.Right;
            _bodyTextBox.TextAlign = HorizontalAlignment.Right;

            // Outgoing messages: light gray background
            _bodyPanel.BackColor = Color.FromArgb(245, 245, 245);
            _bodyTextBox.BackColor = Color.FromArgb(245, 245, 245);
            _bodyTextBox.ForeColor = Color.FromArgb(33, 33, 33);
        }
#pragma warning restore WW0001

        // Update author label with timestamp
        if (_timestamp > DateTime.Today)
        {
            _authorLabel.Text = $"{_author}, {_timestamp.ToShortTimeString()}";
        }
        else
        {
            _authorLabel.Text = $"{_author}, {_timestamp.ToShortDateString()}";
        }

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _authorPanel?.Dispose();
            _authorLabel?.Dispose();
            _bodyPanel?.Dispose();
            _bodyTextBox?.Dispose();
        }

        base.Dispose(disposing);
    }
}
