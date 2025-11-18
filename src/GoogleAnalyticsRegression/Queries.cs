namespace GoogleAnalyticsRegression;

/// <summary>
/// Zawiera zapytania SQL do BigQuery dla danych Google Analytics.
/// </summary>
public static class Queries
{
    /// <summary>
    /// Pobiera dane sesji z Google Analytics, które zakończyły się transakcją.
    ///
    /// Kolumny:
    /// - totals.pageviews: liczba odsłon stron w sesji (zmienna niezależna X)
    /// - totals.transactionRevenue: przychód z transakcji (zmienna zależna Y)
    ///
    /// Uwaga: transactionRevenue w BigQuery jest pomnożone przez 1,000,000.
    /// Zostanie to znormalizowane w kodzie C#.
    /// </summary>
    public const string GetSessionsWithTransactions = @"
        SELECT
            totals.pageviews,
            totals.transactionRevenue
        FROM
            `bigquery-public-data.google_analytics_sample.ga_sessions_20170801`
        WHERE
            totals.transactionRevenue IS NOT NULL
            AND totals.transactionRevenue > 0
            AND totals.pageviews IS NOT NULL
            AND totals.pageviews > 0
    ";
}
