using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace EFCore.Dashboard.Authentication.Cookies;

internal sealed class DashboardCredentialValidator(
    DashboardCookieAuthenticationSettings settings,
    TimeProvider timeProvider)
{
    private const int MaximumTrackedClients = 10_000;
    private readonly ConcurrentDictionary<string, LoginAttempts> _attempts = new();
    private readonly LoginAttempts _overflowAttempts = new();
    private readonly object _newClientLock = new();

    public DashboardCredentialResult Validate(string username, string password, string clientKey)
    {
        var now = timeProvider.GetUtcNow();
        var attempts = GetAttempts(clientKey, now);

        lock (attempts)
        {
            attempts.LastSeen = now;
            var validUsername = FixedTimeEquals(username, settings.Username);
            var validPassword = FixedTimeEquals(password, settings.Password);
            if (validUsername & validPassword)
            {
                if (!ReferenceEquals(attempts, _overflowAttempts))
                    _attempts.TryRemove(clientKey, out _);
                return DashboardCredentialResult.Success;
            }

            if (attempts.LockedUntil > now)
                return DashboardCredentialResult.LockedOut;

            if (attempts.LockedUntil is not null)
            {
                attempts.FailedCount = 0;
                attempts.LockedUntil = null;
            }

            attempts.FailedCount++;
            if (attempts.FailedCount >= settings.MaxFailedAttempts)
                attempts.LockedUntil = now + settings.LockoutDuration;

            return DashboardCredentialResult.Invalid;
        }
    }

    private LoginAttempts GetAttempts(string clientKey, DateTimeOffset now)
    {
        if (_attempts.TryGetValue(clientKey, out var existing))
            return existing;

        lock (_newClientLock)
        {
            if (_attempts.TryGetValue(clientKey, out existing))
                return existing;

            if (_attempts.Count >= MaximumTrackedClients)
            {
                RemoveExpiredClients(now);
                if (_attempts.Count >= MaximumTrackedClients)
                    return _overflowAttempts;
            }

            var created = new LoginAttempts { LastSeen = now };
            _attempts[clientKey] = created;
            return created;
        }
    }

    private void RemoveExpiredClients(DateTimeOffset now)
    {
        var cutoff = now - settings.LockoutDuration - settings.LockoutDuration;
        foreach (var pair in _attempts)
        {
            lock (pair.Value)
            {
                if (pair.Value.LastSeen < cutoff && pair.Value.LockedUntil <= now)
                    _attempts.TryRemove(pair);
            }
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    private sealed class LoginAttempts
    {
        public int FailedCount { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }
}

internal enum DashboardCredentialResult
{
    Success,
    Invalid,
    LockedOut
}
