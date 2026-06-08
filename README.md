# ItemTradeApp Backend

## O projekcie

ItemTradeApp to aplikacja webowa umożliwiająca bezpieczną wymianę wirtualnych przedmiotów pochodzących z różnych gier komputerowych. System pozwala użytkownikom tworzyć oferty wymiany, negocjować warunki transakcji za pomocą kontrofert, komunikować się przez wbudowany czat oraz zarządzać własnym profilem.

Backend został zaimplementowany w technologii ASP.NET Core i udostępnia REST API wykorzystywane przez aplikację frontendową. System wykorzystuje Auth0 do uwierzytelniania użytkowników oraz SignalR do komunikacji w czasie rzeczywistym.

# Główne funkcjonalności

## Zarządzanie użytkownikami

* rejestracja i logowanie użytkowników,
* uwierzytelnianie z wykorzystaniem Auth0,
* zarządzanie profilem użytkownika,
* edycja danych konta,
* system ról i uprawnień,
* system tokenów użytkownika.

## Zarządzanie ofertami

* tworzenie ofert wymiany,
* edycja ofert,
* usuwanie ofert,
* przeglądanie publicznych ofert,
* filtrowanie i wyszukiwanie ofert,
* śledzenie statusu ofert.

## Kontroferty

* składanie kontrofert do istniejących ofert,
* akceptowanie kontrofert,
* odrzucanie kontrofert,
* śledzenie historii negocjacji.

## System czatu

* prywatne rozmowy pomiędzy użytkownikami,
* komunikacja w czasie rzeczywistym z wykorzystaniem SignalR,
* edycja wiadomości,
* usuwanie wiadomości,
* licznik nieprzeczytanych wiadomości.

## Zarządzanie gatunkami, grami, przedmiotami i ich rzadkościami

* zarządzanie grami,
* zarządzanie gatunkami gier,
* zarządzanie rzadkościami przedmiotów,
* zarządzanie katalogiem przedmiotów,
* endpointy typu dropdown wykorzystywane przez frontend.

## Powiadomienia

* generowanie powiadomień systemowych,
* oznaczanie powiadomień jako przeczytane,
* liczniki nieprzeczytanych powiadomień.

## E-meaile

* generowanie maili na podstawie danych biznesowych i dostępnych templatów

## Wymiany

* przypisywanie pośrednika do wymiany
* dodawanie zdjęć do wymiany jako dowodów
* filtrowanie i wyszukiwanie wymian
* pozytywne zakończenie wymiany lub jej anulacja

# Wykorzystane technologie

## Backend

* ASP.NET Core 9
* Entity Framework Core
* PostgreSQL
* SignalR
* RazorLight
* Auth0
* FluentValidation

## Infrastruktura

* Docker
* Docker Compose
* Nginx

## Testy

* xUnit
* Moq

---

# Architektura aplikacji

Projekt został zrealizowany zgodnie z podejściem **Vertical Slice Architecture**.

Kod został podzielony według funkcjonalności biznesowych, a nie warstw technicznych. Każda funkcjonalność zawiera własne kontrolery, serwisy, repozytoria, DTO oraz walidatory.

Przykładowa struktura projektu:

```text
Features/
├── Trades/
├── CounterOffers/
├── Chat/
├── Notifications/
├── UserManagement/
├── UserSettings/
├── Games/
├── Genres/
├── Items/
└── ItemRarities/
```

Takie podejście zwiększa czytelność kodu oraz ułatwia dalszy rozwój systemu.

---

# Wymagania

Przed uruchomieniem projektu należy zainstalować:

* .NET SDK 9.0
* PostgreSQL
* Docker (opcjonalnie)
* Docker Compose (opcjonalnie)

Sprawdzenie wersji:

```bash
dotnet --version
docker --version
docker compose version
```

---

# Konfiguracja aplikacji

Aplikacja wykorzystuje konfigurację z plików `appsettings.json` oraz zmiennych środowiskowych.

Przykładowa konfiguracja:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ItemTradeApp;Username=postgres;Password=postgres"
  },

  "Auth0": {
    "Domain": "twoja-domena.us.auth0.com",
    "Audience": "https://item-tradeapp.com",
    "ClientId": "client-id",
    "ClientSecret": "client-secret"
  }
}
```

# Przygotowanie bazy danych

W celu utworzenia lub zaktualizowania schematu bazy danych należy wykonać załączone w solucji skrypty SQL.


# Uruchomienie aplikacji

Przywrócenie pakietów NuGet:

```bash
dotnet restore
```

Budowanie projektu:

```bash
dotnet build
```

Uruchomienie aplikacji:

```bash
dotnet run
```

Domyślne adresy aplikacji:

```text
http://localhost:5277
https://localhost:7144
```

Dokumentacja Swagger:

```text
https://localhost:7144/swagger
```

---

# Uruchomienie przy użyciu Dockera

Budowanie i uruchomienie kontenerów:

```bash
docker compose up --build
```

Uruchomienie w tle:

```bash
docker compose up -d
```

Zatrzymanie kontenerów:

```bash
docker compose down
```

---

# Uwierzytelnianie i autoryzacja

System wykorzystuje Auth0 do obsługi logowania i autoryzacji użytkowników.

Dostęp do chronionych endpointów wymaga przesłania poprawnego tokenu JWT:

```http
Authorization: Bearer <token>
```

Proces uwierzytelniania wygląda następująco:

1. Użytkownik loguje się przy użyciu Auth0.
2. Auth0 generuje token JWT.
3. Frontend zapisuje token.
4. Token jest przesyłany w nagłówku żądania.
5. Backend weryfikuje poprawność tokenu oraz uprawnienia użytkownika.

---

# Komunikacja w czasie rzeczywistym

System czatu wykorzystuje SignalR.

Endpoint huba:

```text
/api/hubs/chat
```

Obsługiwane zdarzenia:

* message.new
* chat.message.updated
* chat.message.deleted
* chat.thread.updated
* chat.thread.read
* chat.created
* chat.closed

---

# Format odpowiedzi API

Backend wykorzystuje zunifikowany format odpowiedzi oparty o klasę `ApiResult<T>`.

Przykład poprawnej odpowiedzi:

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "message": "Operacja zakończona sukcesem.",
  "data": {}
}
```

Przykład odpowiedzi błędu jeśli dany status HTTP na taką pozwala:

```json
{
  "isSuccess": false,
  "statusCode": 400,
  "message": "Błąd walidacji."
}
```

---

# Struktura projektu

```text
ItemTradeApp/
├── Features/
├── Persistence/
├── Filters/
├── Middlewares/
├── Policies/
├── Resources/
├── SQL/
├── Program.cs
├── appsettings.json
├── Dockerfile
```

---

# Uruchamianie testów

Uruchomienie wszystkich testów:

```bash
dotnet test
```

Uruchomienie konkretnego projektu testowego:

```bash
dotnet test ItemTradeApp.UnitTests
```

---

# Autorzy

Projekt realizowany w ramach pracy inżynierskiej.

* Piotr Wójcik
* Aleksander Radoliński
* Igor Tarasiuk

Polsko-Japońska Akademia Technik Komputerowych (PJATK)

---

# Licencja

Projekt został wykonany w celach edukacyjnych oraz jako część pracy inżynierskiej.
-