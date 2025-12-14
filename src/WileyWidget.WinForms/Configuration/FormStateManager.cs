using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Configuration
{
    /// <summary>
    /// Manages persistent window state (position, size, split positions) for forms.
    /// </summary>
    public class FormStateManager
    {
        private readonly string _configDir;
        private readonly ILogger _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FormStateManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WileyWidget"
            );

            if (!Directory.Exists(_configDir))
            {
                Directory.CreateDirectory(_configDir);
            }

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// Save form window state (position, size, split container positions).
        /// </summary>
        public void SaveFormState(Form form, string formName, int? mainSplitterDistance = null, int? leftSplitterDistance = null)
        {
            ArgumentNullException.ThrowIfNull(form);

            try
            {
                var state = new FormWindowState
                {
                    FormName = formName,
                    X = form.Location.X,
                    Y = form.Location.Y,
                    Width = form.Width,
                    Height = form.Height,
                    WindowState = (int)form.WindowState,
                    MainSplitterDistance = mainSplitterDistance,
                    LeftSplitterDistance = leftSplitterDistance,
                    SavedAt = DateTime.UtcNow
                };

                var filePath = GetStateFilePath(formName);
                var json = JsonSerializer.Serialize(state, _jsonOptions);
                File.WriteAllText(filePath, json);

                _logger.LogDebug("Saved form state for {FormName} at {Path}", formName, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save form state for {FormName}", formName);
            }
        }

        /// <summary>
        /// Load form window state if available. Returns null if no saved state exists.
        /// </summary>
        public FormWindowState? LoadFormState(string formName)
        {
            try
            {
                var filePath = GetStateFilePath(formName);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = File.ReadAllText(filePath);
                var state = JsonSerializer.Deserialize<FormWindowState>(json, _jsonOptions);

                _logger.LogDebug("Loaded form state for {FormName} from {Path}", formName, filePath);
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load form state for {FormName}", formName);
                return null;
            }
        }

        /// <summary>
        /// Apply saved window state to a form (position, size, window state).
        /// </summary>
        public void ApplyFormState(Form form, FormWindowState state)
        {
            ArgumentNullException.ThrowIfNull(form);

            try
            {
                if (state == null)
                {
                    return;
                }

                // Restore window state first - convert int to FormWindowState enum
                var windowStateInt = state.WindowState;
                var windowStateEnum = (System.Windows.Forms.FormWindowState)windowStateInt;
                form.WindowState = windowStateEnum;

                // Only restore position/size if not maximized
                if (form.WindowState != System.Windows.Forms.FormWindowState.Maximized)
                {
                    // Validate that location is visible on at least one screen
                    var bounds = new System.Drawing.Rectangle(state.X, state.Y, state.Width, state.Height);
                    if (IsVisibleOnScreen(bounds))
                    {
                        form.Location = new System.Drawing.Point(state.X, state.Y);
                        form.Size = new System.Drawing.Size(state.Width, state.Height);
                    }
                    else
                    {
                        _logger.LogDebug("Saved form position not visible on any screen; using default position");
                    }
                }

                _logger.LogDebug("Applied form state to {FormName}", state.FormName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply form state to {FormName}", state?.FormName);
            }
        }

        private bool IsVisibleOnScreen(System.Drawing.Rectangle bounds)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return true;
                }
            }
            return false;
        }

        private string GetStateFilePath(string formName)
        {
            return Path.Combine(_configDir, $"{formName}.state.json");
        }
    }

    /// <summary>
    /// Represents the saved state of a form window.
    /// </summary>
    public class FormWindowState
    {
        public enum WindowStateEnum
        {
            Normal = 0,
            Minimized = 1,
            Maximized = 2
        }

        [JsonPropertyName("formName")]
        public string FormName { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("windowState")]
        public int WindowState { get; set; }

        [JsonPropertyName("mainSplitterDistance")]
        public int? MainSplitterDistance { get; set; }

        [JsonPropertyName("leftSplitterDistance")]
        public int? LeftSplitterDistance { get; set; }

        [JsonPropertyName("savedAt")]
        public DateTime SavedAt { get; set; }
    }
}
