using System.Threading.Tasks;

namespace WileyWidget.Services
{
    /// <summary>
    /// Interface for accessing current user context information
    /// </summary>
    public interface IUserContext
    {
        /// <summary>
        /// Gets the current user ID
        /// </summary>
        string? GetCurrentUserId();

        /// <summary>
        /// Gets the current user name
        /// </summary>
        string? GetCurrentUserName();

        /// <summary>
        /// Sets the current user context
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="userName">The user name</param>
        void SetCurrentUser(string? userId, string? userName);
    }
}
