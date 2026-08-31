namespace TimePay.Core.Models;

/// <summary>
/// Represents a supported currency in the TimePay system.
/// Designed to be extensible for future currency support.
/// </summary>
public class Currency
{
    public string Code { get; set; } = "PHP";
    public string Name { get; set; } = "Philippine Peso";
    public string Symbol { get; set; } = "₱";

    /// <summary>
    /// Predefined currencies for future extensibility.
    /// </summary>
    public static readonly Currency PHP = new() { Code = "PHP", Name = "Philippine Peso", Symbol = "₱" };
    public static readonly Currency USD = new() { Code = "USD", Name = "US Dollar", Symbol = "$" };
    public static readonly Currency EUR = new() { Code = "EUR", Name = "Euro", Symbol = "€" };
    public static readonly Currency JPY = new() { Code = "JPY", Name = "Japanese Yen", Symbol = "¥" };
    public static readonly Currency SGD = new() { Code = "SGD", Name = "Singapore Dollar", Symbol = "S$" };
    public static readonly Currency MYR = new() { Code = "MYR", Name = "Malaysian Ringgit", Symbol = "RM" };

    public static Currency[] GetAllCurrencies() => new[] { PHP, USD, EUR, JPY, SGD, MYR };

    public static Currency? FromCode(string code) =>
        GetAllCurrencies().FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
}
