using SharedKernel.DependencyInjection;
using SharedKernel.Time;

namespace Infrastructure.Time;

internal sealed class DateTimeProvider : IDateTimeProvider, IInjectSingleton
{
    public DateTime UtcNow => DateTime.UtcNow;
}
