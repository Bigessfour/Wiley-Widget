using Syncfusion.Windows.Forms.Tools;
using Syncfusion.WinForms.Controls;
using Syncfusion.Drawing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WileyWidget.Models;
using WileyWidget.WinForms.Themes;

namespace WileyWidget.WinForms.Controls.ChatUI;

/// <summary>
/// Scrollable message list container that displays ChatItem bubbles.
/// Auto-resizes bubbles to 60% of available width (similar to modern chat UIs).
/// Integrates with ChatMessage model from WileyWidget.Models.
/// </summary>
public sealed class ChatMessageList : UserControl
{
    private readonly Panel _itemsPanel;

    private const double DefaultMaxBubbleWidthRatio = 0.6;

    /// <summary>
    /// Maximum bubble width as a ratio of the available panel width.
    /// Defaults to 60% to match typical chat UIs.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double MaxBubbleWidthRatio { get; set; } = DefaultMaxBubbleWidthRatio;

    public ChatMessageList()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        _itemsPanel = new GradientPanelExt
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(10),
            Name = "ChatItemsPanel",
            BorderStyle = BorderStyle.None,
            BackColor = Color.White
        };

        Controls.Add(_itemsPanel);

        // Apply theme to panel for proper rendering
        SfSkinManager.SetVisualStyle(_itemsPanel, ThemeColors.DefaultTheme);

        Resize += (_, _) => ApplyBubbleWidthToAllItems();

        // Add initial placeholder message (matches reference implementation)
        AddPlaceholderMessage();
    }

    private void AddPlaceholderMessage()
    {
        var welcome = new ChatMessage
        {
            Message = "👋 Hey there! I'm your AI assistant powered by Grok. Ask me anything—code help, budget analysis, or why your docking panels refuse to cooperate. Let's make something awesome!",
            IsUser = false,
            Timestamp = DateTime.Now
        };
        AddMessage(welcome);
    }

    public void ClearItems()
    {
        _itemsPanel.SuspendLayout();
        try
        {
            _itemsPanel.Controls.Clear();
        }
        finally
        {
            _itemsPanel.ResumeLayout();
        }
    }

    public void AddMessage(ChatMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var chatItem = CreateChatItem(message);
        AddItem(chatItem);
    }

    public void SetItems(IEnumerable<ChatMessage> messages)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        _itemsPanel.SuspendLayout();
        try
        {
            _itemsPanel.Controls.Clear();

            var messageList = messages.ToList();

            // If no messages, show placeholder
            if (messageList.Count == 0)
            {
                AddPlaceholderMessage();
                return;
            }

            foreach (var message in messageList)
            {
                if (message == null)
                    continue;

                var chatItem = CreateChatItem(message);
                chatItem.Name = "chatItem" + _itemsPanel.Controls.Count;
                chatItem.Dock = DockStyle.Top;
                _itemsPanel.Controls.Add(chatItem);
                chatItem.BringToFront();
            }
        }
        finally
        {
            _itemsPanel.ResumeLayout();
        }

        // Apply bubble widths after layout
        ApplyBubbleWidthToAllItems();
        ScrollToBottom();
    }

    private void AddItem(ChatItem item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        item.Name = "chatItem" + _itemsPanel.Controls.Count;
        item.Dock = DockStyle.Top;
        _itemsPanel.Controls.Add(item);
        item.BringToFront();

        ApplyBubbleWidth(item);
        ScrollToBottom();
    }

    private ChatItem CreateChatItem(ChatMessage message)
    {
        var author = message.IsUser ? "You" : "AI Assistant";
        var chatItem = new ChatItem
        {
            IsIncoming = !message.IsUser, // User messages are outgoing (right-aligned)
            Message = message.Message,
            Author = author,
            Timestamp = message.Timestamp
        };

        return chatItem;
    }

    private void ScrollToBottom()
    {
        if (_itemsPanel.Controls.Count == 0)
            return;

        var last = _itemsPanel.Controls[_itemsPanel.Controls.Count - 1];
        _itemsPanel.ScrollControlIntoView(last);
    }

    private void ApplyBubbleWidthToAllItems()
    {
        foreach (var control in _itemsPanel.Controls)
        {
            if (control is ChatItem item)
            {
                ApplyBubbleWidth(item);
            }
        }
    }

    private void ApplyBubbleWidth(ChatItem item)
    {
        var available = Math.Max(0, _itemsPanel.ClientSize.Width - _itemsPanel.Padding.Horizontal);

        var ratio = MaxBubbleWidthRatio;
        if (ratio <= 0 || ratio > 1)
            ratio = 0.6;

        var maxBubble = (int)Math.Floor(available * ratio);
        item.ResizeBubbles(maxBubble);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _itemsPanel?.Dispose();
        }

        base.Dispose(disposing);
    }
}
