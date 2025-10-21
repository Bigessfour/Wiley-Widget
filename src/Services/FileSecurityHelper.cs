using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WileyWidget.Services
{
    /// <summary>
    /// Provides platform-aware file security operations, particularly ACL restrictions on Windows
    /// </summary>
    public static class FileSecurityHelper
    {
        /// <summary>
        /// Restricts file access to the current user only (Windows ACL-based)
        /// </summary>
        /// <param name="filePath">Path to the file to restrict</param>
        /// <returns>True if ACL was applied successfully, false if platform doesn't support or operation failed</returns>
        public static bool RestrictFileToCurrentUser(string filePath)
        {
            if (!File.Exists(filePath))
                return false;

            // Only apply ACLs on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileSecurity = fileInfo.GetAccessControl();

                // Remove inherited permissions
                fileSecurity.SetAccessRuleProtection(true, false);

                // Remove all existing rules
                foreach (System.Security.AccessControl.FileSystemAccessRule rule in fileSecurity.GetAccessRules(true, false, typeof(System.Security.Principal.NTAccount)))
                {
                    fileSecurity.RemoveAccessRule(rule);
                }

                // Add rule for current user only
                var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                var accessRule = new System.Security.AccessControl.FileSystemAccessRule(
                    currentUser,
                    System.Security.AccessControl.FileSystemRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow);

                fileSecurity.AddAccessRule(accessRule);
                fileInfo.SetAccessControl(fileSecurity);

                return true;
            }
            catch (Exception)
            {
                // Platform or permission issue - return false
                return false;
            }
        }
    }
}
