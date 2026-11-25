using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

record Credentials(string? Email, string? Password);

sealed class Accounts(EnergyStore store)
{
    private readonly PasswordHasher<string> hasher = new();
    private readonly string dummyHash = new PasswordHasher<string>().HashPassword("", Guid.NewGuid().ToString());

    public async Task<IResult> Submit(Credentials input, HttpContext context, bool signup)
    {
        var email = input.Email?.Trim().ToLowerInvariant() ?? "";
        var password = input.Password ?? "";
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email ||
            password.Length is 0 or > 256)
            return Results.BadRequest(new { error = "Enter a valid email and password." });

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email)));
        if (signup)
        {
            if (password.Length < 12)
                return Results.BadRequest(new { error = "Use at least 12 characters for your password." });
            var inserted = store.Scalar("INSERT INTO accounts(email_key,password_hash) VALUES($key,$hash) ON CONFLICT(email_key) DO NOTHING RETURNING 1",
                ("$key",key), ("$hash",hasher.HashPassword(email,password)));
            if (inserted == null)
                return Results.Conflict(new { error = "This email is already registered. Try signing in." });
        }
        else
        {
            var hash = store.Scalar("SELECT password_hash FROM accounts WHERE email_key=$key", ("$key",key)) as string;
            if (hasher.VerifyHashedPassword(email, hash ?? dummyHash, password) == PasswordVerificationResult.Failed || hash == null)
                return Results.Json(new { error = "Email or password is incorrect." }, statusCode: 401);
        }
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, email) },
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(new ClaimsPrincipal(identity));
        return Results.Ok(new { email });
    }
}
