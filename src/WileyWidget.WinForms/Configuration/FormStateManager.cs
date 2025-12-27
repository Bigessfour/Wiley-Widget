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
    /// <summary>
    /// Represents a class for formstatemanager.
    /// </summary>
    /// <summary>
    /// Represents a class for formstatemanager.
    /// </summary>
    /// <summary>
    /// Represents a class for formstatemanager.
    /// </summary>
    /// <summary>
    /// Represents a class for formstatemanager.
    /// </summary>
    public class FormStateManager
    {
        /// <summary>
        /// Represents the _configdir.
        /// </summary>
        private readonly string _configDir;
        /// <summary>
        /// Represents the _logger.
        /// </summary>
        /// <summary>
        /// Represents the _logger.
        /// </summary>
        private readonly ILogger _logger;
        /// <summary>
        /// Represents the _jsonoptions.
        /// </summary>
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
        /// <summary>
        /// Performs saveformstate. Parameters: form, formName, null, null.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="formName">The formName.</param>
        /// <param name="null">The null.</param>
        /// <param name="null">The null.</param>
        /// <summary>
        /// Performs saveformstate. Parameters: form, formName, null, null.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="formName">The formName.</param>
        /// <param name="null">The null.</param>
        /// <param name="null">The null.</param>
        /// <summary>
        /// Performs saveformstate. Parameters: form, formName, null, null.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="formName">The formName.</param>
        /// <param name="null">The null.</param>
        /// <param name="null">The null.</param>
        /// <summary>
        /// Performs saveformstate. Parameters: form, formName, null, null.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="formName">The formName.</param>
        /// <param name="null">The null.</param>
        /// <param name="null">The null.</param>
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
        /// <summary>
        /// Performs applyformstate. Parameters: form, state.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="state">The state.</param>
        /// <summary>
        /// Performs applyformstate. Parameters: form, state.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="state">The state.</param>
        /// <summary>
        /// Performs applyformstate. Parameters: form, state.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="state">The state.</param>
        /// <summary>
        /// Performs applyformstate. Parameters: form, state.
        /// </summary>
        /// <param name="form">The form.</param>
        /// <param name="state">The state.</param>
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
                    var bounds = new Rectangle(state.X, state.Y, state.Width, state.Height);
                    if (IsVisibleOnScreen(bounds))
                    {
                        form.Location = new Point(state.X, state.Y);
                        form.Size = new Size(state.Width, state.Height);
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
        /// <summary>
        /// Performs isvisibleonscreen. Parameters: bounds.
        /// </summary>
        /// <param name="bounds">The bounds.</param>
        /// <summary>
        /// Performs isvisibleonscreen. Parameters: bounds.
        /// </summary>
        /// <param name="bounds">The bounds.</param>

        private bool IsVisibleOnScreen(Rectangle bounds)
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
        /// <summary>
        /// Performs getstatefilepath. Parameters: formName.
        /// </summary>
        /// <param name="formName">The formName.</param>
        /// <summary>
        /// Performs getstatefilepath. Parameters: formName.
        /// </summary>
        /// <param name="formName">The formName.</param>

        private string GetStateFilePath(string formName)
        {
            return Path.Combine(_configDir, $"{formName}.state.json");
        }
    }

    /// <summary>
    /// Represents the saved state of a form window.
    /// </summary>
    /// <summary>
    /// Represents a class for formwindowstate.
    /// </summary>
    /// <summary>
    /// Represents a class for formwindowstate.
    /// </summary>
    /// <summary>
    /// Represents a class for formwindowstate.
    /// </summary>
    /// <summary>
    /// Represents a class for formwindowstate.
    /// </summary>
    public class FormWindowState
    {
        /// <summary>
        /// Defines the windowstateenum enumeration.
        /// </summary>
        /// <summary>
        /// Defines the windowstateenum enumeration.
        /// </summary>
        /// <summary>
        /// Defines the windowstateenum enumeration.
        /// </summary>
        /// <summary>
        /// Defines the windowstateenum enumeration.
        /// </summary>
        public enum WindowStateEnum
        {
            Normal = 0,
            Minimized = 1,
            Maximized = 2
        }

        [JsonPropertyName("formName")]
        /// <summary>
        /// Gets or sets the formname.
        /// </summary>
        /// <summary>
        /// Gets or sets the formname.
        /// </summary>
        /// <summary>
        /// Gets or sets the formname.
        /// </summary>
        /// <summary>
        /// Gets or sets the formname.
        /// </summary>
        /// <summary>
        /// Gets or sets the formname.
        /// </summary>
        public string FormName { get; set; } = string.Empty;

        [JsonPropertyName("x")]
        /// <summary>
        /// Gets or sets the X coordinate of the form window.
        /// </summary>
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        public int X { get; set; }

        [JsonPropertyName("y")]
        /// <summary>
        /// Gets or sets the Y coordinate of the form window.
        /// </summary>
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        public int Y { get; set; }

        [JsonPropertyName("width")]
        /// <summary>
        /// Gets or sets the width of the form window.
        /// </summary>
        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        public int Width { get; set; }

        [JsonPropertyName("height")]
        /// <summary>
        /// Gets or sets the height of the form window.
        /// </summary>
        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        public int Height { get; set; }

        [JsonPropertyName("windowState")]
        /// <summary>
        /// Gets or sets the window state of the form (0=Normal, 1=Minimized, 2=Maximized).
        /// </summary>
        /// <summary>
        /// Gets or sets the windowstate.
        /// </summary>
        /// <summary>
        /// Gets or sets the windowstate.
        /// </summary>
        /// <summary>
        /// Gets or sets the windowstate.
        /// </summary>
        /// <summary>
        /// Gets or sets the windowstate.
        /// </summary>
        public int WindowState { get; set; }

        [JsonPropertyName("mainSplitterDistance")]
        public int? MainSplitterDistance { get; set; }

        [JsonPropertyName("leftSplitterDistance")]
        public int? LeftSplitterDistance { get; set; }

        [JsonPropertyName("savedAt")]
        /// <summary>
        /// Gets or sets the savedat.
        /// </summary>
        /// <summary>
        /// Gets or sets the savedat.
        /// </summary>
        /// <summary>
        /// Gets or sets the savedat.
        /// </summary>
        /// <summary>
        /// Gets or sets the savedat.
        /// </summary>
        /// <summary>
        /// Gets or sets the savedat.
        /// </summary>
        public DateTime SavedAt { get; set; }
    }
}
