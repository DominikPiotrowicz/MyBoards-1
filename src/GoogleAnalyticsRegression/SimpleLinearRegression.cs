namespace GoogleAnalyticsRegression;

/// <summary>
/// Implementacja prostej regresji liniowej metodą najmniejszych kwadratów (OLS).
/// Nie używa ML.NET - całkowicie własna implementacja.
/// </summary>
public class SimpleLinearRegression
{
    private double _slope;      // współczynnik nachylenia (a)
    private double _intercept;  // wyraz wolny (b)
    private bool _isTrained;

    /// <summary>
    /// Współczynnik nachylenia prostej (a w równaniu y = ax + b).
    /// </summary>
    public double Slope => _slope;

    /// <summary>
    /// Wyraz wolny / przecięcie z osią Y (b w równaniu y = ax + b).
    /// </summary>
    public double Intercept => _intercept;

    /// <summary>
    /// Czy model został wytrenowany.
    /// </summary>
    public bool IsTrained => _isTrained;

    /// <summary>
    /// Trenuje model na podstawie danych wejściowych metodą najmniejszych kwadratów (OLS).
    /// </summary>
    /// <param name="data">Lista par (X, Y) do trenowania</param>
    /// <exception cref="ArgumentException">Gdy dane są puste lub niewystarczające</exception>
    public void Train(List<(double X, double Y)> data)
    {
        if (data == null || data.Count < 2)
        {
            throw new ArgumentException("Potrzeba co najmniej 2 punktów danych do trenowania modelu.", nameof(data));
        }

        int n = data.Count;

        // Krok 1: Oblicz średnie X i Y
        double meanX = data.Average(d => d.X);
        double meanY = data.Average(d => d.Y);

        // Krok 2: Oblicz licznik i mianownik dla wzoru OLS
        // slope = Σ((Xi - mean_x) * (Yi - mean_y)) / Σ((Xi - mean_x)²)

        double numerator = 0.0;   // licznik: Σ((Xi - mean_x) * (Yi - mean_y))
        double denominator = 0.0; // mianownik: Σ((Xi - mean_x)²)

        foreach (var (x, y) in data)
        {
            double xDiff = x - meanX;
            double yDiff = y - meanY;

            numerator += xDiff * yDiff;
            denominator += xDiff * xDiff;
        }

        // Krok 3: Oblicz slope (nachylenie)
        if (Math.Abs(denominator) < 1e-10)
        {
            throw new InvalidOperationException("Nie można obliczyć regresji - wszystkie wartości X są identyczne.");
        }

        _slope = numerator / denominator;

        // Krok 4: Oblicz intercept (wyraz wolny)
        // intercept = mean_y - slope * mean_x
        _intercept = meanY - (_slope * meanX);

        _isTrained = true;
    }

    /// <summary>
    /// Przewiduje wartość Y na podstawie wartości X używając wytrenowanego modelu.
    /// </summary>
    /// <param name="x">Wartość zmiennej niezależnej (np. liczba odsłon)</param>
    /// <returns>Przewidywana wartość zmiennej zależnej (np. przychód)</returns>
    /// <exception cref="InvalidOperationException">Gdy model nie został jeszcze wytrenowany</exception>
    public double Predict(double x)
    {
        if (!_isTrained)
        {
            throw new InvalidOperationException("Model musi być najpierw wytrenowany przed wykonaniem predykcji.");
        }

        // y = ax + b
        return (_slope * x) + _intercept;
    }

    /// <summary>
    /// Zwraca równanie prostej regresji w formacie czytelnym dla człowieka.
    /// </summary>
    public string GetEquation()
    {
        if (!_isTrained)
        {
            return "Model nie został jeszcze wytrenowany.";
        }

        string sign = _intercept >= 0 ? "+" : "-";
        double absIntercept = Math.Abs(_intercept);

        return $"y = {_slope:F6}x {sign} {absIntercept:F6}";
    }

    /// <summary>
    /// Oblicza współczynnik determinacji R² (goodness of fit).
    /// R² = 1 - (SS_res / SS_tot)
    /// Wartości bliskie 1 oznaczają dobre dopasowanie modelu.
    /// </summary>
    public double CalculateRSquared(List<(double X, double Y)> data)
    {
        if (!_isTrained)
        {
            throw new InvalidOperationException("Model musi być najpierw wytrenowany.");
        }

        if (data == null || data.Count == 0)
        {
            throw new ArgumentException("Dane nie mogą być puste.", nameof(data));
        }

        double meanY = data.Average(d => d.Y);

        double ssRes = 0.0; // suma kwadratów reszt
        double ssTot = 0.0; // suma kwadratów całkowitych

        foreach (var (x, y) in data)
        {
            double predicted = Predict(x);
            ssRes += Math.Pow(y - predicted, 2);
            ssTot += Math.Pow(y - meanY, 2);
        }

        if (Math.Abs(ssTot) < 1e-10)
        {
            return 0.0;
        }

        return 1.0 - (ssRes / ssTot);
    }
}
