namespace GoogleAnalyticsRegression;

/// <summary>
/// Reprezentuje dane sesji Google Analytics dla regresji liniowej.
/// X = PageViews (zmienna niezależna)
/// Y = TransactionRevenue (zmienna zależna)
/// </summary>
public record SessionData(
    long? PageViews,
    long? TransactionRevenueRaw  // W danych GA jest pomnożone przez 1,000,000
)
{
    /// <summary>
    /// Zwraca znormalizowany przychód (podzielony przez 1,000,000).
    /// </summary>
    public double TransactionRevenue => TransactionRevenueRaw.HasValue
        ? TransactionRevenueRaw.Value / 1_000_000.0
        : 0.0;
}
