using Backup.Web.Api.Server.Brokers.DateTimes;
using System;

namespace Backup.Web.Api.Server.Brokers.DateTimes
{
    public class DateTimeBroker : IDateTimeBroker
    {
        public DateTimeOffset GetCurrentDateTime() => DateTimeOffset.UtcNow;
    }
}
