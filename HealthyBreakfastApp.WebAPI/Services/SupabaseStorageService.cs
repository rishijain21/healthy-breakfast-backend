using HealthyBreakfastApp.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HealthyBreakfastApp.WebAPI.Services
{
    public class SupabaseStorageService : ISupabaseStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _storageUrl;
        private readonly string _serviceRoleKey; // Use service role key to bypass RLS
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClient;
            _storageUrl = config["Supabase:StorageUrl"]!;
            _serviceRoleKey = config["Supabase:ServiceRoleKey"] ?? 
                throw new ArgumentNullException("Supabase:ServiceRoleKey is required for storage operations");
            _logger = logger;
        }

        public async Task<string> UploadImageAsync(IFormFile image, string filePath)
        {
            // Validate type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(image.ContentType?.ToLower()))
                throw new ArgumentException("Only JPEG, PNG, WebP allowed");

            // Validate size (15MB max - increased from 5MB)
            if (image.Length > 15 * 1024 * 1024)
                throw new ArgumentException("Max file size is 15MB");

            // Build multipart request
            using var content = new MultipartFormDataContent();
            using var fileStream = image.OpenReadStream();
            using var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(image.ContentType!);
            content.Add(fileContent, "file", image.FileName);

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_storageUrl}/object/meal-images/{filePath}")
            {
                Content = content
            };
            // Use service role key to bypass RLS policies for admin uploads
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
            request.Headers.Add("x-upsert", "true"); // overwrite if exists

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Supabase upload failed: {Error}", error);
                throw new InvalidOperationException($"Upload failed: {error}");
            }

            // Return just the file path (store path in DB, not full URL)
            return $"meal-images/{filePath}";
        }

        public async Task<string> GetSignedUrlAsync(string filePath, int expiresInSeconds = 3600)
        {
            // Ensure we only pass relative path inside bucket
            var cleanPath = filePath.StartsWith("meal-images/")
                ? filePath.Replace("meal-images/", "")
                : filePath;

            var requestUrl =
                $"https://beeqamwptmbpowswawfx.supabase.co/storage/v1/object/sign/meal-images/{cleanPath}";

            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { expiresIn = expiresInSeconds }),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _serviceRoleKey);

            request.Headers.Add("apikey", _serviceRoleKey);

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Supabase sign error: {content}");

            var json = JsonSerializer.Deserialize<JsonElement>(content);
            var signedUrl = json.GetProperty("signedURL").GetString();

            // Dynamic base URL - not hardcoded
            var baseUrl = _storageUrl.Replace("/storage/v1", "");
            return $"{baseUrl}/storage/v1{signedUrl}";
        }
    }
}
