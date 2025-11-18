# Austin Bikeshare Speed Analyzer

Aplikacja konsolowa .NET 8 analizująca szybkość przejazdów rowerowych w Austin z wykorzystaniem **funkcji geograficznych BigQuery** (Geography API).

## Cel biznesowy

**Pytanie**: Jakie są najszybsze przejazdy rowerowe w Austin? Czy dane zawierają anomalie?

Aplikacja:
- Oblicza rzeczywisty dystans między stacjami (w linii prostej)
- Oblicza średnią prędkość na podstawie dystansu i czasu
- Wykrywa anomalie (absurdalne prędkości > 50 km/h)
- Analizuje Top 20 najszybszych przejazdów z 2023 roku

## Wymagania

- .NET 8 SDK
- Konto Google Cloud z dostępem do BigQuery API
- Skonfigurowane Google Cloud credentials

## Struktura projektu

```
AustinBikeshareAnalyzer/
├── AustinBikeshareAnalyzer.csproj  # Definicja projektu .NET 8
├── Program.cs                       # Główna logika i wyświetlanie
├── BikeshareService.cs              # Serwis BigQuery z funkcjami geograficznymi
├── BikeTrip.cs                      # Model danych (rekord)
└── README.md                        # Ten plik
```

## Funkcjonalność

### 1. Źródła danych BigQuery

**Tabela podróży** (brak współrzędnych!):
```
bigquery-public-data.austin_bikeshare.bikeshare_trips
```
Kolumny: `trip_id`, `start_station_id`, `end_station_id`, `duration_minutes`, `start_time`

**Tabela stacji** (zawiera lokalizacje):
```
bigquery-public-data.austin_bikeshare.bikeshare_stations
```
Kolumny: `station_id`, `name`, `latitude`, `longitude`, `location`

### 2. Funkcje geograficzne BigQuery

#### ST_GEOGPOINT(longitude, latitude)

**Tworzy punkt geograficzny** z koordynatów.

```sql
ST_GEOGPOINT(-97.7431, 30.2672)
```

**⚠️ WAŻNA KOLEJNOŚĆ**: `ST_GEOGPOINT(longitude, latitude)`
- Pierwszy parametr: **longitude** (długość geograficzna, X, E/W)
- Drugi parametr: **latitude** (szerokość geograficzna, Y, N/S)

**Typowy błąd**:
```sql
-- ❌ NIEPOPRAWNIE
ST_GEOGPOINT(latitude, longitude)  -- ODWROTNA kolejność!

-- ✅ POPRAWNIE
ST_GEOGPOINT(longitude, latitude)
```

#### ST_DISTANCE(point1, point2)

**Oblicza dystans geodezyjny** między dwoma punktami geograficznymi.

```sql
ST_DISTANCE(
    ST_GEOGPOINT(-97.7431, 30.2672),  -- Punkt A
    ST_GEOGPOINT(-97.7456, 30.2689)   -- Punkt B
)
```

**Zwraca**: dystans w **metrach** (zawsze dodatnia liczba)

**Algorytm**: Oblicza najkrótszą odległość po powierzchni Ziemi (geodezja), nie w linii prostej przez Ziemię.

**Przykład**:
- Punkt A: Austin Downtown (-97.7431, 30.2672)
- Punkt B: University of Texas (-97.7456, 30.2689)
- Dystans: ~242 metry

### 3. Zapytanie SQL

#### Struktura zapytania:

```sql
WITH trip_distances AS (
    SELECT
        t.trip_id,
        t.duration_minutes,
        start_station.name AS start_station_name,
        end_station.name AS end_station_name,

        -- Tworzenie punktów geograficznych
        ST_GEOGPOINT(start_station.longitude, start_station.latitude) AS start_point,
        ST_GEOGPOINT(end_station.longitude, end_station.latitude) AS end_point

    FROM bikeshare_trips t

    -- JOIN ze stacją startową
    INNER JOIN bikeshare_stations start_station
    ON t.start_station_id = start_station.station_id

    -- JOIN ze stacją końcową
    INNER JOIN bikeshare_stations end_station
    ON t.end_station_id = end_station.station_id

    WHERE EXTRACT(YEAR FROM t.start_time) = 2023
)

SELECT
    start_station_name,
    end_station_name,

    -- Obliczenie dystansu w metrach
    ST_DISTANCE(start_point, end_point) AS distance_meters,

    duration_minutes,

    -- Obliczenie prędkości w km/h
    (ST_DISTANCE(start_point, end_point) / 1000.0) / (duration_minutes / 60.0) AS speed_kmh

FROM trip_distances

WHERE ST_DISTANCE(start_point, end_point) > 1000  -- Tylko > 1 km

ORDER BY speed_kmh DESC
LIMIT 20
```

