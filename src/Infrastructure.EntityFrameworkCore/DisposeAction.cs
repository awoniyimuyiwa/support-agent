namespace Infrastructure.EntityFrameworkCore;

public class DisposeAction(Action action) : IDisposable
{
    public void Dispose()
    {
        action();
        GC.SuppressFinalize(this);
    }
}


