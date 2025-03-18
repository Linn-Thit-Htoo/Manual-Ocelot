using System.Security.Claims;

namespace Manual_Ocelot.Services.TokenValidationServices;

public interface ITokenValidationService
{
    ClaimsPrincipal ValidateToken(string token);
}
