using System;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.AspNetCore.Components;
using WileyWidget.WinForms.BlazorComponents;

namespace WileyWidget.WinForms.Forms
{
    /// <summary>
    /// Form that hosts the BlazorWebView for JARVIS AI Assistant.
    /// </summary>
    public partial class JARVISChatHostForm : Form
    {
        private BlazorWebView _blazorView = null!;

        /// <summary>
        /// Initializes a new instance of the JARVISChatHostForm.
        /// </summary>
        public JARVISChatHostForm()
        {
            InitializeComponent();

            Text = "JARVIS AI Assist";
            Size = new System.Drawing.Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;

            // Initialize BlazorWebView
            _blazorView = new BlazorWebView
            {
                Dock = DockStyle.Fill,
                HostPage = "wwwroot/index.html"
            };

            // Inject application services
            _blazorView.Services = Program.Services;

            // Add root component - use App.razor as root component
            // App.razor will handle routing to JARVISAssist via Router
            _blazorView.RootComponents.Add(new RootComponent("#app", typeof(App), null));

            // Add view to form
            Controls.Add(_blazorView);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _blazorView?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Required method for Designer support - do not modify the contents with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 600);
            this.Name = "JARVISChatHostForm";
            this.Text = "JARVIS AI Assist";
        }
    }
}
