namespace Fractal;

public class LazyAsync<T>(Func<Task<T>> taskFactory) : Lazy<Task<T>>(
    () => Task.Factory.StartNew(taskFactory).Unwrap())
{
}
