using ApiConControllers.Auth;
using ApiConControllers.Entities;
using ApiConControllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApiConControllers.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService) : ControllerBase
    {
        /// <summary>
        /// Registra un usuario nuevo con el rol User y devuelve un JWT.
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

            var roles = await userManager.GetRolesAsync(user);
            var (token, expiresAt) = tokenService.CreateToken(user, roles);
            return Ok(new AuthResponse(token, expiresAt));
        }

        /// <summary>
        /// Valida credenciales y devuelve un JWT con los roles del usuario.
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

            var roles = await userManager.GetRolesAsync(user);
            var (token, expiresAt) = tokenService.CreateToken(user, roles);
            return Ok(new AuthResponse(token, expiresAt));
        }

        /// <summary>
        /// Asigna un rol existente a un usuario. Solo Admin.
        /// </summary>
        [HttpPost("add-role")]
        [Authorize(Roles = Roles.Admin)]
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
    }
}
