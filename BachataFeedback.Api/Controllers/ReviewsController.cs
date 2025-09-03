using BachataFeedback.Api.Services;
using BachataFeedback.Core.DTOs;
using BachataFeedback.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BachataFeedback.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly UserManager<User> _userManager;

    public ReviewsController(IReviewService reviewService, UserManager<User> userManager)
    {
        _reviewService = reviewService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviews()
    {
        var requestorId = _userManager.GetUserId(User);
        var currentUserId = _userManager.GetUserId(User);
        var reviews = await _reviewService.GetAllReviewsAsync(requestorId);
        return Ok(reviews);
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetUserReviews(string userId)
    {
        var requestorId = _userManager.GetUserId(User);
        var currentUserId = _userManager.GetUserId(User);
        var revieweeSettings = await _userManager.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Settings)
            .FirstOrDefaultAsync();
        var reviews = await _reviewService.GetUserReviewsAsync(userId, requestorId);

        bool isOwner = currentUserId == userId;
        if (!isOwner && revieweeSettings != null)
        {
            if (!revieweeSettings.ShowRatingsToOthers)
            {
                foreach (var r in reviews)
                {
                    r.LeadRatings = null;
                    r.FollowRatings = null;
                }
            }
            if (!revieweeSettings.ShowTextReviewsToOthers)
            {
                foreach (var r in reviews)
                {
                    r.TextReview = null;
                }
            }
        }
        return Ok(reviews);
    }

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
            return Unauthorized();

        var review = await _reviewService.CreateReviewAsync(currentUser.Id, model);
        return CreatedAtAction(nameof(GetReviews), review);
    }
}