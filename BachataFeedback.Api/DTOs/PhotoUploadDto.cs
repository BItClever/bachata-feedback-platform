using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.DTOs
{
    public class PhotoUploadDto
    {
        [FromForm(Name = "file")]
        public IFormFile File { get; set; } = default!;
    }
}
