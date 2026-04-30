using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MapApi.Contracts;
using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MapApi.Services;

public class AuthService(AppDb db, IWebHostEnvironment env, IConfiguration config)
{
    private readonly SymmetricSecurityKey _jwtKey = new(
        Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? "CHANGE_THIS_SECRET_KEY_MIN_32_CHARS_PLEASE"));

    public async Task<IResult> RegisterAsync(HttpContext ctx)
    {
        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest(new { error = "Yêu cầu multipart/form-data" });

        var form = await ctx.Request.ReadFormAsync();
        var username = form["Username"].ToString().Trim();
        var mail     = form["Mail"].ToString().Trim().ToLowerInvariant();
        var password = form["Password"].ToString();
        var phone    = form["PhoneNumber"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(mail) ||
            string.IsNullOrWhiteSpace(password))
            return Results.BadRequest(new { error = "Username, Mail và Password là bắt buộc" });

        if (await db.Users.AnyAsync(u => u.Username == username))
            return Results.Conflict(new { error = "Username đã tồn tại" });
        if (await db.Users.AnyAsync(u => u.Mail == mail))
            return Results.Conflict(new { error = "Email đã được đăng ký" });

        var avatarUrl = "default-avatar.png";
        var avatarFile = form.Files.GetFile("Avatar");
        if (avatarFile is { Length: > 0 })
            avatarUrl = await SaveAvatarAsync(avatarFile) ?? avatarUrl;

        var user = new Users
        {
            UserId       = Guid.NewGuid(),
            Username     = username,
            Mail         = mail,
            PhoneNumber  = string.IsNullOrWhiteSpace(phone) ? null : phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            AvatarUrl    = avatarUrl,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Results.Created("/api/v1/auth/me", new { user.UserId, user.Username, user.Mail, user.AvatarUrl });
    }

    public async Task<IResult> LoginAsync(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Identifier và Password là bắt buộc" });

        var identifier = req.Identifier.Trim();
        Users? user = identifier.Contains('@')
            ? await db.Users.FirstOrDefaultAsync(u => u.Mail == identifier.ToLowerInvariant() && u.IsActive)
            : await db.Users.FirstOrDefaultAsync(u => u.Username == identifier && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Results.Unauthorized();

        return Results.Ok(new
        {
            Token        = GenerateToken(user),
            UserId       = user.UserId,
            user.Username,
            user.Mail,
            user.AvatarUrl,
            user.PlanType,
            user.ProExpiryDate,
            ExpiresAt    = DateTime.UtcNow.AddHours(24)
        });
    }

    public async Task<IResult> GetProfileAsync(Guid userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
        if (user is null) return Results.NotFound(new { error = "User not found" });

        return Results.Ok(new
        {
            user.UserId, user.Username, user.Mail, user.PhoneNumber,
            user.AvatarUrl, user.PlanType, user.ProExpiryDate, user.CreatedAt
        });
    }

    public async Task<IResult> UpdateProfileAsync(Guid userId, HttpContext ctx)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
        if (user is null) return Results.NotFound(new { error = "User not found" });

        if (!ctx.Request.HasFormContentType)
            return Results.BadRequest(new { error = "Yêu cầu multipart/form-data" });

        var form     = await ctx.Request.ReadFormAsync();
        var username = form["Username"].ToString().Trim();
        var phone    = form["PhoneNumber"].ToString().Trim();

        if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
        {
            if (await db.Users.AnyAsync(u => u.Username == username && u.UserId != userId))
                return Results.Conflict(new { error = "Username đã tồn tại" });
            user.Username = username;
        }

        if (!string.IsNullOrWhiteSpace(phone))
            user.PhoneNumber = phone;

        var avatarFile = form.Files.GetFile("Avatar");
        if (avatarFile is { Length: > 0 })
        {
            var saved = await SaveAvatarAsync(avatarFile);
            if (saved is not null) user.AvatarUrl = saved;
        }

        await db.SaveChangesAsync();
        return Results.Ok(new { user.UserId, user.Username, user.Mail, user.PhoneNumber, user.AvatarUrl, user.PlanType });
    }

    public async Task<IResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return Results.BadRequest(new { error = "Cần nhập mật khẩu hiện tại và mật khẩu mới." });

        if (req.NewPassword.Length < 6)
            return Results.BadRequest(new { error = "Mật khẩu mới phải có ít nhất 6 ký tự." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
        if (user is null) return Results.NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Results.BadRequest(new { error = "Mật khẩu hiện tại không đúng." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true, message = "Đổi mật khẩu thành công." });
    }

    public async Task<IResult> UpgradeAsync(Guid userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
        if (user is null) return Results.NotFound();

        user.PlanType      = "PRO";
        user.ProExpiryDate = DateTime.UtcNow.AddDays(30);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true, PlanType = user.PlanType, ProExpiryDate = user.ProExpiryDate });
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private string GenerateToken(Users user)
    {
        var creds = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Mail)
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string?> SaveAvatarAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png")) return null;

        var dir = Path.Combine(env.WebRootPath, "avatars");
        Directory.CreateDirectory(dir);
        var safeName = $"{Guid.NewGuid():N}{ext}";
        await using var fs = File.Create(Path.Combine(dir, safeName));
        await file.CopyToAsync(fs);
        return $"/avatars/{safeName}";
    }
}
