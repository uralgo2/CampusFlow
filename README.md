# CampusFlow

REST API и веб-интерфейс для бронирования аудиторий: справочник аудиторий, свободные слоты, брони с
проверкой пересечений, роли и административная часть.

## Стек

| Слой           | Технологии                                         |
| -------------- | -------------------------------------------------- |
| Backend        | .NET 9, ASP.NET Core Web API                       |
| Данные         | EF Core 9, SQLite                                  |
| Аутентификация | JWT (HS256), Microsoft.IdentityModel.JsonWebTokens |
| Документация   | OpenAPI + Swagger UI (Swashbuckle)                 |
| Frontend       | HTML, CSS, JavaScript                              |
| Тесты          | 108 юнит-тестов на встроенном раннере              |

## Требования

.NET SDK 9.0 или новее.

## Запуск

```powershell
dotnet run
```

Приложение поднимается на `http://localhost:5242`:

| Ресурс        | Адрес                                           |
| ------------- | ----------------------------------------------- |
| Веб-интерфейс | `http://localhost:5242/`                        |
| Swagger UI    | `http://localhost:5242/swagger`                 |
| OpenAPI JSON  | `http://localhost:5242/swagger/v1/swagger.json` |

При первом запуске создаётся файл `campusflow.db` и загружаются seed-данные.

Другой порт:

```powershell
dotnet run --urls http://127.0.0.1:8080
```

## Тесты

```powershell
dotnet run --project tests/CampusFlow.Tests
```

Фильтр по имени теста:

```powershell
dotnet run --project tests/CampusFlow.Tests -- --filter BookingService
```

Код выхода `0` — все тесты пройдены, `1` — есть падения.

## Конфигурация

`appsettings.json`:

| Ключ                           | Значение по умолчанию            | Назначение                              |
| ------------------------------ | -------------------------------- | --------------------------------------- |
| `ConnectionStrings:CampusFlow` | `Data Source=campusflow.db`      | файл базы SQLite                        |
| `Jwt:Issuer`                   | `CampusFlow`                     | издатель токена                         |
| `Jwt:Audience`                 | `CampusFlow`                     | получатель токена                       |
| `Jwt:Key`                      | секрет из `appsettings.json`     | ключ HMAC-SHA256, не короче 32 символов |
| `Jwt:LifetimeMinutes`          | `480`                            | срок действия токена                    |
| `Seed:RecreateOnStartup`       | `false` (в Development — `true`) | пересоздание базы при старте            |

Любой параметр переопределяется переменной окружения:

```powershell
$env:Jwt__Key = "случайный-секрет-не-короче-32-символов"
$env:ConnectionStrings__CampusFlow = "Data Source=D:\data\campusflow.db"
dotnet run
```

## Аутентификация

Алгоритм — HMAC-SHA256. Выпуск и проверка токенов выполняются через пакеты
`Microsoft.IdentityModel.JsonWebTokens` и `Microsoft.AspNetCore.Authentication.JwtBearer`.

Вход:

