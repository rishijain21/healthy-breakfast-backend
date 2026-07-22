using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Sovva.Application.Common.Behaviors
{
    public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

        public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("Handling MediatR Request: {Name} {@Request}", requestName, request);

            var timer = Stopwatch.StartNew();

            try
            {
                var response = await next();
                timer.Stop();

                var elapsedMilliseconds = timer.ElapsedMilliseconds;
                if (elapsedMilliseconds > 500)
                {
                    _logger.LogWarning("Long Running MediatR Request: {Name} ({ElapsedMilliseconds} ms) {@Request}",
                        requestName, elapsedMilliseconds, request);
                }
                else
                {
                    _logger.LogInformation("Completed MediatR Request: {Name} in {ElapsedMilliseconds} ms",
                        requestName, elapsedMilliseconds);
                }

                return response;
            }
            catch (Exception ex)
            {
                timer.Stop();
                _logger.LogError(ex, "Request Failure MediatR Request: {Name} after {ElapsedMilliseconds} ms {@Request}",
                    requestName, timer.ElapsedMilliseconds, request);
                throw;
            }
        }
    }
}
