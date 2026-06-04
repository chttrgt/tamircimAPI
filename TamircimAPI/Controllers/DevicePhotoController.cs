using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TamircimAPI.Services.Device;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/devices/{deviceId:int}/photos")]
    [Authorize]
    public class DevicePhotoController : ControllerBase
    {
        private readonly IDevicePhotoService _service;

        // Yükleme boyut tavanı (işlenmeden önceki ham dosya). Telefon fotoğrafı için bol.
        private const long MaxUploadBytes = 15 * 1024 * 1024; // 15 MB

        public DevicePhotoController(IDevicePhotoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> List(int deviceId)
        {
            var result = await _service.GetByDeviceAsync(deviceId);
            return Ok(result);
        }

        [HttpPost]
        [EnableRateLimiting("profile")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<IActionResult> Upload(
            int deviceId,
            IFormFile file,
            IFormFile thumbnail,
            [FromForm] int width = 0,
            [FromForm] int height = 0)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Dosya gönderilmedi." });
            if (thumbnail == null || thumbnail.Length == 0)
                return BadRequest(new { message = "Küçük resim gönderilmedi." });
            if (file.Length > MaxUploadBytes)
                return BadRequest(new { message = "Dosya çok büyük (en fazla 15 MB)." });

            await using var mainStream = file.OpenReadStream();
            await using var thumbStream = thumbnail.OpenReadStream();
            var result = await _service.UploadAsync(
                deviceId, mainStream, thumbStream, width, height, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpGet("{photoId:int}/file")]
        public async Task<IActionResult> GetFile(int deviceId, int photoId, [FromQuery] bool thumb = false)
        {
            var result = await _service.GetFileAsync(deviceId, photoId, thumb);
            if (result == null) return NotFound();

            // Auth'lu içerik → ara katmanlarda public cache'lenmesin
            Response.Headers.CacheControl = "private, max-age=86400";
            return File(result.Value.Stream, result.Value.ContentType);
        }

        [HttpDelete("{photoId:int}")]
        public async Task<IActionResult> Delete(int deviceId, int photoId)
        {
            await _service.DeleteAsync(deviceId, photoId);
            return NoContent();
        }
    }
}
