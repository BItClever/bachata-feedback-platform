using BachataFeedback.Api.Data;
using BachataFeedback.Api.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Services;

public interface IUserService
{
    Task<IEnumerable<UserProfileDto>> GetActiveUsersAsync();
    Task<UserProfileDto?> GetUserByIdAsync(string id);
    Task<UserProfileDto?> UpdateUserProfileAsync(string userId, UpdateProfileDto model);
    Task<UserProfileDto?> GetUserProfileAsync(string userId);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public UserService(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IEnumerable<UserProfileDto>> GetActiveUsersAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .Select(u => new UserProfileDto
            {
                Id = u.Id,
                Email = u.Email!,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Nickname = u.Nickname,
                StartDancingDate = u.StartDancingDate,
                SelfAssessedLevel = u.SelfAssessedLevel,
                Bio = u.Bio,
                DanceStyles = u.DanceStyles,
                MainPhotoPath = u.MainPhotoPath,
                CreatedAt = u.CreatedAt,
                DancerRole = u.DancerRole
            })
            .ToListAsync();
    }

    public async Task<UserProfileDto?> GetUserByIdAsync(string id)
    {
        var user = await _context.Users
            .Where(u => u.Id == id && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null) return null;

        return new UserProfileDto
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
            DancerRole = user.DancerRole
        };
    }

    public async Task<UserProfileDto?> UpdateUserProfileAsync(string userId, UpdateProfileDto model)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Nickname = model.Nickname;
        user.StartDancingDate = model.StartDancingDate;
        user.SelfAssessedLevel = model.SelfAssessedLevel;
        user.Bio = model.Bio;
        user.DanceStyles = model.DanceStyles;

        // если добавлено поле роли:
        var dancerRoleProp = typeof(UpdateProfileDto).GetProperty("DancerRole");
        if (dancerRoleProp != null)
        {
            var dancerRoleValue = (string?)dancerRoleProp.GetValue(model);
            user.DancerRole = dancerRoleValue;
        }

        await _context.SaveChangesAsync();

        return new UserProfileDto
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
        };
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(string userId)
    {
        return await GetUserByIdAsync(userId);
    }
}