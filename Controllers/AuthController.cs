using ApiConControllers.Auth;
using ApiConControllers.Data;
using ApiConControllers.Entities;
using ApiConControllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApiConControllers.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        AppDbContext db,
        IOptions<JwtSettings> jwtSettings) : ControllerBase
    {
        /// <summary>
        /// Registra un usuario nuevo con el rol User y devuelve access token + refresh token.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                return ValidationProblem(ModelState);
            }

            await userManager.AddToRoleAsync(user, Roles.User);

            return Ok(await IssueTokensAsync(user));
        }

        /// <summary>
        /// Valida credenciales y devuelve access token (JWT) + refresh token.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            // Mensaje genérico: no revelamos si el email existe o si falló la contraseña.
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            return Ok(await IssueTokensAsync(user));
        }

        /// <summary>
        /// Cambia un refresh token válido por un par nuevo (access + refresh). El refresh anterior queda revocado.
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
        {
            var hash = tokenService.HashToken(request.RefreshToken);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            // Token que nunca emitimos: rechazar.
            if (existing is null)
            {
                return Unauthorized(new { message = "Invalid refresh token." });
            }

            // Existe pero no sirve. Si ya estaba revocado, alguien reutiliza un token viejo
            // (posible robo): revocamos todos los tokens activos del usuario.
            if (!existing.IsActive)
            {
                if (existing.Revoked is not null)
                {
                    await RevokeAllActiveTokensAsync(existing.UserId);
                }
                return Unauthorized(new { message = "Invalid refresh token." });
            }

            var user = await userManager.FindByIdAsync(existing.UserId);
            if (user is null)
            {
                return Unauthorized(new { message = "Invalid refresh token." });
            }

            // Rotación: el token usado se revoca y se emite uno nuevo.
            return Ok(await IssueTokensAsync(user, replacing: existing));
        }

        /// <summary>
        /// Revoca un refresh token (logout). El access token ya emitido sigue válido hasta que expire.
        /// </summary>
        [HttpPost("revoke")]
        [AllowAnonymous]
        public async Task<IActionResult> Revoke(RevokeRequest request)
        {
            var hash = tokenService.HashToken(request.RefreshToken);
            var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (token is null || !token.IsActive)
            {
                return NotFound(new { message = "Token not found or already inactive." });
            }

            token.Revoked = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { message = "Refresh token revoked." });
        }

        /// <summary>
        /// Asigna un rol existente a un usuario. Solo Admin.
        /// </summary>
        [HttpPost("add-role")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> AddRole(AddRoleRequest request)
        {
            if (!await roleManager.RoleExistsAsync(request.Role))
            {
                return BadRequest(new { message = $"Role '{request.Role}' does not exist." });
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return NotFound(new { message = $"User '{request.Email}' not found." });
            }

            if (await userManager.IsInRoleAsync(user, request.Role))
            {
                return Conflict(new { message = "User already has this role." });
            }

            var result = await userManager.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new { message = $"Role '{request.Role}' added to '{request.Email}'." });
        }

        /// <summary>
        /// Asigna (o cambia) un claim del usuario: department o subscription. Solo Admin.
        /// </summary>
        [HttpPost("set-claim")]
        [Authorize(Policy = Policies.AdminOnly)]
        public async Task<IActionResult> SetClaim(SetClaimRequest request)
        {
            if (!AppClaimTypes.IsKnownType(request.Type))
            {
                return BadRequest(new { message = $"Unknown claim type '{request.Type}'." });
            }

            if (!AppClaimTypes.IsValid(request.Type, request.Value))
            {
                return BadRequest(new { message = $"Invalid value '{request.Value}' for claim '{request.Type}'." });
            }

            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                return NotFound(new { message = $"User '{request.Email}' not found." });
            }

            var result = await userManager.SetClaimAsync(user, request.Type, request.Value);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new { message = $"Claim '{request.Type}' = '{request.Value}' set for '{request.Email}'." });
        }

        // Crea el access token y un refresh token nuevo (guardando solo su hash).
        // Si "replacing" viene informado, ese token se revoca en el MISMO SaveChanges (rotación atómica).
        private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user, RefreshToken? replacing = null)
        {
            // Roles y claims se leen de la base en cada emisión (login y refresh): un cambio hecho
            // con add-role o set-claim llega al token nuevo, no al que ya está en circulación.
            var roles = await userManager.GetRolesAsync(user);
            var claims = await userManager.GetClaimsAsync(user);
            var (accessToken, accessExpiresAt) = tokenService.CreateAccessToken(user, roles, claims);

            var refreshToken = tokenService.CreateRefreshToken();
            var refreshHash = tokenService.HashToken(refreshToken);
            var refreshExpiresAt = DateTime.UtcNow.AddDays(jwtSettings.Value.RefreshTokenDays);

            if (replacing is not null)
            {
                replacing.Revoked = DateTime.UtcNow;
                replacing.ReplacedByTokenHash = refreshHash;
            }

            db.RefreshTokens.Add(new RefreshToken
            {
                TokenHash = refreshHash,
                UserId = user.Id,
                Created = DateTime.UtcNow,
                Expires = refreshExpiresAt
            });
            await db.SaveChangesAsync();

            return new AuthResponse(
                user.Id, user.Email!, roles,
                accessToken, accessExpiresAt,
                refreshToken, refreshExpiresAt);
        }

        // Si se detecta un token robado, se cancelan todos los activos de ese usuario:
        // el atacante y el usuario real tienen que volver a iniciar sesión.
        private async Task RevokeAllActiveTokensAsync(string userId)
        {
            var activeTokens = await db.RefreshTokens
                .Where(t => t.UserId == userId && t.Revoked == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.Revoked = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }
    }
}
