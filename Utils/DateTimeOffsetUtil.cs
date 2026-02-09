namespace WorkerTemplate.Utils
{
    public static class DateTimeOffsetUtil
    {
        /// <summary>
        /// Ensures the DateTimeOffset has milliseconds, defaults to .000 if missing
        /// </summary>
        public static DateTimeOffset EnsureMilliseconds(DateTimeOffset input)
        {
            // Take the input and round down to seconds, then add existing milliseconds
            var ms = input.Millisecond; // already 0-999
            return new DateTimeOffset(
                input.Year,
                input.Month,
                input.Day,
                input.Hour,
                input.Minute,
                input.Second,
                ms,          // keep original milliseconds (0 if none)
                input.Offset
            );
        }
    }
}
