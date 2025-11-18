namespace StackOverflowAnalyzer;

/// <summary>
/// Reprezentuje statystyki szybkości odpowiedzi dla danego języka/tagu na Stack Overflow.
/// </summary>
/// <param name="Tag">Tag języka programowania (np. "c#", "python", "rust")</param>
/// <param name="AvgMinutesToAnswer">Średni czas w minutach od zadania pytania do udzielenia zaakceptowanej odpowiedzi</param>
/// <param name="TotalSolvedQuestions">Całkowita liczba rozwiązanych pytań (z zaakceptowaną odpowiedzią)</param>
public record LanguageStats(
    string Tag,
    double AvgMinutesToAnswer,
    long TotalSolvedQuestions
);
