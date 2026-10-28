using System;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CarbonTracker.API.Services.Interfaces;

namespace CarbonTracker.API.Services;


public class UsersService : IUsersService
{
    private readonly IDbConnection _db;
    private readonly ITokenService _tokens;
    private readonly IPasswordResetDelivery _delivery;
    private readonly ILogger<UsersService> _logger;

    public UsersService(IDbConnection db, ITokenService tokens, IPasswordResetDelivery delivery, ILogger<UsersService> logger)
    {
        _db = db;
        _tokens = tokens;
        _delivery = delivery;
        _logger = logger;
    }

    public async Task CreateUserAsync(string username, string email, string password)
    {
        // Unique check
        using (var check = _db.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM users WHERE lower(user_name)=lower(?) OR lower(email)=lower(?) LIMIT 1";
            AddP(check, username); AddP(check, email);
            using var r = check.ExecuteReader();
            if (r.Read()) throw new DuplicateAccountException();
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO users (user_id, email, user_name, password_hash)
                            VALUES (uuidv4(), ?, ?, ?)";
        AddP(cmd, email); AddP(cmd, username); AddP(cmd, hash);
        cmd.ExecuteNonQuery();
        await Task.CompletedTask;
    }

    public async Task<(Guid UserId, string Username)?> FindByIdentifierAsync(string identifier)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"SELECT user_id, user_name
                            FROM users
                            WHERE lower(user_name)=lower(?) OR lower(email)=lower(?)
                            LIMIT 1";
        AddP(cmd, identifier); AddP(cmd, identifier);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var userId = r.GetGuid(0);
        var username = r.GetString(1);
        return await Task.FromResult<(Guid, string)?>((userId, username));
    }

