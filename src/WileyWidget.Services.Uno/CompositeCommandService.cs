using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

using Serilog;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services
{
    /// <summary>
    /// Service for managing composite commands that coordinate multiple operations.
    /// Provides centralized command management for complex UI interactions.
    /// </summary>
    public class CompositeCommandService : ICompositeCommandService
    {
        private readonly ILogger<CompositeCommandService> _logger;
        private readonly Dictionary<string, CompositeRelayCommand> _commands = new();

        public CompositeCommandService(ILogger<CompositeCommandService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates or gets a composite command with the specified name.
        /// </summary>
        public CompositeRelayCommand GetOrCreateCommand(string commandName, bool monitorCommandActivity = false)
        {
            if (string.IsNullOrEmpty(commandName))
                throw new ArgumentNullException(nameof(commandName));

            if (!_commands.TryGetValue(commandName, out var command))
            {
                command = new CompositeRelayCommand(monitorCommandActivity);
                _commands[commandName] = command;
                _logger.LogDebug("Created composite command: {CommandName}", commandName);
            }

            return command;
        }

        /// <summary>
        /// Registers a command with a composite command.
        /// </summary>
        public void RegisterCommand(string compositeCommandName, ICommand command)
        {
            if (string.IsNullOrEmpty(compositeCommandName))
                throw new ArgumentNullException(nameof(compositeCommandName));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var compositeCommand = GetOrCreateCommand(compositeCommandName);
            compositeCommand.RegisterCommand(command);

            _logger.LogDebug("Registered command with composite command: {CompositeCommandName}", compositeCommandName);
        }

        /// <summary>
        /// Unregisters a command from a composite command.
        /// </summary>
        public void UnregisterCommand(string compositeCommandName, ICommand command)
        {
            if (string.IsNullOrEmpty(compositeCommandName))
                throw new ArgumentNullException(nameof(compositeCommandName));
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            if (_commands.TryGetValue(compositeCommandName, out var compositeCommand))
            {
                compositeCommand.UnregisterCommand(command);
                _logger.LogDebug("Unregistered command from composite command: {CompositeCommandName}", compositeCommandName);
            }
        }

        /// <summary>
        /// Gets a composite command by name.
        /// </summary>
        public CompositeRelayCommand? GetCommand(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return null;

            return _commands.TryGetValue(commandName, out var command) ? command : null;
        }

        /// <summary>
        /// Removes a composite command.
        /// </summary>
        public void RemoveCommand(string commandName)
        {
            if (string.IsNullOrEmpty(commandName))
                return;

            if (_commands.Remove(commandName))
            {
                _logger.LogDebug("Removed composite command: {CommandName}", commandName);
            }
        }

        /// <summary>
        /// Gets all registered composite command names.
        /// </summary>
        public IEnumerable<string> GetCommandNames()
        {
            return _commands.Keys.ToList();
        }

        /// <summary>
        /// Creates a save command that coordinates multiple save operations.
        /// </summary>
        public CompositeRelayCommand CreateSaveCommand()
        {
            var saveCommand = GetOrCreateCommand("SaveAll", monitorCommandActivity: true);
            _logger.LogInformation("Created SaveAll composite command for coordinating save operations");
            return saveCommand;
        }

        /// <summary>
        /// Creates a refresh command that coordinates multiple refresh operations.
        /// </summary>
        public CompositeRelayCommand CreateRefreshCommand()
        {
            var refreshCommand = GetOrCreateCommand("RefreshAll", monitorCommandActivity: true);
            _logger.LogInformation("Created RefreshAll composite command for coordinating refresh operations");
            return refreshCommand;
        }

        /// <summary>
        /// Creates a validation command that coordinates multiple validation operations.
        /// </summary>
        public CompositeRelayCommand CreateValidationCommand()
        {
            var validationCommand = GetOrCreateCommand("ValidateAll", monitorCommandActivity: false);
            _logger.LogInformation("Created ValidateAll composite command for coordinating validation operations");
            return validationCommand;
        }
    }

    /// <summary>
    /// Interface for the composite command service.
    /// </summary>
    public interface ICompositeCommandService
    {
        CompositeRelayCommand GetOrCreateCommand(string commandName, bool monitorCommandActivity = false);
        void RegisterCommand(string compositeCommandName, ICommand command);
        void UnregisterCommand(string compositeCommandName, ICommand command);
        CompositeRelayCommand? GetCommand(string commandName);
        void RemoveCommand(string commandName);
        IEnumerable<string> GetCommandNames();
        CompositeRelayCommand CreateSaveCommand();
        CompositeRelayCommand CreateRefreshCommand();
        CompositeRelayCommand CreateValidationCommand();
    }

    /// <summary>
    /// CompositeRelayCommand: aggregates multiple ICommand instances and exposes
    /// a combined ICommand surface that executes all registered child commands.
    /// </summary>
    public class CompositeRelayCommand : ICommand
    {
        private readonly ObservableCollection<ICommand> _commands = new();
        private readonly bool _monitorActivity;

        public CompositeRelayCommand(bool monitorActivity = false)
        {
            _monitorActivity = monitorActivity;
        }

        public event EventHandler? CanExecuteChanged;

        public void RegisterCommand(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_commands.Contains(command)) return;

            _commands.Add(command);
            command.CanExecuteChanged += OnChildCanExecuteChanged;
            // Notify that composite can-execute may have changed
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UnregisterCommand(ICommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_commands.Remove(command))
            {
                command.CanExecuteChanged -= OnChildCanExecuteChanged;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnChildCanExecuteChanged(object? sender, EventArgs e)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object? parameter)
        {
            // Composite can execute if any child command can execute
            foreach (var cmd in _commands)
            {
                try
                {
                    if (cmd.CanExecute(parameter)) return true;
                }
                catch
                {
                    // Swallow to avoid bubbling a child exception during evaluation
                }
            }
            return false;
        }

        public void Execute(object? parameter)
        {
            foreach (var cmd in _commands.ToList())
            {
                try
                {
                    if (cmd.CanExecute(parameter)) cmd.Execute(parameter);
                }
                catch
                {
                    // Individual child failures should not stop others
                }
            }
        }

        /// <summary>
        /// Forces raising CanExecuteChanged on the composite command.
        /// </summary>
        public void NotifyCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
