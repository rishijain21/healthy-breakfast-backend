using Microsoft.AspNetCore.Http;

namespace HealthyBreakfastApp.Application.Interfaces
{
    public interface ISupabaseStorageService
    {
        Task<string> UploadImageAsync(IFormFile image, string filePath);
        Task<string> GetSignedUrlAsync(string filePath, int expiresInSeconds = 3600);
        Task DeleteImageAsync(string filePath);
    }
}
