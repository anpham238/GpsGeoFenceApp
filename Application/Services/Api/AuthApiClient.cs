using System.Net.Http.Json;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services.Api;

public sealed class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(HttpClient http) => _http = http;

    public async Task<LoginResult?> LoginAsync(
        string identifier, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Identifier = identifier, Password = password }, ct);

        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<LoginResult>(ct);
    }

    public async Task<bool> RegisterAsync(
        string username, string mail, string password,
        string? phoneNumber = null, Stream? avatarStream = null, string? avatarFileName = null,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(username), "Username");
        form.Add(new StringContent(mail), "Mail");
        form.Add(new StringContent(password), "Password");
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            form.Add(new StringContent(phoneNumber), "PhoneNumber");
        if (avatarStream is not null && !string.IsNullOrWhiteSpace(avatarFileName))
            form.Add(new StreamContent(avatarStream), "Avatar", avatarFileName);

        var resp = await _http.PostAsync("/api/v1/auth/register", form, ct);
        return resp.IsSuccessStatusCode;
    }

    // ── Token storage helpers ──────────────────────────────────────────────
    public static void SaveSession(LoginResult result)
    {
        Preferences.Set("auth_token", result.Token);
        Preferences.Set("auth_user_id", result.UserId.ToString());
        Preferences.Set("auth_username", result.Username);
        Preferences.Set("auth_avatar_url", result.AvatarUrl ?? "");
    }

    public static void ClearSession()
    {
        Preferences.Remove("auth_token");
        Preferences.Remove("auth_user_id");
        Preferences.Remove("auth_username");
        Preferences.Remove("auth_avatar_url");
    }

    public static bool IsLoggedIn() =>
        !string.IsNullOrEmpty(Preferences.Get("auth_token", ""));

    public static Guid GetCurrentUserId()
    {
        var id = Preferences.Get("auth_user_id", "");
        return Guid.TryParse(id, out var g) ? g : Guid.Empty;
    }

    public static string GetCurrentUsername() =>
        Preferences.Get("auth_username", "");

    public static string GetCurrentAvatarUrl() =>
        Preferences.Get("auth_avatar_url", "");
}

public sealed class LoginResult
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string Mail { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTime ExpiresAt { get; set; }
}