#### Kluczowe elementy:

**1. Podwójny JOIN z tabelą stacji**:

Potrzebujemy koordynatów **dwóch** stacji (start i end):

```sql
-- Alias: start_station
INNER JOIN bikeshare_stations start_station
ON t.start_station_id = start_station.station_id

-- Alias: end_station
INNER JOIN bikeshare_stations end_station
ON t.end_station_id = end_station.station_id
```

**2. Tworzenie punktów geograficznych**:

```sql
ST_GEOGPOINT(start_station.longitude, start_station.latitude) AS start_point
ST_GEOGPOINT(end_station.longitude, end_station.latitude) AS end_point
```

**3. Obliczanie dystansu**:

```sql
ST_DISTANCE(start_point, end_point) AS distance_meters
```

Zwraca dystans w metrach (np. 1234.56).

**4. Obliczanie prędkości**:

Wzór: `speed = distance / time`

```sql
-- Dystans w km: distance_meters / 1000
-- Czas w godzinach: duration_minutes / 60
-- Prędkość w km/h:
(ST_DISTANCE(start_point, end_point) / 1000.0) / (duration_minutes / 60.0)
```

Uproszczenie:
```sql
-- To samo co:
ST_DISTANCE(start_point, end_point) * 60.0 / (duration_minutes * 1000.0)
```

**5. Filtrowanie**:

```sql
WHERE
    EXTRACT(YEAR FROM t.start_time) = 2023  -- Rok 2023
    AND ST_DISTANCE(start_point, end_point) > 1000  -- Dystans > 1 km
    AND duration_minutes > 0  -- Czas > 0
```

**6. Sortowanie i limit**:

```sql
ORDER BY speed_kmh DESC  -- Najszybsze na górze
LIMIT 20  -- Top 20
```

### 4. Model danych

```csharp
public record BikeTrip(
    string StartStation,
    string EndStation,
    double DistanceMeters,
    double SpeedKmh,
    double DurationMin
)
{
    public double DistanceKm => DistanceMeters / 1000.0;
    public bool IsAnomalousSpeed => SpeedKmh > 50.0;
    public string SpeedCategory => SpeedKmh switch { ... };
}
```

**Właściwości wyliczane**:
- `DistanceKm`: Dystans w kilometrach (dla czytelności)
- `IsAnomalousSpeed`: Czy prędkość jest podejrzana (> 50 km/h)
- `SpeedCategory`: Kategoria prędkości (wolna/normalna/szybka/nieprawdopodobna)

### 5. Wykrywanie anomalii

Aplikacja automatycznie wykrywa **podejrzane prędkości**:

**Kategorie prędkości**:
- **< 10 km/h**: Wolna (spacer)
- **10-20 km/h**: Normalna (rekreacja)
- **20-30 km/h**: Szybka (sport)
- **30-50 km/h**: Bardzo szybka (wyścig)
- **> 50 km/h**: ⚠️ NIEPRAWDOPODOBNA - błąd danych!

**Możliwe przyczyny anomalii**:
1. 🚗 Rower przewożony samochodem/autobusem (nie zwrócony w stacji pośredniej)
2. ⏱️ Błędnie zarejestrowany czas (zgubiony sygnał GPS, problemy systemu)
3. 📍 Niepoprawne współrzędne stacji w bazie danych
4. 🔄 Rower zwrócony do innej stacji bez prawidłowej rejestracji

## Konfiguracja Google Cloud

### 1. Utwórz projekt

```bash
gcloud auth login
gcloud projects create austin-bikeshare-analyzer
gcloud config set project austin-bikeshare-analyzer
```

### 2. Włącz BigQuery API

```bash
gcloud services enable bigquery.googleapis.com
```

### 3. Utwórz Service Account

```bash
gcloud iam service-accounts create bigquery-reader \
    --display-name="BigQuery Reader"

gcloud projects add-iam-policy-binding austin-bikeshare-analyzer \
    --member="serviceAccount:bigquery-reader@austin-bikeshare-analyzer.iam.gserviceaccount.com" \
    --role="roles/bigquery.user"

gcloud iam service-accounts keys create ~/bigquery-key.json \
    --iam-account=bigquery-reader@austin-bikeshare-analyzer.iam.gserviceaccount.com
```

### 4. Ustaw zmienną środowiskową

