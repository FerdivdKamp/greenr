using System;
using System.Data;
using System.Threading.Tasks;

namespace CarbonTracker.API.Services;

public class UsersService : IUsersService
{
    private readonly IDbConnection _db;
    private readonly ITokenService _tokens;

    public UsersService(IDbConnection db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
        EnsureSchema();
    }

    private void EnsureSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS users (
              user_id UUID DEFAULT uuidv4() PRIMARY KEY,
              email TEXT UNIQUE NOT NULL,
              username TEXT UNIQUE NOT NULL,
              first_name TEXT,
              password_hash TEXT NOT NULL,
              created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
              updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS password_reset_tokens (
              token TEXT PRIMARY KEY,
              user_id UUID NOT NULL,
              expires_at TIMESTAMP NOT NULL,
              used_at TIMESTAMP,
              created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS refresh_tokens (
              token TEXT PRIMARY KEY,
              user_id UUID NOT NULL,
              expires_at TIMESTAMP NOT NULL,
              revoked_at TIMESTAMP,
              created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );";
        cmd.ExecuteNonQuery();
    }

    public async Task CreateUserAsync(string username, string email, string password)
    {
        // Unique check
        using (var check = _db.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM users WHERE lower(user_name)=lower(?) OR lower(email)=lower(?) LIMIT 1";
            AddP(check, username); AddP(check, email);
            using var r = check.ExecuteReader();
            if (r.Read()) throw new InvalidOperationException("Username or email already exists.");
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
        cmd.CommandText = @"SELECT user_id, username
                            FROM users
                            WHERE lower(username)=lower(?) OR lower(email)=lower(?)
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

    public async Task<(string AccessToken, string RefreshToken)> IssueTokensAsync(Guid userId, string username)
    {
        var access = _tokens.CreateAccessToken(userId, username);
        var refresh = _tokens.CreateRefreshToken();
        var expires = DateTime.UtcNow.AddDays(_tokens.RefreshDays());

        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT INTO refresh_tokens (token, user_id, expires_at) VALUES (?, ?, ?)";
        AddP(cmd, refresh); AddP(cmd, userId); AddP(cmd, expires);
        cmd.ExecuteNonQuery();

        return await Task.FromResult((access, refresh));
    }

    public async Task<string> CreatePasswordResetAsync(string email)
    {
        Guid? userId = null;
        using (var find = _db.CreateCommand())
        {
            find.CommandText = "SELECT user_id FROM users WHERE lower(email)=lower(?) LIMIT 1";
            AddP(find, email);
            using var r = find.ExecuteReader();
            if (r.Read()) userId = r.GetGuid(0);
        }

        // Always generate a token; only persist it if the user exists.
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        if (userId is null) return await Task.FromResult(token);

        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"INSERT INTO password_reset_tokens (token, user_id, expires_at)
                            VALUES (?, ?, ?)";
        AddP(cmd, token); AddP(cmd, userId.Value); AddP(cmd, DateTime.UtcNow.AddMinutes(30));
        cmd.ExecuteNonQuery();

        return await Task.FromResult(token);
    }

    public async Task ResetPasswordAsync(string token, string newPassword)
    {
        Guid? userId = null;
        DateTime? used = null;
        DateTime expires;

        using (var get = _db.CreateCommand())
        {
            get.CommandText = @"SELECT user_id, expires_at, used_at
                                FROM password_reset_tokens WHERE token=? LIMIT 1";
            AddP(get, token);
            using var r = get.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException("Invalid or expired token.");
            userId = r.GetGuid(0);
            expires = r.GetDateTime(1);
            used = r.IsDBNull(2) ? null : r.GetDateTime(2);
        }

        if (used != null || expires < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired token.");

        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);

        using var tx = _db.BeginTransaction();
        try
        {
            using (var upd = _db.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE users SET password_hash=?, updated_at=CURRENT_TIMESTAMP WHERE user_id=?";
                AddP(upd, hash); AddP(upd, userId!.Value);
                upd.ExecuteNonQuery();
            }

            using (var mark = _db.CreateCommand())
            {
                mark.Transaction = tx;
                mark.CommandText = "UPDATE password_reset_tokens SET used_at=CURRENT_TIMESTAMP WHERE token=?";
                AddP(mark, token);
                mark.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            throw;
        }

        await Task.CompletedTask;
    }

    private static void AddP(IDbCommand cmd, object? value)
    {
        var p = cmd.CreateParameter();
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
