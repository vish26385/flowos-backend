namespace FlowOS.Api.Helpers
{
    public static class DateTimeUtc
    {
        public static DateTime ToUtc(DateTime dt)
        {
            // If client sent "local" DateTime, convert properly
            if (dt.Kind == DateTimeKind.Local) return dt.ToUniversalTime();

            // If client sent "unspecified", treat it as UTC (common in JSON)
            if (dt.Kind == DateTimeKind.Unspecified) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            return dt; // already UTC
        }

        public static DateTime UtcDayStart(DateTime dtUtc)
        {
            dtUtc = ToUtc(dtUtc);
            return new DateTime(dtUtc.Year, dtUtc.Month, dtUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        }
    }
}
