using FleetControl.Application.Common.Interfaces;

namespace FleetControl.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTime UtcNow => DateTime.UtcNow;
}
