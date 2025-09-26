using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Api.DTOs;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Nickname { get; set; }
}

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public DateTime? StartDancingDate { get; set; }
    public string? SelfAssessedLevel { get; set; }
    public string? Bio { get; set; }
    public string? DanceStyles { get; set; }
    public string? MainPhotoPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? DancerRole { get; set; }
}

public class UpdateProfileDto
{
    [Required]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Nickname { get; set; }

    public DateTime? StartDancingDate { get; set; }

    [MaxLength(50)]
    public string? SelfAssessedLevel { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? DanceStyles { get; set; }
    [MaxLength(10)]
    public string? DancerRole { get; set; }
}