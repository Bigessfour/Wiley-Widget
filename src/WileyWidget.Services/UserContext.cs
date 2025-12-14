using System.Threading;
using WileyWidget.Services.Abstractions;

namespace WileyWidget.Services
{
    /// <summary>
    /// Simple implementation of user context service using AsyncLocal for thread safety
    /// </summary>
    public class UserContext : IUserContext
    {
        private static readonly AsyncLocal<string?> _currentUserId = new();
        private static readonly AsyncLocal<string?> _currentUserName = new();
        /// <summary>
        /// Gets the current user ID from the async local storage
        /// </summary>
        public string? UserId => _currentUserId.Value;

        /// <summary>
        /// Gets the display name of the current user from the async local storage
        /// </summary>
        public string? DisplayName => _currentUserName.Value;

        /// <summary>
        /// Backwards-compatible: gets the current user ID
        /// </summary>
        public string? GetCurrentUserId() => UserId;

        /// <summary>
        /// Backwards-compatible: gets the current user name
        /// </summary>
        public string? GetCurrentUserName() => DisplayName;

        /// <summary>
        /// Sets the current user context in async local storage
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="userName">The user name</param>
        public void SetCurrentUser(string? userId, string? userName)
        {
            _currentUserId.Value = userId;
            _currentUserName.Value = userName;
        }
    }
}