**Windows (PowerShell):**
```powershell
$env:GOOGLE_APPLICATION_CREDENTIALS="C:\path\to\bigquery-key.json"
```

**Linux/macOS:**
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/bigquery-key.json"
```

## Uruchomienie

```bash
cd src/AustinBikeshareAnalyzer

dotnet restore
dotnet run
```

## Przykładowe wyjście

```
═══════════════════════════════════════════════════════════════════════
   Austin Bikeshare - Analiza Szybkości Przejazdów (Geography API)
═══════════════════════════════════════════════════════════════════════

🔗 Łączenie z BigQuery...
📊 Dataset: bigquery-public-data.austin_bikeshare

🚴 Pobieranie Top 20 najszybszych przejazdów rowerowych...
   Używam funkcji geograficznych BigQuery:
   • ST_GEOGPOINT(lon, lat) - tworzenie punktów geograficznych
   • ST_DISTANCE(p1, p2) - dystans geodezyjny w metrach

✅ Pobrano 20 przejazdów.

📊 TOP 20 NAJSZYBSZYCH PRZEJAZDÓW ROWEROWYCH - AUSTIN 2023
═══════════════════════════════════════════════════════════════════════

┌─────┬────────────────────────────┬────────────────────────────┬──────────┬──────────┬──────────┐
│ Nr  │ Stacja startowa            │ Stacja końcowa             │ Dystans  │ Czas     │ Prędkość │
├─────┼────────────────────────────┼────────────────────────────┼──────────┼──────────┼──────────┤
│   1 │ 21st & Speedway            │ Rainey St & River St       │   5.23 km │    4.2 min │  74.7 km/h │  🔴
│   2 │ Republic Square            │ Zilker Park                │   3.87 km │    3.8 min │  61.1 km/h │  🔴
│   3 │ Convention Center          │ Barton Springs             │   2.14 km │    2.9 min │  44.3 km/h │
...
└─────┴────────────────────────────┴────────────────────────────┴──────────┴──────────┴──────────┘

⚠️  WYKRYTO ANOMALIE - PODEJRZANE PRĘDKOŚCI
═══════════════════════════════════════════════════════════════════════

Znaleziono 5 przejazdów z prędkością > 50 km/h.
To prawdopodobnie błędy danych, ponieważ rower rzadko osiąga taką prędkość.

Możliwe przyczyny:
  1. 🚗 Rower przewożony samochodem/autobusem
  2. ⏱️  Błędnie zarejestrowany czas (np. zgubiony sygnał GPS)
  3. 📍 Niepoprawne lokalizacje stacji w bazie danych
  4. 🔄 Rower zwrócony do innej stacji bez rejestracji końca podróży

📈 STATYSTYKI OGÓLNE:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   Prędkość:
   • Średnia:  38.45 km/h
   • Minimum:  30.12 km/h
   • Maximum:  74.73 km/h

   Dystans:
   • Średnia:  3.24 km

   Czas trwania:
   • Średnia:  5.17 minut
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ Analiza zakończona pomyślnie!
```

## Szczegóły techniczne

### Funkcje geograficzne BigQuery

#### Dlaczego używamy ST_GEOGPOINT zamiast kolumny location?

Kolumna `location` w tabeli `bikeshare_stations` może być:
1. Typu `GEOGRAPHY` - gotowy punkt geograficzny
2. Typu `STRING` - tekst w formacie WKT: `"POINT(-97.7431 30.2672)"`

Aby uniknąć problemów z typami, tworzymy punkty z osobnych kolumn `latitude` i `longitude`:

```sql
-- Zawsze działa, niezależnie od typu kolumny location
ST_GEOGPOINT(longitude, latitude)

-- vs. użycie kolumny location (może wymagać konwersji)
location  -- może być NULL, STRING lub GEOGRAPHY
```

#### Jak działa ST_DISTANCE?

**Algorytm**: Oblicza najkrótszą odległość po powierzchni elipsoidy Ziemi (WGS84).

**Nie jest to**:
- ❌ Dystans w linii prostej przez Ziemię
- ❌ Dystans drogą (routing)
- ❌ Dystans Euklidesowy (płaska ziemia)

**Jest to**:
- ✅ Dystans geodezyjny (great circle distance)
- ✅ Najkrótsza droga po powierzchni Ziemi
- ✅ "Odległość w linii prostej" na mapie (ale uwzględniająca krzywiznę Ziemi)

**Dokładność**: Centymetry dla małych dystansów (<1000 km)

#### Konwersja jednostek

BigQuery Geography API używa **metrów** jako jednostki podstawowej.

**Konwersje**:
```sql
-- Metry → Kilometry
distance_meters / 1000.0

