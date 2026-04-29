using Microsoft.AspNetCore.Identity;

namespace AirrostiDemo.Server.Data
{
    /// <summary>
    /// The application's user entity, persisted by ASP.NET Core Identity.
    /// </summary>
    /// <remarks>
    /// We deliberately inherit from <see cref="IdentityUser"/> without adding
    /// any extra properties yet — Identity already gives us the email, the
    /// password hash, the security stamp, lockout state, and a string
    /// primary key (the GUID-ish <c>Id</c>). Keeping our user equal to the
    /// stock Identity user makes future migrations trivial: when this demo
    /// grows columns like "DisplayName" or "PreferredDrugList", they go
    /// here and EF Core picks them up automatically.
    /// <para>
    /// The <c>Id</c> from this class is what gets embedded as the JWT's
    /// <c>sub</c> claim by <c>JwtTokenService</c>, and that same value is
    /// what controllers later read out of <c>ClaimTypes.NameIdentifier</c>
    /// to scope queries to the calling user.
    /// </para>
    /// </remarks>
    public class AppUser : IdentityUser
    {
    }
}
