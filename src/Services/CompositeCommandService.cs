using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Prism.Commands;
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
        private readonly Dictionary<string, CompositeCommand> _commands = new();

        public CompositeCommandService(ILogger<CompositeCommandService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates or gets a composite command with the specified name.
        /// </summary>
        public CompositeCommand GetOrCreateCommand(string commandName, bool monitorCommandActivity = false)
        {
            if (string.IsNullOrEmpty(commandName))
                throw new ArgumentNullException(nameof(commandName));

            if (!_commands.TryGetValue(commandName, out var command))
            {
                command = new CompositeCommand(monitorCommandActivity);
                _commands[commandName] = command;
                _logger.LogDebug("Created composite command: {CommandName}", commandName);
            }

            return command;
        }

        /// <summary>
        /// Registers a command with a composite command.
        /// </summary>
        public void RegisterCommand(string compositeCommandName, DelegateCommandBase command)
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
        public void UnregisterCommand(string compositeCommandName, DelegateCommandBase command)
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
        public CompositeCommand? GetCommand(string commandName)
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
        public CompositeCommand CreateSaveCommand()
        {
            var saveCommand = GetOrCreateCommand("SaveAll", monitorCommandActivity: true);
            _logger.LogInformation("Created SaveAll composite command for coordinating save operations");
            return saveCommand;
        }

        /// <summary>
        /// Creates a refresh command that coordinates multiple refresh operations.
        /// </summary>
        public CompositeCommand CreateRefreshCommand()
        {
            var refreshCommand = GetOrCreateCommand("RefreshAll", monitorCommandActivity: true);
            _logger.LogInformation("Created RefreshAll composite command for coordinating refresh operations");
            return refreshCommand;
        }

        /// <summary>
        /// Creates a validation command that coordinates multiple validation operations.
        /// </summary>
        public CompositeCommand CreateValidationCommand()
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
        CompositeCommand GetOrCreateCommand(string commandName, bool monitorCommandActivity = false);
        void RegisterCommand(string compositeCommandName, DelegateCommandBase command);
        void UnregisterCommand(string compositeCommandName, DelegateCommandBase command);
        CompositeCommand? GetCommand(string commandName);
        void RemoveCommand(string commandName);
        IEnumerable<string> GetCommandNames();
        CompositeCommand CreateSaveCommand();
        CompositeCommand CreateRefreshCommand();
        CompositeCommand CreateValidationCommand();
    }
}