-- Metry → Mile
distance_meters / 1609.34

-- Minuty → Godziny
duration_minutes / 60.0

-- Prędkość: (metry) → (km/h)
(distance_meters / 1000.0) / (duration_minutes / 60.0)
```

### Obliczanie prędkości

**Wzór podstawowy**:
```
prędkość = dystans / czas
```

**W SQL**:
```sql
-- Dystans w km: distance_meters / 1000
-- Czas w h: duration_minutes / 60
speed_kmh = (distance_meters / 1000) / (duration_minutes / 60)
```

**Uproszczenie**:
```sql
-- Dzielenie przez ułamek = mnożenie przez odwrotność
speed_kmh = (distance_meters / 1000) * (60 / duration_minutes)
speed_kmh = distance_meters * 60 / (duration_minutes * 1000)
```

**Przykład**:
- Dystans: 2400 metrów
- Czas: 8 minut

```
speed = (2400 / 1000) / (8 / 60)
speed = 2.4 / 0.1333
speed = 18 km/h
```

### Wydajność zapytania

**CTE (Common Table Expression)**:
```sql
WITH trip_distances AS (...)
```

Używamy CTE aby:
1. Zwiększyć czytelność (podział na kroki)
2. Umożliwić reużycie wyników (np. `ST_DISTANCE` obliczane raz)
3. Ułatwić debugowanie

**Alternatywa** (bez CTE):
```sql
-- Mniej czytelne, ST_DISTANCE obliczane wielokrotnie
SELECT ...,
    ST_DISTANCE(...) AS distance,
    ST_DISTANCE(...) / ... AS speed  -- Obliczane ponownie!
FROM ...
WHERE ST_DISTANCE(...) > 1000  -- I znowu!
```

## Wymagania - realizacja

✅ **Dataset Austin Bikeshare**: `bikeshare_trips` + `bikeshare_stations`

✅ **JOIN podróży ze stacjami**: Dwa razy (dla start i end)

✅ **ST_GEOGPOINT**: Tworzenie punktów z `longitude`, `latitude`

✅ **ST_DISTANCE**: Obliczanie dystansu w metrach

✅ **Obliczanie prędkości**: `(distance_km) / (time_h)`

✅ **Filtrowanie**: Dystans > 1 km, Rok 2023

✅ **Top 20 najszybszych**: ORDER BY speed DESC LIMIT 20

✅ **Rekord BikeTrip**: (StartStation, EndStation, DistanceMeters, SpeedKmh, DurationMin)

✅ **Wykrywanie anomalii**: Prędkości > 50 km/h oznaczone jako podejrzane

## Możliwe rozszerzenia

- Analiza tras (nie tylko dystans, ale rzeczywista trasa)
- Mapa ciepła (heatmap) najpopularniejszych tras
- Analiza godzin szczytu
- Porównanie prędkości w różnych porach dnia
- Wizualizacja tras na mapie
- Analiza sezonowości (zima vs lato)
- Predykcja czasu przejazdu między stacjami

## Rozwiązywanie problemów

### Błąd: "Invalid GEOGRAPHY value"

Sprawdź kolejność parametrów w `ST_GEOGPOINT`:
- ✅ `ST_GEOGPOINT(longitude, latitude)`
- ❌ `ST_GEOGPOINT(latitude, longitude)` - BŁĄD!

### Błąd: "Cannot access field on NULL"

Upewnij się, że filtrujesz `NULL` wartości:
```sql
WHERE latitude IS NOT NULL
  AND longitude IS NOT NULL
```

### Długi czas wykonania zapytania

Austin Bikeshare dataset może być duży. Zapytanie z funkcjami geograficznymi może potrwać 10-30 sekund. To normalne.

### Wszystkie prędkości są > 50 km/h

Możliwe problemy:
1. Nieprawidłowa kolejność parametrów w `ST_GEOGPOINT`
2. Błędna konwersja jednostek (metry vs kilometry)
3. Problem z danymi źródłowymi dla danego okresu

## Koszty

Austin Bikeshare dataset jest **publiczny i darmowy**, ale BigQuery ma limity:
- Pierwsze 1 TB zapytań miesięcznie: **DARMOWE**
- Następne TB: $5/TB

Ta aplikacja używa ~kilka MB na zapytanie, więc mieści się w darmowym limicie.

## Licencja

Projekt edukacyjny - dane publiczne z BigQuery Austin Bikeshare.
