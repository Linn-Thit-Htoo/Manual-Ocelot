﻿namespace Manual_Ocelot.Services.TokenValidationServices;

public class TokenValidationService : ITokenValidationService
{
    private readonly AppSetting _setting;

    public TokenValidationService(IOptions<AppSetting> options)
    {
        _setting = options.Value;
    }

    public ClaimsPrincipal ValidateToken(byte[] key, string token)
    {
        try
        {
            JwtSecurityTokenHandler tokenHandler = new();
            TokenValidationParameters parameters =
                new()
                {
                    RequireExpirationTime = true,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                };
            ClaimsPrincipal principal = tokenHandler.ValidateToken(
                token,
                parameters,
                out SecurityToken securityToken
            );

            return principal;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
