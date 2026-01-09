using System.Security.Cryptography;
using System.Text;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// User service implementation (temporary with mock data for Phase 2 testing)
/// </summary>
public class UserService : IUserService
{
    // Temporary mock data for testing - use static to share across instances
    private static readonly List<User> _mockUsers = InitializeMockUsers();
    private static readonly object _lock = new object();

    private static List<User> InitializeMockUsers()
    {
        // Use same hashing algorithm as AuthenticationService
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(Constants.DEFAULT_PASSWORD);
        var hash = sha256.ComputeHash(bytes);
        var hashedPassword = Convert.ToBase64String(hash);

        return new List<User>
        {
            new User
            {
                Id = 1,
                Username = Constants.ADMIN_USERNAME,
                Name = "Administrator",
                PasswordHash = hashedPassword,
                Role = UserRole.Administrator,
                IsEnabled = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new User
            {
                Id = 2,
                Username = Constants.USER_USERNAME,
                Name = "Operator",
                PasswordHash = hashedPassword,
                Role = UserRole.Operator,
                IsEnabled = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new User
            {
                Id = 3,
                Username = Constants.GUEST_USERNAME,
                Name = "Guest",
                PasswordHash = string.Empty,
                Role = UserRole.Guest,
                IsEnabled = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            }
        };
    }

    /// <inheritdoc/>
    public Task<List<User>> GetAllUsersAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_mockUsers.ToList());
        }
    }

    /// <inheritdoc/>
    public Task<User?> GetUserByIdAsync(int id)
    {
        lock (_lock)
        {
            var user = _mockUsers.FirstOrDefault(u => u.Id == id);
            return Task.FromResult(user);
        }
    }

    /// <inheritdoc/>
    public Task<User?> GetUserByUsernameAsync(string username)
    {
        lock (_lock)
        {
            var user = _mockUsers.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }
    }

    /// <inheritdoc/>
    public Task<int> AddUserAsync(User user)
    {
        lock (_lock)
        {
            user.Id = _mockUsers.Any() ? _mockUsers.Max(u => u.Id) + 1 : 1;
            user.CreatedAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            _mockUsers.Add(user);
            return Task.FromResult(user.Id);
        }
    }

    /// <inheritdoc/>
    public Task<bool> UpdateUserAsync(User user)
    {
        lock (_lock)
        {
            var existing = _mockUsers.FirstOrDefault(u => u.Id == user.Id);
            if (existing != null)
            {
                var index = _mockUsers.IndexOf(existing);
                user.UpdatedAt = DateTime.Now;
                _mockUsers[index] = user;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<bool> DeleteUserAsync(int id)
    {
        lock (_lock)
        {
            var user = _mockUsers.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                _mockUsers.Remove(user);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsUsernameExistsAsync(string username, int? excludeId = null)
    {
        lock (_lock)
        {
            var exists = _mockUsers.Any(u => 
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && 
                (!excludeId.HasValue || u.Id != excludeId.Value));
            return Task.FromResult(exists);
        }
    }

    /// <inheritdoc/>
    public Task<bool> InitializeDefaultUsersAsync()
    {
        // Already initialized in static constructor
        return Task.FromResult(true);
    }
}
