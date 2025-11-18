namespace ChicagoTaxiAnalyzer;

/// <summary>
/// Reprezentuje pojedynczy przejazd taksówką z Chicago.
/// Wszystkie pola numeryczne są nullable, ponieważ dane w BigQuery mogą zawierać NULL.
/// </summary>
public record TaxiTrip(
    DateTime? TripStartTimestamp,
    double? TripMiles,
    decimal? Fare,
    decimal? Tips,
    string? PaymentType
);
