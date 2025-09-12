using BachataFeedback.Api.Services;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _roleManager = roleManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = new User
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            Nickname = model.Nickname
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return BadRequest(ModelState);
        }

        // Назначаем базовую роль User
        if (await _roleManager.RoleExistsAsync("User"))
        {
            await _userManager.AddToRoleAsync(user, "User");
        }

        var token = await _tokenService.GenerateTokenAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        // Permissions уже вшиты в токен; дополнительно собирать — не обязательно, но вернем для UI
        var permissions = new List<string>(); // можно не трогать, если UI будет читать из токена; но для простоты возвращаем пусто

        return Ok(new
        {
            message = "User registered successfully",
            token,
            user = new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Nickname = user.Nickname,
                StartDancingDate = user.StartDancingDate,
                SelfAssessedLevel = user.SelfAssessedLevel,
                Bio = user.Bio,
                DanceStyles = user.DanceStyles,
                MainPhotoPath = user.MainPhotoPath,
                CreatedAt = user.CreatedAt,
                DancerRole = user.DancerRole,
                Roles = roles,
                Permissions = permissions
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid login credentials" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid login credentials" });

        var token = await _tokenService.GenerateTokenAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            message = "Login successful",
            token,
            user = new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Nickname = user.Nickname,
                StartDancingDate = user.StartDancingDate,
                SelfAssessedLevel = user.SelfAssessedLevel,
                Bio = user.Bio,
                DanceStyles = user.DanceStyles,
                MainPhotoPath = user.MainPhotoPath,
                CreatedAt = user.CreatedAt,
                DancerRole = user.DancerRole,
                Roles = roles,
                Permissions = Array.Empty<string>() // по желанию можно собрать из RoleManager, но токен уже содержит
            }
        });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Nickname = user.Nickname,
            StartDancingDate = user.StartDancingDate,
            SelfAssessedLevel = user.SelfAssessedLevel,
            Bio = user.Bio,
            DanceStyles = user.DanceStyles,
            MainPhotoPath = user.MainPhotoPath,
            CreatedAt = user.CreatedAt,
            DancerRole = user.DancerRole,
            Roles = roles,
            Permissions = Array.Empty<string>()
        });
    }

    [HttpGet("permissions")]
    public IActionResult MyPermissions()
    {
        // Читаем claims "permission" из текущего токена
        var perms = User?.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .Distinct()
            .ToArray() ?? Array.Empty<string>();

        var roles = User?.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .Distinct()
            .ToArray() ?? Array.Empty<string>();

        return Ok(new { roles, permissions = perms });
    }
}