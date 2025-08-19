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

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
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

        if (result.Succeeded)
        {
            var token = await _tokenService.GenerateTokenAsync(user);

            return Ok(new
            {
                message = "User registered successfully",
                token = token,
                user = new UserProfileDto
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
                    CreatedAt = user.CreatedAt
                }
            });
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return BadRequest(ModelState);
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

        if (result.Succeeded)
        {
            var token = await _tokenService.GenerateTokenAsync(user);

            return Ok(new
            {
                message = "Login successful",
                token = token,
                user = new UserProfileDto
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
                    CreatedAt = user.CreatedAt
                }
            });
        }

        return Unauthorized(new { message = "Invalid login credentials" });
    }
}