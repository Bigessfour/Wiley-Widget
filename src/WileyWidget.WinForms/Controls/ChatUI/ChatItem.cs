using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Syncfusion.Drawing;
using Syncfusion.WinForms.Controls;
using Syncfusion.Windows.Forms.Tools;
using AppThemeColors = WileyWidget.WinForms.Themes.AppThemeColors;
using ThemeColors = WileyWidget.WinForms.Themes.ThemeColors;

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
                ForeColor = SystemColors.GrayText
        };

        _authorPanel.Controls.Add(_authorLabel);

        // Body panel (contains message text)
        _bodyPanel = new GradientPanelExt
        {
            Dock = DockStyle.Left,
            Padding = new Padding(10, 8, 10, 8),
            Height = 46,
            BorderStyle = BorderStyle.FixedSingle,
            BorderColor = SystemColors.ControlDark,
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

        // Inherit theme from parent; ensure Syncfusion styling cascades
        Syncfusion.WinForms.Controls.SfSkinManager.SetVisualStyle(this, ThemeColors.DefaultTheme);

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
        private void UpdateBubbleRegion()
        {
            if (_bodyPanel.Width <= 0 || _bodyPanel.Height <= 0)
                return;

            const int bubbleRadius = 18;
            const int tailRadius = 4;

            int topLeftRadius = bubbleRadius;
            int topRightRadius = bubbleRadius;
            int bottomRightRadius = _isIncoming ? bubbleRadius : tailRadius;
            int bottomLeftRadius = _isIncoming ? tailRadius : bubbleRadius;

            var bounds = _bodyPanel.ClientRectangle;
            var path = CreateBubblePath(bounds, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius);

            var oldRegion = _bodyPanel.Region;
            _bodyPanel.Region = new Region(path);
            oldRegion?.Dispose();
            path.Dispose();
        }

        private static GraphicsPath CreateBubblePath(Rectangle bounds, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            var path = new GraphicsPath();

            int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
            topLeft = Math.Max(0, Math.Min(topLeft, maxRadius));
            topRight = Math.Max(0, Math.Min(topRight, maxRadius));
            bottomRight = Math.Max(0, Math.Min(bottomRight, maxRadius));
            bottomLeft = Math.Max(0, Math.Min(bottomLeft, maxRadius));

            int left = bounds.Left;
            int top = bounds.Top;
            int right = bounds.Right;
            int bottom = bounds.Bottom;

            path.StartFigure();

            if (topLeft > 0)
                path.AddArc(left, top, topLeft * 2, topLeft * 2, 180, 90);
            else
                path.AddLine(left, top, left, top);

            path.AddLine(left + topLeft, top, right - topRight, top);

            if (topRight > 0)
                path.AddArc(right - topRight * 2, top, topRight * 2, topRight * 2, 270, 90);
            else
                path.AddLine(right, top, right, top);

            path.AddLine(right, top + topRight, right, bottom - bottomRight);

            if (bottomRight > 0)
                path.AddArc(right - bottomRight * 2, bottom - bottomRight * 2, bottomRight * 2, bottomRight * 2, 0, 90);
            else
                path.AddLine(right, bottom, right, bottom);

            path.AddLine(right - bottomRight, bottom, left + bottomLeft, bottom);

            if (bottomLeft > 0)
                path.AddArc(left, bottom - bottomLeft * 2, bottomLeft * 2, bottomLeft * 2, 90, 90);
            else
                path.AddLine(left, bottom, left, bottom);

            path.AddLine(left, bottom - bottomLeft, left, top + topLeft);
            path.CloseFigure();

            return path;
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
                UpdateBubbleRegion();
        }
        finally
        {
            ResumeLayout();
        }
    }

    private void UpdateAppearance()
    {
        // Update alignment and styling based on message direction
        if (_isIncoming)
        {
            _bodyPanel.Dock = DockStyle.Left;
            _authorLabel.Dock = DockStyle.Left;
            _bodyTextBox.TextAlign = HorizontalAlignment.Left;
        }
        else
        {
            _bodyPanel.Dock = DockStyle.Right;
            _authorLabel.Dock = DockStyle.Right;
            _bodyTextBox.TextAlign = HorizontalAlignment.Right;
        }

        // Update author label with timestamp
        if (_timestamp > DateTime.Today)
        {
            _authorLabel.Text = $"{_author}, {_timestamp.ToShortTimeString()}";
        }
        else
        {
            _authorLabel.Text = $"{_author}, {_timestamp.ToShortDateString()}";
        }

        ApplyBubbleColors();
        Invalidate();
    }

    private void ApplyBubbleColors()
    {
        var (startColor, endColor, textColor) = _isIncoming
            ? (AppThemeColors.ChatIncomingStart, AppThemeColors.ChatIncomingEnd, AppThemeColors.ChatIncomingText)
            : (AppThemeColors.ChatOutgoingStart, AppThemeColors.ChatOutgoingEnd, AppThemeColors.ChatOutgoingText);

        _bodyPanel.BackgroundColor = new BrushInfo(GradientStyle.Vertical, startColor, endColor);
        _bodyTextBox.BackColor = textColor;
        _bodyPanel.Invalidate();
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