    public async Task<bool> VerifyPasswordAsync(Guid userId, string password)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT password_hash FROM users WHERE user_id = ? LIMIT 1";
        AddP(cmd, userId);
        var hash = cmd.ExecuteScalar() as string;
        if (string.IsNullOrEmpty(hash)) return false;
        return await Task.FromResult(BCrypt.Net.BCrypt.Verify(password, hash));
    }

    // Store only digests. Prefixing also deliberately invalidates legacy plaintext
    // rows: old credentials cannot match the new lookup format.
    private static string Digest(string token) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(Guid userId, string username)
    {
        var access = _tokens.CreateAccessToken(userId, username);
        var refresh = _tokens.CreateRefreshToken();
        InsertRefresh(userId, refresh, null);
        return Task.FromResult((access, refresh));
    }

    private void InsertRefresh(Guid userId, string token, IDbTransaction? tx)
    {
        using var cmd = _db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO refresh_tokens (token, user_id, expires_at) VALUES (?, ?, ?)";
        AddP(cmd, Digest(token)); AddP(cmd, userId); AddP(cmd, DateTime.UtcNow.AddDays(_tokens.RefreshDays()));
        cmd.ExecuteNonQuery();
    }

    public Task<(string AccessToken, string RefreshToken)> RefreshAsync(string refreshToken)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            Guid userId;
            using (var consume = _db.CreateCommand())
            {
                consume.Transaction = tx;
                consume.CommandText = @"UPDATE refresh_tokens SET revoked_at=?
                    WHERE token=? AND revoked_at IS NULL AND expires_at>?
                    RETURNING user_id";
                var now = DateTime.UtcNow;
                AddP(consume, now); AddP(consume, Digest(refreshToken)); AddP(consume, now);
                using var reader = consume.ExecuteReader();
                if (!reader.Read()) throw new InvalidRefreshTokenException();
                userId = reader.GetGuid(0);
            }
            string username;
            using (var user = _db.CreateCommand())
            {
                user.Transaction = tx;
                user.CommandText = "SELECT user_name FROM users WHERE user_id=?";
                AddP(user, userId);
                username = user.ExecuteScalar() as string ?? throw new InvalidRefreshTokenException();
            }
            var access = _tokens.CreateAccessToken(userId, username);
            var refresh = _tokens.CreateRefreshToken();
            InsertRefresh(userId, refresh, tx);
            tx.Commit();
            return Task.FromResult((access, refresh));
        }
        catch (DbException ex) when (IsWriteConflict(ex))
        {
            // DuckDB uses optimistic concurrency. A competing consume wins;
            // disposal rolls this transaction back, including any replacement.
            throw new InvalidRefreshTokenException();
        }
    }

    public async Task RevokeAsync(string refreshToken)
    {
        // Retry conflicting writes so revocation of the supplied token completes.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = "UPDATE refresh_tokens SET revoked_at=? WHERE token=? AND revoked_at IS NULL";
                AddP(cmd, DateTime.UtcNow); AddP(cmd, Digest(refreshToken));
                cmd.ExecuteNonQuery();
                return;
            }
            catch (DbException ex) when (IsWriteConflict(ex) && attempt < 3)
            {
                await Task.Delay(10);
            }
        }
    }

    public async Task CreatePasswordResetAsync(string email)
    {
        // Generate for both paths; HTTP responses never contain the credential.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Guid userId;
        string storedEmail;
        using (var find = _db.CreateCommand())
        {
            find.CommandText = "SELECT user_id, email FROM users WHERE lower(email)=lower(?) LIMIT 1";
            AddP(find, email);
            using var r = find.ExecuteReader();
            if (!r.Read()) return;
            userId = r.GetGuid(0);
            storedEmail = r.GetString(1);
        }
        var expires = DateTime.UtcNow.AddMinutes(30);
        using (var cmd = _db.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO password_reset_tokens (token, user_id, expires_at) VALUES (?, ?, ?)";
            AddP(cmd, Digest(token)); AddP(cmd, userId); AddP(cmd, expires);
            cmd.ExecuteNonQuery();
        }
        try
        {
            await _delivery.DeliverAsync(storedEmail, token, expires);
        }
        catch (Exception)
        {
            // Provider exceptions can contain email addresses or token-bearing
            // URLs. Do not log the exception or reveal delivery failure to callers.
            _logger.LogError("Password reset delivery failed.");
        }
    }

    public Task ResetPasswordAsync(string token, string newPassword)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            Guid userId;
            using (var consume = _db.CreateCommand())
            {
                consume.Transaction = tx;
                consume.CommandText = @"UPDATE password_reset_tokens SET used_at=?
                    WHERE token=? AND used_at IS NULL AND expires_at>? RETURNING user_id";
                var now = DateTime.UtcNow;
                AddP(consume, now); AddP(consume, Digest(token)); AddP(consume, now);
                using var r = consume.ExecuteReader();
                if (!r.Read()) throw new InvalidResetTokenException();
                userId = r.GetGuid(0);
            }
            var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            using (var upd = _db.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE users SET password_hash=?, updated_at=CURRENT_TIMESTAMP WHERE user_id=?";
                AddP(upd, hash); AddP(upd, userId);
                upd.ExecuteNonQuery();
            }
            // A successful reset invalidates all outstanding recovery credentials
            // and refresh sessions, including those from other devices.
            foreach (var sql in new[] {
                "UPDATE password_reset_tokens SET used_at=? WHERE user_id=? AND used_at IS NULL",
                "UPDATE refresh_tokens SET revoked_at=? WHERE user_id=? AND revoked_at IS NULL" })
            {
                using var cmd = _db.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                AddP(cmd, DateTime.UtcNow); AddP(cmd, userId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (DbException ex) when (IsWriteConflict(ex))
        {
            throw new InvalidResetTokenException();
        }
        return Task.CompletedTask;
    }

    private static bool IsWriteConflict(DbException ex) =>
        ex.Message.Contains("Conflict on", StringComparison.OrdinalIgnoreCase);

    private static void AddP(IDbCommand cmd, object? value)
    {
        var p = cmd.CreateParameter();
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
