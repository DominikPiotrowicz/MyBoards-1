# Chicago Taxi Analyzer

Aplikacja konsolowa .NET 8 do analizy danych o przejazdach taksówkami z Chicago z publicznej bazy danych BigQuery.

## Cel biznesowy

Pobranie i analiza 100 najdroższych przejazdów z roku 2023, które **nie zostały opłacone gotówką**.

## Wymagania

- .NET 8 SDK
- Konto Google Cloud z dostępem do BigQuery API
- Skonfigurowane credentials dla Google Cloud

## Struktura projektu

```
ChicagoTaxiAnalyzer/
├── ChicagoTaxiAnalyzer.csproj  # Definicja projektu
├── Program.cs                   # Główna logika aplikacji
├── TaxiTrip.cs                  # Model danych (record)
├── Queries.cs                   # Zapytania SQL
└── README.md                    # Ten plik
```

## Funkcjonalność

1. **Połączenie z BigQuery**: Aplikacja łączy się z publiczną bazą danych `bigquery-public-data.chicago_taxi_trips.taxi_trips`

2. **Zapytanie SQL**: Pobiera 100 najdroższych przejazdów z 2023 roku, które nie były opłacone gotówką

3. **Model danych**:
   - `TaxiTrip` - rekord C# z polami nullable (DateTime?, double?, decimal?, string?)
   - Zabezpieczenie przed błędami przy wartościach NULL z bazy

4. **Analiza w C#**:
   - Dla każdego przejazdu obliczany jest procent napiwku względem całkowitej kwoty
   - Wzór: `tipPercentage = (tips / (fare + tips)) * 100`

5. **Wyświetlanie**: Wyniki prezentowane w czytelnej tabeli konsolowej z:
   - Datą i czasem przejazdu
   - Dystansem (mile)
   - Opłatą bazową (fare)
   - Napiwkiem (tips)
   - Procentem napiwku
   - Metodą płatności

## Konfiguracja Google Cloud

### 1. Utworzenie projektu w Google Cloud

```bash
# Zaloguj się do Google Cloud
gcloud auth login

# Utwórz nowy projekt (opcjonalnie)
gcloud projects create chicago-taxi-analyzer

# Ustaw aktywny projekt
gcloud config set project chicago-taxi-analyzer
```

### 2. Włączenie BigQuery API

```bash
gcloud services enable bigquery.googleapis.com
```

### 3. Utworzenie i konfiguracja Service Account

```bash
# Utwórz service account
gcloud iam service-accounts create bigquery-reader \
    --display-name="BigQuery Reader"

# Przypisz rolę BigQuery User
gcloud projects add-iam-policy-binding chicago-taxi-analyzer \
    --member="serviceAccount:bigquery-reader@chicago-taxi-analyzer.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

# Wygeneruj klucz JSON
gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@chicago-taxi-analyzer.iam.gserviceaccount.com
```

### 4. Ustawienie zmiennej środowiskowej

**Windows (PowerShell):**
```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\bigquery-key.json"
```

**Windows (CMD):**
```cmd
set GOOGLE_APPLICATION_CREDENTIALS=C:\path\to\bigquery-key.json
```

**Linux/macOS:**
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/bigquery-key.json"
```

## Uruchomienie aplikacji

### Przywrócenie pakietów i uruchomienie

```bash
# Przejdź do folderu projektu
cd src/ChicagoTaxiAnalyzer

# Przywróć pakiety NuGet
dotnet restore

# Uruchom aplikację
dotnet run
```

### Budowanie Release

```bash
# Zbuduj wersję Release
dotnet build -c Release

# Uruchom zbudowaną aplikację
dotnet run -c Release
```

## Przykładowe wyjście

```
Chicago Taxi Analyzer - Analiza najdroższych przejazdów 2023
=================================================================

Łączenie z BigQuery...
Wykonywanie zapytania...

Pobrano 100 rekordów.

┌─────┬─────────────────────┬─────────────┬───────────┬───────────┬──────────────┬──────────────┐
│ Nr  │ Data i czas         │ Mile        │ Fare      │ Tips      │ % napiwku    │ Payment      │
├─────┼─────────────────────┼─────────────┼───────────┼───────────┼──────────────┼──────────────┤
│   1 │ 2023-03-15 14:30    │       45.20 │  $234.50  │   $45.50  │      16.25%  │ Credit Card  │
│   2 │ 2023-07-22 09:15    │       38.75 │  $198.00  │   $32.00  │      13.91%  │ Credit Card  │
...
└─────┴─────────────────────┴─────────────┴───────────┴───────────┴──────────────┴──────────────┘

Statystyki:
  Całkowita liczba przejazdów: 100
  Przejazdów z napiwkiem: 85
  Średnia opłata (fare): $156.32
  Średni napiwek (tips): $23.45
  Maksymalna opłata: $345.67

Analiza zakończona pomyślnie!
```

## Wymagania techniczne - realizacja

✅ **Użycie Google.Cloud.BigQuery.V2** - pakiet NuGet dodany w .csproj

✅ **Rekord TaxiTrip** - utworzony z polami nullable:
   - `DateTime? TripStartTimestamp`
   - `double? TripMiles`
   - `decimal? Fare`
   - `decimal? Tips`
   - `string? PaymentType`

✅ **Typy nullable** - wszystkie pola numeryczne to typy nullable, aby uniknąć błędów przy NULL-ach z bazy

✅ **Logika w C#** - procent napiwku obliczany w pętli `foreach` w C#, nie w SQL

✅ **Tabela w konsoli** - wyniki wyświetlane w czytelnej tabeli ze znakami box-drawing

✅ **Kod SQL w oddzielnej stałej** - zapytanie SQL znajduje się w klasie `Queries.cs`

## Rozwiązywanie problemów

### Błąd: "The Application Default Credentials are not available"

Upewnij się, że:
1. Zmienna `GOOGLE_APPLICATION_CREDENTIALS` jest ustawiona
2. Plik JSON z kluczem istnieje w podanej ścieżce
3. Plik JSON ma poprawne uprawnienia do odczytu

### Błąd: "Access Denied: BigQuery BigQuery: Permission denied"

Upewnij się, że service account ma przypisaną rolę `roles/bigquery.user`

### Błąd przy konwersji typów

Aplikacja używa typów nullable i bezpiecznej konwersji `as` oraz `Convert.ToDecimal` z obsługą null, aby zapobiec błędom.

## Licencja

Projekt edukacyjny - dane publiczne z BigQuery.
