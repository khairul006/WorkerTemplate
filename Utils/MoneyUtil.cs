using Jose;
using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace WorkerTemplate.Utils
{
    public static class MoneyUtil
    {
        private const int DecimalPlaces = 2;
        private const int Multiplier = 100;
        private static readonly Regex MoneyPattern = new(@"^\d+(\.\d+)?$", RegexOptions.Compiled);

        public static (int amount, int decimalPlaces) ToMinorUnits(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Money value cannot be null or empty");

                var trimmed = value.Trim();

                // reject anything that is not plain digits with optional dot
                if (!MoneyPattern.IsMatch(trimmed))
                    throw new ArgumentException($"Invalid money format: '{value}'. Expected plain decimal like 3.43 or 100");

                var parsed = decimal.Parse(trimmed, CultureInfo.InvariantCulture);
                var amount = (int)Math.Round(parsed * Multiplier, MidpointRounding.AwayFromZero);

                return (amount, DecimalPlaces);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ArithmeticException($"Failed to convert money value: {ex.Message}", ex);
            }
        }
    }
}