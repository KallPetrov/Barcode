# CALAC

Модулна платформа за работа с баркод скенери и PDA устройства (Zebra, Honeywell, Datalogic).

**Версия:** 0.12.0
**Последна актуализация:** 2026-06-28

## Структура

```
Barcode/
├── backend/           # .NET 8 REST API
├── admin/             # React админ панел
├── mobile/android/    # Kotlin PDA приложение
├── docs/              # Документация и изисквания
├── CHANGELOG.md       # Проследяване на промените
└── docker-compose.yml # PostgreSQL + Redis
```

## Текущо състояние (Фаза 3 в процес)

### Реализирани модули

1. **Основни (Фаза 1)**
   - JWT автентикация (потребител/парола + PIN)
   - Управление на потребители и роли (Operator, Supervisor, Admin)
   - Регистрация и мониторинг на PDA устройства
   - Offline sync опашка (API + Room DB на устройството)
   - Audit log
   - Multi-tenant основа

2. **Инвентаризация (Фаза 2 - завършена)**
   - Управление на складови локации (зона, ред, рафт, ниво, позиция)
   - Управление на артикули (SKU, име, описание, баркод, тегло, мярка)
   - Управление на наличностите (количество, партида/сериен номер, срок на годност)

3. **Оперативна работа (Фаза 3 - разширена)**
   - Задачи към операторите с приоритет, статус и разпределение
   - Оперативна панелна статистика за задачи, инвентаризации и picking
   - Скорошна активност и audit trail за ръководители
   - Известия и аларми за критични събития
   - Отчети за ефективност на операторите
   - По-силна mobile/offline workflow поддръжка

## Изисквания

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Android Studio (за PDA приложението)

## Бърз старт

### 1. База данни

```powershell
docker compose up -d
```

### 2. Backend API

```powershell
cd backend
dotnet restore
dotnet run --project src/BarcodePlatform.Api
```

API: http://localhost:5000  
Swagger: http://localhost:5000/swagger

### 3. Админ панел

```powershell
cd admin
npm install
npm run dev
```

Админ: http://localhost:5173

**Демо акаунти (само за dev среда):**

| Потребител | Парола       | Роля     |
|------------|--------------|----------|
| admin      | Admin123!    | Admin    |
| operator   | Operator123! | Operator |

> [!WARNING]
> Тези акаунти са предназначени единствено за демонстрация и разработка. В производствена среда те трябва да бъдат сменени или премахнати.

### 4. PDA приложение

Отворете `mobile/android` в Android Studio и стартирайте на устройство или емулатор.

- Емулатор: API URL е `http://10.0.2.2:5000`
- Реално PDA: променете `API_BASE_URL` в `app/build.gradle.kts`

## API endpoints

### Аутентикация

| Метод | Endpoint | Описание |
|-------|----------|----------|
| POST | `/api/auth/login` | Вход с потребител/парола |
| POST | `/api/auth/login/pin` | Вход с PIN |
| GET | `/api/auth/me` | Текущ потребител |

### Устройства

| Метод | Endpoint | Описание |
|-------|----------|----------|
| POST | `/api/devices/register` | Регистрация на PDA |
| POST | `/api/devices/heartbeat` | Heartbeat сигнал |
| GET | `/api/devices` | Списък устройства |
| GET | `/api/devices/{id}` | Информация за устройство |

### Локации

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/locations` | Списък локации |
| GET | `/api/locations/{id}` | Информация за локация |
| POST | `/api/locations` | Създаване на локация |
| PUT | `/api/locations/{id}` | Актуализация на локация |

### Артикули

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/items` | Списък артикули |
| GET | `/api/items/{id}` | Информация за артикул |
| GET | `/api/items/barcode/{barcode}` | Търсене по баркод |
| POST | `/api/items` | Създаване на артикул |
| PUT | `/api/items/{id}` | Актуализация на артикул |

### Инвентар

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/inventory/stock` | Списък наличности |
| POST | `/api/inventory/stock` | Добавяне на наличност |

### Синхронизация

| Метод | Endpoint | Описание |
|-------|----------|----------|
| POST | `/api/sync/push` | Качване на offline операции |
| GET | `/api/sync/status` | Статус на синхронизацията |

### Табло

| Метод | Endpoint | Описание |
|-------|----------|----------|
| GET | `/api/dashboard/stats` | Статистики |

## Проследяване на промените

За подробна история на промените и версии вижте [CHANGELOG.md](./CHANGELOG.md).

## План за развитие

### Фаза 2 (продължение)
- Пълна/частична инвентаризация
- Управление на инвентаризационни сесии
- Пicking с FEFO/FIFO
- Приемане на стоки
- Вътрешни трансфери

### Фаза 3
- ERP adapter (Odoo / Dynamics)
- Печат на етикети
- Работа с партиди и сериални номера
- Допълнителни отчети

Вижте `docs/moduli-new_system.md` за пълния списък модули.
