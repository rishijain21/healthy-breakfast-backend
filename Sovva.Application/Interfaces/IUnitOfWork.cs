namespace Sovva.Application.Interfaces
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task<int> SaveChangesAsync();
        Task ExecuteInTransactionAsync(Func<Task> action);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
    }
}
