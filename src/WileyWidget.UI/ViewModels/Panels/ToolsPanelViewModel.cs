using Prism.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using WileyWidget.UI.ViewModels;

namespace WileyWidget.ViewModels.Panels
{
    /// <summary>
    /// Represents a toolbar item for the tools panel.
    /// </summary>
    public class ToolItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        /// <summary>
        /// Gets or sets the display name of the tool item.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private ICommand? _command;
        /// <summary>
        /// Gets or sets the command to execute when the item is clicked.
        /// </summary>
        public ICommand? Command
        {
            get => _command;
            set
            {
                if (_command != value)
                {
                    _command = value;
                    OnPropertyChanged(nameof(Command));
                }
            }
        }

        private string _toolTip = string.Empty;
        /// <summary>
        /// Gets or sets the tooltip text for the item.
        /// </summary>
        public string ToolTip
        {
            get => _toolTip;
            set
            {
                if (_toolTip != value)
                {
                    _toolTip = value;
                    OnPropertyChanged(nameof(ToolTip));
                }
            }
        }

        private string _icon = string.Empty;
        /// <summary>
        /// Gets or sets the icon or symbol for the item.
        /// </summary>
        public string Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }

        private bool _isEnabled = true;
        /// <summary>
        /// Gets or sets whether the item is enabled.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel for the Tools Panel View
    /// Provides utilities like calculator, unit converter, and date calculator.
    /// </summary>
    public class ToolsPanelViewModel : BasePanelViewModel, IDisposable
    {
        private System.Timers.Timer _autoSaveTimer;

        #region Toolbar Properties

        private ObservableCollection<ToolItem> _toolItems = new();
        /// <summary>
        /// Gets or sets the collection of toolbar items for the SfToolBar.
        /// Contains quick access buttons for different tool functions.
        /// </summary>
        public ObservableCollection<ToolItem> ToolItems
        {
            get => _toolItems;
            set => SetProperty(ref _toolItems, value);
        }

        private int _selectedTabIndex;
        /// <summary>
        /// Gets or sets the index of the currently selected tab.
        /// Used to switch between Calculator, Unit Converter, Date Calculator, and Notes tabs.
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    UpdateToolbarItemStates();
                }
            }
        }

        #endregion

        #region Calculator Properties

        private string _calculatorDisplay = "0";
        /// <summary>
        /// Gets or sets the calculator display value.
        /// Updated as user inputs numbers and operations.
        /// </summary>
        public string CalculatorDisplay
        {
            get => _calculatorDisplay;
            set => SetProperty(ref _calculatorDisplay, value);
        }

        private string _calculatorInput = string.Empty;
        /// <summary>
        /// Gets or sets the current calculator input string.
        /// Built as user presses buttons.
        /// </summary>
        public string CalculatorInput
        {
            get => _calculatorInput;
            set => SetProperty(ref _calculatorInput, value);
        }

        private string _calculatorOperation = string.Empty;
        /// <summary>
        /// Gets or sets the current operation (+, -, *, /).
        /// Stores selected operation between operands.
        /// </summary>
        public string CalculatorOperation
        {
            get => _calculatorOperation;
            set => SetProperty(ref _calculatorOperation, value);
        }

        private double _calculatorFirstOperand;
        /// <summary>
        /// Gets or sets the first operand for calculations.
        /// Stored when operation is selected.
        /// </summary>
        public double CalculatorFirstOperand
        {
            get => _calculatorFirstOperand;
            set => SetProperty(ref _calculatorFirstOperand, value);
        }

        private bool _calculatorNewEntry = true;
        /// <summary>
        /// Gets or sets whether the next input should start a new entry.
        /// Reset after operations.
        /// </summary>
        public bool CalculatorNewEntry
        {
            get => _calculatorNewEntry;
            set => SetProperty(ref _calculatorNewEntry, value);
        }

        private ObservableCollection<string> _calculatorHistory = new();
        /// <summary>
        /// Gets or sets the calculator history.
        /// Entries added after each calculation.
        /// </summary>
        public ObservableCollection<string> CalculatorHistory
        {
            get => _calculatorHistory;
            set => SetProperty(ref _calculatorHistory, value);
        }

        private double _calculatorMemory;
        /// <summary>
        /// Gets or sets the calculator memory value.
        /// Used for memory recall, store, and add operations.
        /// </summary>
        public double CalculatorMemory
        {
            get => _calculatorMemory;
            set => SetProperty(ref _calculatorMemory, value);
        }

        #endregion

        #region Calculator Commands

        /// <summary>
        /// Command to input a number to the calculator.
        /// Number input logic implemented.
        /// </summary>
        public ICommand CalculatorNumberCommand { get; }

        /// <summary>
        /// Command to input an operation (+, -, *, /).
        /// Operation selection logic implemented.
        /// </summary>
        public ICommand CalculatorOperationCommand { get; }

        /// <summary>
        /// Command to calculate the result.
        /// Calculation logic based on stored operation implemented.
        /// </summary>
        public ICommand CalculatorEqualsCommand { get; }

        /// <summary>
        /// Command to clear the calculator.
        /// Resets all calculator state.
        /// </summary>
        public ICommand CalculatorClearCommand { get; }

        /// <summary>
        /// Command to clear calculator history.
        /// Clears the history collection.
        /// </summary>
        public ICommand CalculatorClearHistoryCommand { get; }

        /// <summary>
        /// Command to backspace in calculator input.
        /// Removes last character from input.
        /// </summary>
        public ICommand CalculatorBackspaceCommand { get; }

        /// <summary>
        /// Command to add decimal point.
        /// Adds decimal point if not already present.
        /// </summary>
        public ICommand CalculatorDecimalCommand { get; }

        /// <summary>
        /// Command to toggle sign (+/-).
        /// Multiplies current value by -1.
        /// </summary>
        public ICommand CalculatorToggleSignCommand { get; }

        /// <summary>
        /// Command to clear calculator memory.
        /// </summary>
        public ICommand CalculatorMemoryClearCommand { get; }

        /// <summary>
        /// Command to recall value from calculator memory.
        /// </summary>
        public ICommand CalculatorMemoryRecallCommand { get; }

        /// <summary>
        /// Command to store current value in calculator memory.
        /// </summary>
        public ICommand CalculatorMemoryStoreCommand { get; }

        /// <summary>
        /// Command to add current value to calculator memory.
        /// </summary>
        public ICommand CalculatorMemoryAddCommand { get; }

        /// <summary>
        /// Command to clear current entry (CE button).
        /// </summary>
        public ICommand CalculatorClearEntryCommand { get; }

        #endregion

        #region Unit Converter Properties

        private string _converterValue = "0";
        /// <summary>
        /// Gets or sets the value to convert.
        /// Triggers conversion when changed.
        /// </summary>
        public string ConverterValue
        {
            get => _converterValue;
            set => SetProperty(ref _converterValue, value);
        }

        private string _converterFromUnit = "Meters";
        /// <summary>
        /// Gets or sets the source unit.
        /// Triggers conversion when changed.
        /// </summary>
        public string ConverterFromUnit
        {
            get => _converterFromUnit;
            set => SetProperty(ref _converterFromUnit, value);
        }

        private string _converterToUnit = "Feet";
        /// <summary>
        /// Gets or sets the target unit.
        /// Triggers conversion when changed.
        /// </summary>
        public string ConverterToUnit
        {
            get => _converterToUnit;
            set => SetProperty(ref _converterToUnit, value);
        }

        private string _converterResult = "0";
        /// <summary>
        /// Gets or sets the conversion result.
        /// Calculated based on value and unit pair.
        /// </summary>
        public string ConverterResult
        {
            get => _converterResult;
            set => SetProperty(ref _converterResult, value);
        }

        private ObservableCollection<string> _availableUnits = new();
        /// <summary>
        /// Gets or sets the list of available units for conversion.
        /// Populated based on conversion category (length, weight, volume, etc.) via UpdateUnitsForCategory().
        /// </summary>
        public ObservableCollection<string> AvailableUnits
        {
            get => _availableUnits;
            set => SetProperty(ref _availableUnits, value);
        }

        private ObservableCollection<string> _unitCategories = new();
        /// <summary>
        /// Gets or sets the list of unit conversion categories (Length, Weight, Volume, etc.).
        /// </summary>
        public ObservableCollection<string> UnitCategories
        {
            get => _unitCategories;
            set => SetProperty(ref _unitCategories, value);
        }

        // Missing properties for XAML bindings
        private double? _fromValue;
        public double? FromValue
        {
            get => _fromValue;
            set
            {
                if (SetProperty(ref _fromValue, value))
                    OnConvertUnits();
            }
        }

        private ObservableCollection<string> _fromUnits = new();
        public ObservableCollection<string> FromUnits
        {
            get => _fromUnits;
            set => SetProperty(ref _fromUnits, value);
        }

        private string? _selectedFromUnit;
        public string? SelectedFromUnit
        {
            get => _selectedFromUnit;
            set
            {
                if (SetProperty(ref _selectedFromUnit, value))
                    OnConvertUnits();
            }
        }

        private string _toValue = string.Empty;
        public string ToValue
        {
            get => _toValue;
            set => SetProperty(ref _toValue, value);
        }

        private ObservableCollection<string> _toUnits = new();
        public ObservableCollection<string> ToUnits
        {
            get => _toUnits;
            set => SetProperty(ref _toUnits, value);
        }

        private string? _selectedToUnit;
        public string? SelectedToUnit
        {
            get => _selectedToUnit;
            set
            {
                if (SetProperty(ref _selectedToUnit, value))
                    OnConvertUnits();
            }
        }

        private string? _selectedUnitCategory;
        public string? SelectedUnitCategory
        {
            get => _selectedUnitCategory;
            set
            {
                if (SetProperty(ref _selectedUnitCategory, value))
                {
                    UpdateUnitsForCategory();
                }
            }
        }

        // Date calculator properties
        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private ObservableCollection<string> _dateOperations = new() { "Add Days", "Subtract Days", "Calculate Difference" };
        public ObservableCollection<string> DateOperations
        {
            get => _dateOperations;
            set => SetProperty(ref _dateOperations, value);
        }

        private string? _selectedDateOperation;
        public string? SelectedDateOperation
        {
            get => _selectedDateOperation;
            set => SetProperty(ref _selectedDateOperation, value);
        }

        private int? _dateValue;
        public int? DateValue
        {
            get => _dateValue;
            set => SetProperty(ref _dateValue, value);
        }

        private string _dateResult = string.Empty;
        public string DateResult
        {
            get => _dateResult;
            set => SetProperty(ref _dateResult, value);
        }

        // Notes property
        private string _notesText = string.Empty;
        public string NotesText
        {
            get => _notesText;
            set => SetProperty(ref _notesText, value);
        }

        #endregion

        #region Unit Converter Commands

        /// <summary>
        /// Command to perform unit conversion.
        /// Calculates conversion based on selected units.
        /// </summary>
        public ICommand ConvertUnitsCommand { get; }

        /// <summary>
        /// Command to swap from/to units.
        /// Swaps ConverterFromUnit and ConverterToUnit.
        /// </summary>
        public ICommand SwapUnitsCommand { get; }

        /// <summary>
        /// Command to clear converter.
        /// Resets converter state.
        /// </summary>
        public ICommand ClearConverterCommand { get; }

        #endregion

        #region Date Calculator Properties

        private DateTime _dateCalculatorStartDate = DateTime.Today;
        /// <summary>
        /// Gets or sets the start date for calculations.
        /// Used for date arithmetic.
        /// </summary>
        public DateTime DateCalculatorStartDate
        {
            get => _dateCalculatorStartDate;
            set => SetProperty(ref _dateCalculatorStartDate, value);
        }

        private DateTime _dateCalculatorEndDate = DateTime.Today;
        /// <summary>
        /// Gets or sets the end date for calculations.
        /// Used for date difference calculations.
        /// </summary>
        public DateTime DateCalculatorEndDate
        {
            get => _dateCalculatorEndDate;
            set => SetProperty(ref _dateCalculatorEndDate, value);
        }

        private int _dateCalculatorDays;
        /// <summary>
        /// Gets or sets the number of days to add/subtract.
        /// Used for date arithmetic.
        /// </summary>
        public int DateCalculatorDays
        {
            get => _dateCalculatorDays;
            set => SetProperty(ref _dateCalculatorDays, value);
        }

        private string _dateCalculatorResult = string.Empty;
        /// <summary>
        /// Gets or sets the date calculation result.
        /// Displays difference or calculated date.
        /// </summary>
        public string DateCalculatorResult
        {
            get => _dateCalculatorResult;
            set => SetProperty(ref _dateCalculatorResult, value);
        }

        #endregion

        #region Date Calculator Commands

        /// <summary>
        /// Command to calculate date difference.
        /// Calculates days between start and end dates.
        /// </summary>
        public ICommand CalculateDateDifferenceCommand { get; }

        /// <summary>
        /// Command to add days to date.
        /// Adds DateCalculatorDays to DateCalculatorStartDate.
        /// </summary>
        public ICommand AddDaysToDateCommand { get; }

        /// <summary>
        /// Command to subtract days from date.
        /// Subtracts DateCalculatorDays from DateCalculatorStartDate.
        /// </summary>
        public ICommand SubtractDaysFromDateCommand { get; }

        /// <summary>
        /// Command to calculate date based on selected operation.
        /// </summary>
        public ICommand CalculateDateCommand { get; }

        #endregion

        #region Notes Properties

        private string _notesContent = string.Empty;
        /// <summary>
        /// Gets or sets the notes content.
        /// Auto-saves when changed after a short delay.
        /// </summary>
        public string NotesContent
        {
            get => _notesContent;
            set
            {
                if (SetProperty(ref _notesContent, value))
                {
                    NotesHasUnsavedChanges = true;
                    // Auto-save after a short delay (2 seconds)
                    StartAutoSaveTimer();
                }
            }
        }

        private bool _notesHasUnsavedChanges;
        /// <summary>
        /// Gets or sets whether notes have unsaved changes.
        /// Tracks when content changes.
        /// </summary>
        public bool NotesHasUnsavedChanges
        {
            get => _notesHasUnsavedChanges;
            set => SetProperty(ref _notesHasUnsavedChanges, value);
        }

        #endregion

        #region Notes Commands

        /// <summary>
        /// Command to save notes.
        /// Persists notes to memory (can be extended to file/database).
        /// </summary>
        public ICommand SaveNotesCommand { get; }

        /// <summary>
        /// Command to clear notes.
        /// Clears NotesContent after confirmation dialog.
        /// </summary>
        public ICommand ClearNotesCommand { get; }

        #endregion

        #region Constructor

        public ToolsPanelViewModel()
        {
            // Initialize auto-save timer
            _autoSaveTimer = new System.Timers.Timer(2000); // 2 seconds delay
            _autoSaveTimer.Elapsed += OnAutoSaveTimerElapsed;
            _autoSaveTimer.AutoReset = false; // Only fire once per trigger

            // Initialize nullable properties to prevent binding errors
            DateValue = 0;

            // Initialize commands with implementations
            // Calculator, converter, and other tool commands are fully implemented

            // Calculator commands
            CalculatorNumberCommand = new DelegateCommand<string>(OnCalculatorNumber);
            CalculatorOperationCommand = new DelegateCommand<string>(OnCalculatorOperation);
            CalculatorEqualsCommand = new DelegateCommand(OnCalculatorEquals);
            CalculatorClearCommand = new DelegateCommand(OnCalculatorClear);
            CalculatorClearHistoryCommand = new DelegateCommand(OnCalculatorClearHistory);
            CalculatorBackspaceCommand = new DelegateCommand(OnCalculatorBackspace);
            CalculatorDecimalCommand = new DelegateCommand(OnCalculatorDecimal);
            CalculatorToggleSignCommand = new DelegateCommand(OnCalculatorToggleSign);
            CalculatorMemoryClearCommand = new DelegateCommand(OnCalculatorMemoryClear);
            CalculatorMemoryRecallCommand = new DelegateCommand(OnCalculatorMemoryRecall);
            CalculatorMemoryStoreCommand = new DelegateCommand(OnCalculatorMemoryStore);
            CalculatorMemoryAddCommand = new DelegateCommand(OnCalculatorMemoryAdd);
            CalculatorClearEntryCommand = new DelegateCommand(OnCalculatorClearEntry);

            // Unit converter commands
            ConvertUnitsCommand = new DelegateCommand(OnConvertUnits);
            SwapUnitsCommand = new DelegateCommand(OnSwapUnits);
            ClearConverterCommand = new DelegateCommand(OnClearConverter);

            // Date calculator commands
            CalculateDateDifferenceCommand = new DelegateCommand(OnCalculateDateDifference);
            AddDaysToDateCommand = new DelegateCommand(OnAddDaysToDate);
            SubtractDaysFromDateCommand = new DelegateCommand(OnSubtractDaysFromDate);
            CalculateDateCommand = new DelegateCommand(OnCalculateDate);

            // Notes commands
            SaveNotesCommand = new DelegateCommand(OnSaveNotes, CanSaveNotes);
            ClearNotesCommand = new DelegateCommand(OnClearNotes);

            // Initialize available units
            InitializeUnits();

            // Initialize toolbar items
            InitializeToolItems();
        }

        #endregion

        #region Toolbar Methods

        private void InitializeToolItems()
        {
            ToolItems.Add(new ToolItem
            {
                Name = "Calculator",
                Command = new DelegateCommand(() => SelectedTabIndex = 0, () => SelectedTabIndex != 0),
                ToolTip = "Switch to Calculator tab",
                Icon = "🧮",
                IsEnabled = SelectedTabIndex != 0
            });

            ToolItems.Add(new ToolItem
            {
                Name = "Unit Converter",
                Command = new DelegateCommand(() => SelectedTabIndex = 1, () => SelectedTabIndex != 1),
                ToolTip = "Switch to Unit Converter tab",
                Icon = "🔄",
                IsEnabled = SelectedTabIndex != 1
            });

            ToolItems.Add(new ToolItem
            {
                Name = "Date Calculator",
                Command = new DelegateCommand(() => SelectedTabIndex = 2, () => SelectedTabIndex != 2),
                ToolTip = "Switch to Date Calculator tab",
                Icon = "📅",
                IsEnabled = SelectedTabIndex != 2
            });

            ToolItems.Add(new ToolItem
            {
                Name = "Notes",
                Command = new DelegateCommand(() => SelectedTabIndex = 3, () => SelectedTabIndex != 3),
                ToolTip = "Switch to Notes tab",
                Icon = "📝",
                IsEnabled = SelectedTabIndex != 3
            });
        }

        private void UpdateToolbarItemStates()
        {
            foreach (var item in ToolItems)
            {
                // Update the IsEnabled property based on current tab
                switch (item.Name)
                {
                    case "Calculator":
                        item.IsEnabled = SelectedTabIndex != 0;
                        break;
                    case "Unit Converter":
                        item.IsEnabled = SelectedTabIndex != 1;
                        break;
                    case "Date Calculator":
                        item.IsEnabled = SelectedTabIndex != 2;
                        break;
                    case "Notes":
                        item.IsEnabled = SelectedTabIndex != 3;
                        break;
                }
            }

            // Raise property changed for the collection to update UI
            RaisePropertyChanged(nameof(ToolItems));
        }

        #endregion

        // Calculator handlers
        private void OnCalculatorNumber(string number)
        {
            if (CalculatorNewEntry)
            {
                CalculatorDisplay = number;
                CalculatorNewEntry = false;
            }
            else
            {
                // Don't allow multiple leading zeros
                if (CalculatorDisplay == "0" && number == "0")
                    return;

                // Replace leading zero with the number
                if (CalculatorDisplay == "0")
                    CalculatorDisplay = number;
                else
                    CalculatorDisplay += number;
            }
        }

        private void OnCalculatorOperation(string operation)
        {
            // If there's a pending operation, calculate it first
            if (!string.IsNullOrEmpty(CalculatorOperation) && !CalculatorNewEntry)
            {
                OnCalculatorEquals();
            }

            // Store the current display value as first operand
            if (double.TryParse(CalculatorDisplay, out double value))
            {
                CalculatorFirstOperand = value;
            }

            // Store the operation
            CalculatorOperation = operation;

            // Next input should start a new entry
            CalculatorNewEntry = true;
        }

        private void OnCalculatorEquals()
        {
            if (string.IsNullOrEmpty(CalculatorOperation))
                return;

            // Get the second operand from current display
            if (!double.TryParse(CalculatorDisplay, out double secondOperand))
                return;

            double result = 0;
            string operationSymbol = CalculatorOperation;

            // Perform the calculation
            switch (CalculatorOperation)
            {
                case "+":
                    result = CalculatorFirstOperand + secondOperand;
                    break;
                case "-":
                    result = CalculatorFirstOperand - secondOperand;
                    break;
                case "*":
                    result = CalculatorFirstOperand * secondOperand;
                    break;
                case "/":
                    if (secondOperand != 0)
                        result = CalculatorFirstOperand / secondOperand;
                    else
                    {
                        CalculatorDisplay = "Error";
                        CalculatorOperation = string.Empty;
                        CalculatorNewEntry = true;
                        return;
                    }
                    break;
                default:
                    return;
            }

            // Format result (remove unnecessary decimal places)
            string resultText = result.ToString("G15", CultureInfo.InvariantCulture);
            if (resultText.Contains(".", StringComparison.Ordinal) && resultText.EndsWith('0'))
            {
                resultText = resultText.TrimEnd('0').TrimEnd('.');
            }

            // Update display
            CalculatorDisplay = resultText;

            // Add to history
            string historyEntry = $"{CalculatorFirstOperand} {operationSymbol} {secondOperand} = {resultText}";
            CalculatorHistory.Add(historyEntry);

            // Reset for next calculation
            CalculatorOperation = string.Empty;
            CalculatorFirstOperand = result;
            CalculatorNewEntry = true;
        }

        private void OnCalculatorClear()
        {
            CalculatorDisplay = "0";
            CalculatorInput = string.Empty;
            CalculatorOperation = string.Empty;
            CalculatorFirstOperand = 0;
            CalculatorNewEntry = true;
        }

        private void OnCalculatorClearHistory()
        {
            // Clear calculator history
            CalculatorHistory.Clear();
        }

        private void OnCalculatorBackspace()
        {
            if (CalculatorDisplay.Length > 1)
            {
                CalculatorDisplay = CalculatorDisplay.Substring(0, CalculatorDisplay.Length - 1);
            }
            else if (CalculatorDisplay.Length == 1 && CalculatorDisplay != "0")
            {
                CalculatorDisplay = "0";
            }
            // If display is already "0", do nothing
        }

        private void OnCalculatorDecimal()
        {
            if (!CalculatorDisplay.Contains(".", StringComparison.Ordinal))
            {
                CalculatorDisplay += ".";
                CalculatorNewEntry = false;
            }
        }

        private void OnCalculatorToggleSign()
        {
            if (double.TryParse(CalculatorDisplay, out double value))
            {
                value = -value;
                CalculatorDisplay = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void OnCalculatorMemoryClear()
        {
            CalculatorMemory = 0;
        }

        private void OnCalculatorMemoryRecall()
        {
            CalculatorDisplay = CalculatorMemory.ToString(CultureInfo.InvariantCulture);
        }

        private void OnCalculatorMemoryStore()
        {
            if (double.TryParse(CalculatorDisplay, out double value))
            {
                CalculatorMemory = value;
            }
        }

        private void OnCalculatorMemoryAdd()
        {
            if (double.TryParse(CalculatorDisplay, out double value))
            {
                CalculatorMemory += value;
            }
        }

        private void OnCalculatorClearEntry()
        {
            // Clear current entry (CE) - reset display to 0
            CalculatorDisplay = "0";
        }

        // Unit converter handlers
        private void OnConvertUnits()
        {
            if (!FromValue.HasValue || string.IsNullOrEmpty(SelectedFromUnit) || string.IsNullOrEmpty(SelectedToUnit))
            {
                ToValue = "0";
                return;
            }

            var result = FromValue.Value;

            // Convert to base unit first, then to target unit
            result = ConvertToBaseUnit(result, SelectedFromUnit);
            result = ConvertFromBaseUnit(result, SelectedToUnit);

            ToValue = result.ToString("F6", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        }

        private double ConvertToBaseUnit(double value, string unit)
        {
            return unit switch
            {
                // Length - base unit: meters
                "Feet" => value * 0.3048,
                "Inches" => value * 0.0254,
                "Kilometers" => value * 1000,
                "Miles" => value * 1609.344,
                "Meters" => value,

                // Weight - base unit: kilograms
                "Pounds" => value * 0.453592,
                "Ounces" => value * 0.0283495,
                "Grams" => value * 0.001,
                "Kilograms" => value,

                // Volume - base unit: liters
                "Gallons" => value * 3.78541,
                "Milliliters" => value * 0.001,
                "Liters" => value,

                // Temperature - base unit: Celsius
                "Fahrenheit" => (value - 32) * 5/9,
                "Kelvin" => value - 273.15,
                "Celsius" => value,

                _ => value
            };
        }

        private double ConvertFromBaseUnit(double value, string unit)
        {
            return unit switch
            {
                // Length - from meters
                "Feet" => value / 0.3048,
                "Inches" => value / 0.0254,
                "Kilometers" => value / 1000,
                "Miles" => value / 1609.344,
                "Meters" => value,

                // Weight - from kilograms
                "Pounds" => value / 0.453592,
                "Ounces" => value / 0.0283495,
                "Grams" => value / 0.001,
                "Kilograms" => value,

                // Volume - from liters
                "Gallons" => value / 3.78541,
                "Milliliters" => value / 0.001,
                "Liters" => value,

                // Temperature - from Celsius
                "Fahrenheit" => value * 9/5 + 32,
                "Kelvin" => value + 273.15,
                "Celsius" => value,

                _ => value
            };
        }

        private void OnSwapUnits()
        {
            var temp = SelectedFromUnit;
            SelectedFromUnit = SelectedToUnit;
            SelectedToUnit = temp;
            OnConvertUnits();
        }

        private void OnClearConverter()
        {
            FromValue = 0;
            ToValue = "0";
            SelectedFromUnit = FromUnits?.FirstOrDefault();
            SelectedToUnit = ToUnits?.FirstOrDefault();
        }

        // Date calculator handlers
        private void OnCalculateDateDifference()
        {
            var difference = DateCalculatorEndDate - DateCalculatorStartDate;
            DateCalculatorResult = $"{Math.Abs(difference.Days)} days {(difference.Days >= 0 ? "later" : "earlier")}";
        }

        private void OnAddDaysToDate()
        {
            var resultDate = DateCalculatorStartDate.AddDays(DateCalculatorDays);
            DateCalculatorResult = resultDate.ToLongDateString();
        }

        private void OnSubtractDaysFromDate()
        {
            var resultDate = DateCalculatorStartDate.AddDays(-DateCalculatorDays);
            DateCalculatorResult = resultDate.ToLongDateString();
        }

        private void OnCalculateDate()
        {
            // Calculate based on SelectedDateOperation
            if (!StartDate.HasValue || !DateValue.HasValue) return;

            switch (SelectedDateOperation)
            {
                case "Add Days":
                    DateResult = StartDate.Value.AddDays(DateValue.Value).ToShortDateString();
                    break;
                case "Subtract Days":
                    DateResult = StartDate.Value.AddDays(-DateValue.Value).ToShortDateString();
                    break;
                default:
                    DateResult = "Select an operation";
                    break;
            }
        }

        #region Notes Handlers

        // Notes handlers
        private void OnSaveNotes()
        {
            NotesHasUnsavedChanges = false;
            // Notes are already bound to NotesText property, so they're saved in memory
            // Future: persist to file or database if needed
        }

        private bool CanSaveNotes()
        {
            return NotesHasUnsavedChanges;
        }

        private void OnClearNotes()
        {
            // Add confirmation dialog before clearing
            var result = MessageBox.Show(
                "Are you sure you want to clear all notes? This action cannot be undone.",
                "Clear Notes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                NotesContent = string.Empty;
                NotesText = string.Empty;
                NotesHasUnsavedChanges = false;
            }
        }

#endregion

        #region Helper Methods

        private void StartAutoSaveTimer()
        {
            // Stop existing timer if running
            _autoSaveTimer.Stop();
            // Start new timer
            _autoSaveTimer.Start();
        }

        private void OnAutoSaveTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Auto-save notes on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnSaveNotes();
            });
        }

        private void InitializeUnits()
        {
            // Initialize unit categories
            UnitCategories = new ObservableCollection<string>
            {
                "Length", "Weight", "Volume", "Temperature"
            };

            // Set default category and update units
            SelectedUnitCategory = "Length";
            FromValue = 0;
            ToValue = "0";
        }

        private void UpdateUnitsForCategory()
        {
            if (string.IsNullOrEmpty(SelectedUnitCategory))
                return;

            var units = SelectedUnitCategory switch
            {
                "Length" => new[] { "Meters", "Feet", "Inches", "Kilometers", "Miles" },
                "Weight" => new[] { "Kilograms", "Pounds", "Ounces", "Grams" },
                "Volume" => new[] { "Liters", "Gallons", "Milliliters" },
                "Temperature" => new[] { "Celsius", "Fahrenheit", "Kelvin" },
                _ => Array.Empty<string>()
            };

            FromUnits = new ObservableCollection<string>(units);
            ToUnits = new ObservableCollection<string>(units);

            // Set default selections
            SelectedFromUnit = units.FirstOrDefault();
            SelectedToUnit = units.Skip(1).FirstOrDefault() ?? units.FirstOrDefault();
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _autoSaveTimer?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
