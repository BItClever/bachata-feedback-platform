using Microsoft.AspNetCore.Mvc;

namespace BachataFeedback.Api.DTOs
{
    public class MultiPhotoUploadDto
    {
        [FromForm(Name = "files")]
        public List<IFormFile> Files { get; set; } = new();
    }
}