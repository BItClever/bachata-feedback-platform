using System.ComponentModel.DataAnnotations;

namespace BachataFeedback.Api.DTOs
{
    public class UpdatePhotoFocusDto
    {
        [Range(0, 100)]
        public float FocusX { get; set; }

        [Range(0, 100)]
        public float FocusY { get; set; }
    }
}