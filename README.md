# Груповий pre-order: функціонал для Owner (рівні 1–3)

Стек: ASP.NET Core + DynamoDB (`AWSSDK.DynamoDBv2`, Object Persistence Model / `DynamoDBContext`).

## Структура

| Файл | Що всередині |
|---|---|
| `Domain/Entities.cs` | Моделі таблиць DynamoDB, enum-и статусів, назви GSI |
| `Infrastructure/GroupPreOrderRepository.cs` | Усі запити до DynamoDB: Load, Query по GSI, BatchGet страв, TransactWrite при submit |
| `Application/GroupPreOrderService.cs` | Бізнес-логіка трьох рівнів |
| `Application/Exceptions.cs` | `NotFound` → 404, `BadRequest` → 400, `Forbidden` → 403 |
| `Api/GroupPreOrderController.cs` | 4 ендпоінти |
| `Api/ExceptionHandler.cs` | Маппінг винятків у ProblemDetails + реєстрація в `Program.cs` (у коментарі) |
| `Contracts/Dtos.cs` | Request/response точно за `homework-backend.md` |
| `seed/` | Скрипт створення таблиць, тестові дані, виправлені `dishes.json` / `locations.json` |

## Ендпоінти

| Рівень | Метод | Шлях |
|---|---|---|
| 🟢 1 | GET | `/guest/groups/{groupReservationId}/sessions` |
| 🔵 2 | GET | `/reservations/{reservationId}/guest-session-id` |
| 🔵 2 | POST | `/group-reservations/{groupReservationId}/submit` |
| 🟣 3 | DELETE | `/guest/groups/{groupReservationId}/sessions/{guestSessionId}` |

Коди помилок і тексти повідомлень збігаються з мок-бекендом фронтенду
(`src/app/shared/mocks/group-orders/group-orders.mock-store.ts`).

## Що звірити з проєктом

Назви `Dishes` і атрибути страв узяті з `dishes.json`. Решту таблиць я **припустила**:

- `Reservations`: `id`, `customerId` (Owner)
- `GroupReservations`: `id`, `reservationId`, `status` (0 Open / 1 Submitted / 2 Closed)
- `GuestSessions`: `id`, `groupReservationId` (GSI), `reservationId` (GSI, лише в owner session), `type` (0 Owner / 1 Guest), `name`, `isSubmitted`, `joinedAt`, `items` = `[{dishId, quantity}]`
- `Orders`: `id`, `reservationId` (GSI), `status`, `dishItems` = `[{dishId, quantity}]`

Якщо у вашому проєкті інакше — правиться лише `Domain/Entities.cs` (рядки в атрибутах).
Якщо користувач визначається не через claim `sub` / `NameIdentifier` — поправте `UserId` у контролері.

## Тестування

```bash
cd seed
export AWS_REGION=us-west-2

# 1. таблиці (пропустіть, якщо вони вже є)
./create-tables.sh

# 2. дані
aws dynamodb batch-write-item --request-items file://locations.json
aws dynamodb batch-write-item --request-items file://dishes.json
# у homework-data.json замініть REPLACE-WITH-YOUR-USER-ID на id свого користувача
aws dynamodb batch-write-item --request-items file://homework-data.json
```

Очікувані відповіді на тестових даних:

| Запит | Результат |
|---|---|
| GET `/guest/groups/hw-group-1/sessions` | 4 учасники: Martina 32.49, Andrii 19.98, Olena 18.99, Ivan 0; `total` = 71.46 |
| GET `/reservations/hw-reservation-1/guest-session-id` | `{"guestSessionId": "hw-gs-owner"}` |
| POST `/group-reservations/hw-group-1/submit` з `{"reservationId":"hw-reservation-1"}` | `[{"dishId":"3a6058ab-…","quantity":3},{"dishId":"95f0b087-…","quantity":1}]` (Olena не submitted — її лосося немає) |
| той самий POST повторно | 400 `Group reservation was submitted.` |
| DELETE `/guest/groups/hw-group-1/sessions/hw-gs-olena` | 3 учасники, `total` = 52.47 |
| DELETE `.../sessions/hw-gs-owner` | 400 `Owner session cannot be removed.` |
| DELETE `.../sessions/unknown` | 404 |
| будь-який запит від іншого користувача | 403 |

Перевірити записане в базу:

```bash
aws dynamodb get-item --table-name Orders --key '{"id":{"S":"hw-order-1"}}'
aws dynamodb get-item --table-name GroupReservations --key '{"id":{"S":"hw-group-1"}}'
```
