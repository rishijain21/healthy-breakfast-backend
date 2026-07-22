using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Sovva.Application.Tests.Helpers;

/// <summary>
/// Lightweight in-memory ISender implementation for testing CQRS Facades and Handlers.
/// </summary>
public class TestMediatRSender : ISender
{
    private readonly Dictionary<Type, Func<object, CancellationToken, Task<object?>>> _handlers = new();

    public void Register<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler) where TRequest : IRequest<TResponse>
    {
        _handlers[typeof(TRequest)] = async (req, ct) => await handler.Handle((TRequest)req, ct);
    }

    public void Register<TRequest>(IRequestHandler<TRequest> handler) where TRequest : IRequest
    {
        _handlers[typeof(TRequest)] = async (req, ct) => { await handler.Handle((TRequest)req, ct); return null; };
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(request.GetType(), out var handler))
        {
            var res = await handler(request, cancellationToken);
            return (TResponse)res!;
        }
        throw new InvalidOperationException($"No handler registered in TestMediatRSender for {request.GetType().Name}");
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        if (_handlers.TryGetValue(request.GetType(), out var handler))
        {
            await handler(request, cancellationToken);
            return;
        }
        throw new InvalidOperationException($"No handler registered in TestMediatRSender for {request.GetType().Name}");
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(request.GetType(), out var handler))
        {
            return handler(request, cancellationToken);
        }
        throw new InvalidOperationException($"No handler registered in TestMediatRSender for {request.GetType().Name}");
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
