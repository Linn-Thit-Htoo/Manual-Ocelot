using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Manual_Ocelot.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Manual_Ocelot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly AppSetting _setting;

        public TokenController(IOptions<AppSetting> options)
        {
            _setting = options.Value;
        }

        [HttpPost("GetToken")]
        public IActionResult GetToken()
        {
            try
            {
                List<Claim> claims = new List<Claim>()
                {
                    new Claim(JwtRegisteredClaimNames.Sub, "Lin Thit"),
                };
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_setting.Jwt.Key));
                var signIn = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                JwtSecurityToken? jwtSecurityToken = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(10),
                    signingCredentials: signIn
                );

                var token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
                return Ok(token);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