```http
POST /api/auth/login
Content-Type: application/json

{ "login": "student1", "password": "student1" }
```

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "login": "student1",
    "displayName": "Иван Студентов",
    "role": "student"
  }
}
```

Токен передаётся в заголовке:

```http
Authorization: Bearer <token>
```

Состав токена:

| Claim               | Содержимое                             |
| ------------------- | -------------------------------------- |
| `sub`               | идентификатор пользователя             |
| `login`             | логин                                  |
| `displayName`       | отображаемое имя                       |
| `role`              | `student` или `admin`                  |
| `jti`               | уникальный идентификатор токена        |
| `iat`, `nbf`, `exp` | время выпуска, начало и конец действия |
| `iss`, `aud`        | издатель и получатель                  |

При проверке контролируются подпись, алгоритм (`HS256`), срок действия, издатель и получатель;
`ClockSkew` равен нулю. После проверки токена пользователь загружается из базы, поэтому роль и
признак существования пользователя всегда актуальны.

Роли:

| Действие                       | student | admin |
| ------------------------------ | ------- | ----- |
| Аудитории, свободные слоты     | да      | да    |
| Создание брони                 | да      | да    |
| Свои брони, отмена своей брони | да      | да    |
| Все брони, отмена любой брони  | нет     | да    |
| Управление аудиториями         | нет     | да    |
| Статистика загрузки            | нет     | да    |

## API

Базовый путь — `/api`. Формат обмена — JSON, UTF-8. Даты `YYYY-MM-DD`, время `HH:MM`.

| Метод  | Путь                                                | Доступ | Успех     | Ошибки             |
| ------ | --------------------------------------------------- | ------ | --------- | ------------------ |
| POST   | `/api/auth/login`                                   | все    | 200       | 400, 401           |
| GET    | `/api/auth/me`                                      | токен  | 200       | 401                |
| GET    | `/api/rooms`                                        | все    | 200       | 400                |
| GET    | `/api/rooms/{id}`                                   | все    | 200       | 404                |
| GET    | `/api/rooms/{id}/free-slots?date=`                  | все    | 200       | 400, 404           |
| GET    | `/api/rooms/free?date=&startTime=&endTime=`         | все    | 200       | 400                |
| POST   | `/api/bookings`                                     | токен  | 201       | 400, 401, 404, 409 |
| GET    | `/api/bookings/my?status=`                          | токен  | 200       | 400, 401           |
| DELETE | `/api/bookings/{id}`                                | токен  | 200       | 401, 403, 404, 409 |
| GET    | `/api/admin/bookings?date=&roomId=&userId=&status=` | admin  | 200       | 400, 403           |
| GET    | `/api/admin/bookings/export`                        | admin  | 200 (CSV) | 400, 403           |
| DELETE | `/api/admin/bookings/{id}`                          | admin  | 200       | 403, 404, 409      |
| POST   | `/api/admin/rooms`                                  | admin  | 201       | 400, 403           |
| PUT    | `/api/admin/rooms/{id}`                             | admin  | 200       | 400, 403, 404      |
| DELETE | `/api/admin/rooms/{id}`                             | admin  | 204       | 403, 404, 409      |
| GET    | `/api/admin/stats?from=&to=&building=`              | admin  | 200       | 400, 403           |

### Ошибки

```json
{
  "code": "BOOKING_CONFLICT",
  "message": "Аудитория уже забронирована на это время",
  "details": {
    "roomId": 4,
    "date": "2026-11-15",
    "startTime": "14:00",
    "endTime": "15:00"
  }
}
```

| Код                  | HTTP |
| -------------------- | ---- |
| `VALIDATION_ERROR`   | 400  |
| `UNAUTHORIZED`       | 401  |
| `FORBIDDEN`          | 403  |
| `ROOM_NOT_FOUND`     | 404  |
| `BOOKING_NOT_FOUND`  | 404  |
| `NOT_FOUND`          | 404  |
| `METHOD_NOT_ALLOWED` | 405  |
| `BOOKING_CONFLICT`   | 409  |
| `ALREADY_CANCELLED`  | 409  |
| `ROOM_IN_USE`        | 409  |
| `INTERNAL_ERROR`     | 500  |

### Правила бронирования

- `date` не в прошлом;
- `startTime < endTime`;
- `startTime` и `endTime` кратны 30 минутам;
- `09:00 ≤ startTime`, `endTime ≤ 18:00`;
- для пары «аудитория + дата» нет активной брони с пересекающимся интервалом `[start, end)`;
  касание границ конфликтом не считается;
- `userId` берётся из токена;
- отменённые брони не занимают слот.

### Свободные слоты

```http
GET /api/rooms/1/free-slots?date=2026-11-15
```

```json
{
  "roomId": 1,
  "date": "2026-11-15",
  "freeSlots": [
    { "startTime": "09:00", "endTime": "10:00" },
    { "startTime": "11:30", "endTime": "14:00" }
  ]
}
```

Возвращаются максимальные непрерывные интервалы внутри рабочего дня `09:00–18:00`.

### Создание брони

```http
POST /api/bookings
Authorization: Bearer <token>
Content-Type: application/json

{ "roomId": 4, "date": "2026-11-15", "startTime": "14:00", "endTime": "15:00", "comment": "Семинар" }
```

```json
{
  "id": 8,
  "roomId": 4,
  "roomName": "B-101",
  "userId": 1,
  "userDisplayName": "Иван Студентов",
  "date": "2026-11-15",
  "startTime": "14:00",
  "endTime": "15:00",
  "status": "active",
  "comment": "Семинар"
}
```

## Модель данных

| Сущность  | Поля                                                                          |
| --------- | ----------------------------------------------------------------------------- |
| `User`    | `id`, `login`, `displayName`, `role`                                          |
| `Room`    | `id`, `name`, `building`, `floor`, `capacity`, `equipment`                    |
| `Booking` | `id`, `roomId`, `userId`, `date`, `startTime`, `endTime`, `status`, `comment` |

Оборудование: `projector`, `whiteboard`, `computers`, `audio`, `video`. Корпус: `A`, `B`, `C`, `D`.
Статус брони: `active`, `cancelled`.

## Seed-данные

Пароль пользователей равен логину.

| Логин      | Имя                  | Роль    |
| ---------- | -------------------- | ------- |
| `student1` | Иван Студентов       | student |
| `student2` | Мария Студентова     | student |
| `admin`    | Администратор Пупкин | admin   |

Аудитории и брони:

- 12 аудиторий в корпусах A–D, вместимость 10–60 человек;
- 7 броней; даты задаются относительно дня запуска: `0` — сегодня, `+1` — завтра, `+2` — послезавтра.

Полные таблицы: `Infrastructure/Persistence/DbSeeder.cs`.

## Веб-интерфейс

Доступен по адресу `http://localhost:5242/`.

## Структура проекта

```
Program.cs                     конфигурация приложения и конвейер обработки запросов
Api/                           контроллеры, JWT-обработчик, middleware ошибок
Application/                   сервисы, валидация, разбор дат и времени, абстракции репозиториев
Contracts/                     DTO запросов и ответов
Domain/                        сущности, SchedulePolicy, справочники
Infrastructure/                EF Core + SQLite, репозитории, seed
tests/CampusFlow.Tests/        юнит-тесты, fakes, встроенный раннер
wwwroot/                       Веб-интерфейс
```
