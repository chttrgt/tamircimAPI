using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TamircimAPI.Authorization;
using TamircimAPI.Models.DTOs.Device;
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
        public async Task<IActionResult> List(
            int deviceId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 30;
            var result = await _service.GetByDeviceAsync(deviceId, page, pageSize);
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(Permissions.PhotosManage)]
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
        [HasPermission(Permissions.PhotosManage)]
        public async Task<IActionResult> Delete(int deviceId, int photoId)
        {
            await _service.DeleteAsync(deviceId, photoId);
            return NoContent();
        }

        // Toplu silme — DELETE gövdesi bazı ara katmanlarda düşürüldüğünden POST.
        [HttpPost("bulk-delete")]
        [HasPermission(Permissions.PhotosManage)]
        public async Task<IActionResult> BulkDelete(int deviceId, [FromBody] BulkDeletePhotosDTO body)
        {
            if (body?.Ids == null || body.Ids.Count == 0)
                return BadRequest(new { message = "Silinecek görsel seçilmedi." });

            var deleted = await _service.DeleteManyAsync(deviceId, body.Ids);
            return Ok(new { deleted });
        }
    }
}
