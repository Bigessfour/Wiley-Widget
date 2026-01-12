using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace WileyWidget.WinForms.Services
{
    /// <summary>
    /// Role-based access control (RBAC) for enterprise security.
    /// Manages user roles and permissions for feature and data access.
    /// </summary>
    public class RoleBasedAccessControl
    {
        private readonly ILogger<RoleBasedAccessControl> _logger;
        private readonly Dictionary<string, UserRole> _roles = new();
        private readonly Dictionary<string, List<string>> _userRoles = new();

        public RoleBasedAccessControl(ILogger<RoleBasedAccessControl> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeDefaultRoles();
        }

        /// <summary>
        /// Checks if a user has a specific permission.
        /// </summary>
        public bool HasPermission(string user, string permission)
        {
            if (!_userRoles.TryGetValue(user, out var userRoleList))
            {
                _logger.LogWarning("User not found: {User}", user);
                return false;
            }

            return userRoleList
                .Select(roleName => _roles.TryGetValue(roleName, out var role) ? role : null)
                .Where(r => r != null)
                .Any(r => r!.Permissions.Contains(permission));
        }

        /// <summary>
        /// Assigns a role to a user.
        /// </summary>
        public void AssignRole(string user, string roleName)
        {
            if (!_roles.ContainsKey(roleName))
            {
                _logger.LogError("Role not found: {Role}", roleName);
                return;
            }

            if (!_userRoles.ContainsKey(user))
            {
                _userRoles[user] = new List<string>();
            }

            if (!_userRoles[user].Contains(roleName))
            {
                _userRoles[user].Add(roleName);
                _logger.LogInformation("Role assigned: {User} -> {Role}", user, roleName);
            }
        }

        /// <summary>
        /// Removes a role from a user.
        /// </summary>
        public void RemoveRole(string user, string roleName)
        {
            if (_userRoles.TryGetValue(user, out var roles))
            {
                roles.Remove(roleName);
                _logger.LogInformation("Role removed: {User} -> {Role}", user, roleName);
            }
        }

        /// <summary>
        /// Gets all roles for a user.
        /// </summary>
        public IReadOnlyList<string> GetUserRoles(string user)
        {
            return _userRoles.TryGetValue(user, out var roles) ? roles.AsReadOnly() : new List<string>().AsReadOnly();
        }

        /// <summary>
        /// Creates a custom role.
        /// </summary>
        public void CreateRole(string roleName, IEnumerable<string> permissions)
        {
            if (!_roles.ContainsKey(roleName))
            {
                _roles[roleName] = new UserRole
                {
                    Name = roleName,
                    Permissions = new HashSet<string>(permissions)
                };
                _logger.LogInformation("Role created: {Role}", roleName);
            }
        }

        /// <summary>
        /// Checks if user can access a resource.
        /// </summary>
        public bool CanAccessResource(string user, string resource)
        {
            return HasPermission(user, $"access:{resource}");
        }

        /// <summary>
        /// Checks if user can modify a resource.
        /// </summary>
        public bool CanModifyResource(string user, string resource)
        {
            return HasPermission(user, $"modify:{resource}") || HasPermission(user, "admin");
        }

        /// <summary>
        /// Checks if user is administrator.
        /// </summary>
        public bool IsAdmin(string user)
        {
            return GetUserRoles(user).Contains("Admin");
        }

        private void InitializeDefaultRoles()
        {
            // Administrator - full access
            _roles["Admin"] = new UserRole
            {
                Name = "Admin",
                Permissions = new HashSet<string>
                {
                    "admin",
                    "access:*",
                    "modify:*",
                    "delete:*",
                    "manage:users",
                    "manage:roles",
                    "view:audit"
                }
            };

            // Manager - read/write to budget data
            _roles["Manager"] = new UserRole
            {
                Name = "Manager",
                Permissions = new HashSet<string>
                {
                    "access:budgets",
                    "access:accounts",
                    "access:reports",
                    "modify:budgets",
                    "modify:accounts",
                    "view:reports"
                }
            };

            // Accountant - read-only access
            _roles["Accountant"] = new UserRole
            {
                Name = "Accountant",
                Permissions = new HashSet<string>
                {
                    "access:budgets",
                    "access:accounts",
                    "access:reports",
                    "view:reports"
                }
            };

            // Viewer - read-only dashboard
            _roles["Viewer"] = new UserRole
            {
                Name = "Viewer",
                Permissions = new HashSet<string>
                {
                    "access:dashboard",
                    "view:reports"
                }
            };

            _logger.LogInformation("Default roles initialized");
        }

        private class UserRole
        {
            public string Name { get; set; } = string.Empty;
            public HashSet<string> Permissions { get; set; } = new();
        }
    }

    /// <summary>
    /// Authorization helper for checking permissions.
    /// </summary>
    public static class AuthorizationHelper
    {
        public static void RequirePermission(RoleBasedAccessControl rbac, string user, string permission)
        {
            if (rbac == null) throw new ArgumentNullException(nameof(rbac));

            if (!rbac.HasPermission(user, permission))
            {
                throw new UnauthorizedAccessException($"User '{user}' does not have permission: {permission}");
            }
        }

        public static void RequireRole(RoleBasedAccessControl rbac, string user, string role)
        {
            if (rbac == null) throw new ArgumentNullException(nameof(rbac));

            if (!rbac.GetUserRoles(user).Contains(role))
            {
                throw new UnauthorizedAccessException($"User '{user}' does not have role: {role}");
            }
        }
    }
}
