using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HealthyBreakfastApp.WebAPI.Controllers
{
    /// <summary>
    /// Helper class for secure error responses that don't leak internal details in production
    /// </summary>
    public static class ControllerExtensions
    {
        /// <summary>
        /// Returns a 500 error response that logs full details internally but only exposes a generic message to clients
        /// </summary>
        public static ObjectResult ServerError(this ControllerBase controller, ILogger logger, Exception ex, string userMessage = "An error occurred")
        {
            // Always log full details internally
            logger.LogError(ex, userMessage);

            // Only expose generic message to clients - never expose ex.Message, stack traces, or internal details
            var response = new { message = userMessage };
            return controller.StatusCode(500, response);
        }

        /// <summary>
        /// Returns a 500 error response with custom user message
        /// </summary>
        public static ObjectResult ServerError(this ControllerBase controller, ILogger logger, Exception ex, string userMessage, string internalDetails)
        {
            // Log full details internally including the internal details
            logger.LogError(ex, "{UserMessage}. Internal: {InternalDetails}", userMessage, internalDetails);

            // Only expose generic message to clients
            var response = new { message = userMessage };
            return controller.StatusCode(500, response);
        }
    }
}
