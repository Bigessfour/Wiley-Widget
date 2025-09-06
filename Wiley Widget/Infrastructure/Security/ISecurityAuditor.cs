namespace WileyWidget.Infrastructure.Security
{
    /// <summary>
    /// Interface for security auditing functionality.
    /// </summary>
    public interface ISecurityAuditor
    {
        /// <summary>
        /// Performs a security audit with the given message.
        /// </summary>
        /// <param name="message">The audit message.</param>
        void Audit(string message);
    }
}
