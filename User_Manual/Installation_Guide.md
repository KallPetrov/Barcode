# 🚀 Изчерпателно ръководство за инсталация, инфраструктура и отстраняване на проблеми (Installation & Infrastructure Master Guide)

Този документ предоставя пълни технически инструкции за разполагане, първоначална настройка и системно администриране на платформата CALAC. Той описва всеки контейнер, конфигурационен параметър, стъпки за компилиране на мобилния и уеб клиента, както и процеса по миграция и разрешаване на често срещани проблеми.

---

## 🏗️ 1. Системна архитектура и разпределяне на контейнери

Производствената (Production) и предпроизводствената (Staging) инфраструктура се базират на контейнеризирани микроуслуги, работещи в изолирана мрежа. Архитектурата съдържа следните основни блокове:

```text
                                    ┌───────────────────────┐
                                    │    Admin Panel (Web)  │
                                    │  React 19 + Vite PWA  │
                                    └───────────┬───────────┘
                                                │ HTTPS / WSS
                                                ▼
┌──────────────────┐  gRPC/REST   ┌─────────────────────────┐   TCP (6379)  ┌───────────────┐
│  Mobile App PDA  ├─────────────►│    CALAC Backend API    ├──────────────►│  Redis Cache  │
│  Kotlin Android  │              │      .NET 10 LTS        │               └───────────────┘
└──────────────────┘              └─────────────┬───────────┘
                                                │
                                                │ TCP (5432)
                                                ▼
                                        ┌───────────────┐
                                        │  PostgreSQL   │
                                        │  Primary DB   │
                                        └───────────────┘
```

### Спецификация на контейнерите (Docker Services)

1. **База данни (PostgreSQL 16 Alpine)**:
   - **Роля**: Персистентно съхранение на всички транзакционни данни, одит логове, абонаменти и потребителски профили.
   - **Порт**: `5432` (достъпен само в локалната Docker мрежа или изложен за административни цели).
   - **Томове (Volumes)**: `postgres_data` монтиран към `/var/lib/postgresql/data` за сигурност при рестарт.

2. **Кеширащ слой и координация (Redis 7 Alpine)**:
   - **Роля**: Бързо кеширане на заявки, съхранение на разпределени сесии, броячи за Rate Limiting и SignalR Backplane (за синхронизация на уебхуци и нотификации в реално време между множество инстанции на API).
   - **Порт**: `6379`.
   - **Томове (Volumes)**: `redis_data` монтиран към `/data`.

---

## 🛠️ 2. Локална инсталация за разработчици (Local Developer Setup)

Локалният режим позволява стартиране на уеб и backend сървърите с минимални усилия, използвайки вградения SQLite механизъм за автоматично подсигуряване (automatic database initialization & seeding).

### Стъпка 2.1: Сваляне на софтуерния код
```bash
git clone https://github.com/calac/platform.git
cd platform
```

### Стъпка 2.2: Стартиране на Backend API (.NET 10 LTS)
1. Навигирайте до директорията на backend-а:
   ```bash
   cd backend
   ```
2. Извършете възстановяване на NuGet пакетите:
   ```bash
   dotnet restore
   ```
3. Изпълнете компилация на софтуерните проекти:
   ```bash
   dotnet build
   ```
4. Стартирайте уеб сървъра (Kestrel):
   ```bash
   dotnet run --project src/CALAC.Api
   ```
   - **Как работи SQLite Fallback**: Системата прочита секцията `ConnectionStrings` в `appsettings.json`. Ако параметърът `DefaultConnection` съдържа `Host=localhost` или е празен, и на тези портове не работи реална PostgreSQL инстанция, .NET софтуерът автоматично превключва към SQLite провайдър и създава локален файл `calac.db` в директория `backend/src/CALAC.Api/`.
   - Сървърът започва да слуша на: `http://localhost:5000`.
   - Swagger интерактивната документация се намира на адрес: `http://localhost:5000/swagger/index.html`.

### Стъпка 2.3: Стартиране на Административния панел (React 19)
1. Отворете нов терминал в корена на проекта и навигирайте до `admin/`:
   ```bash
   cd admin
   ```
2. Инсталирайте всички нужни Node.js зависимости:
   ```bash
   npm install
   ```
3. Конфигурирайте променливите на средата чрез създаване на файл `.env.development` в директория `admin/`:
   ```env
   VITE_API_URL=http://localhost:5000
   ```
4. Стартирайте Vite сървъра за разработка:
   ```bash
   npm run dev
   ```
   - Уеб интерфейсът е достъпен на: `http://localhost:5173`.
   - Потребител по подразбиране: `admin` с парола `Admin123!`.

---

## 🐳 3. Разполагане в производствена среда (Production Deployment)

При производствено разполагане се изисква използването на реални, високопроизводителни контейнери.

### Примерна пълна конфигурация за `docker-compose.yml`
Създайте или актуализирайте `docker-compose.yml` в корена на вашия сървър:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: calac-postgres
    restart: always
    environment:
      POSTGRES_USER: ${DB_USER:-calac}
      POSTGRES_PASSWORD: ${DB_PASSWORD:-calac123}
      POSTGRES_DB: ${DB_NAME:-calac_platform}
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER:-calac} -d ${DB_NAME:-calac_platform}"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: calac-redis
    restart: always
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  backend:
    image: calac/backend-api:latest
    container_name: calac-backend-api
    restart: always
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    environment:
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=calac_platform;Username=calac;Password=calac123
      - Jwt__Key=СложнаПаролаМинимум32СимволаЗаJWT!!
      - Jwt__Issuer=CALAC
      - Jwt__Audience=CALAC
      - Jwt__ExpireHours=8
      - Stripe__ApiKey=sk_prod_...
      - Stripe__WebhookSecret=whsec_...
    ports:
      - "5000:5000"

volumes:
  postgres_data:
  redis_data:
```

### Стартиране и Управление
За стартиране на пълния стек контейнери в заден план:
```bash
docker compose up -d
```

За спиране на контейнерите и запазване на данните в томовете:
```bash
docker compose down
```

За преглед на логове в реално време:
```bash
docker compose logs -f --tail=100
```

---

## 🔄 4. Миграции на бази данни и управление на схемата

CALAC управлява схемата си чрез Entity Framework Core. Всички миграции са разположени в проекта `CALAC.Infrastructure`.

### Подготовка на средата
За управление на миграциите ви е нужен глобално инсталиран инструмент `dotnet-ef`. Добавете неговия път в променливата `PATH`:
```bash
dotnet tool install --global dotnet-ef || true
export PATH="$PATH:$HOME/.dotnet/tools"
```

### Добавяне на нова миграция (при промяна в C# домейните)
Изпълнява се от директория `backend/`:
```bash
dotnet ef migrations add <ИмеНаПромяната> \
  --project src/CALAC.Infrastructure \
  --startup-project src/CALAC.Api \
  --context AppDbContext
```

### Ръчно прилагане на миграциите към базата
За да обновите структурата на базата данни (напр. производствения PostgreSQL):
```bash
dotnet ef database update \
  --project src/CALAC.Infrastructure \
  --startup-project src/CALAC.Api \
  --context AppDbContext
```

---

## ⚡ 5. Инфраструктурна диагностика и отстраняване на грешки (Troubleshooting)

### Грешка: "Docker PostgreSQL - overlayfs: invalid argument"
- **Причина**: Среща се често в среди за сигурност, виртуализирани среди, пясъчници или специфични Linux дистрибуции, където ядрото няма права да монтира overlayfs за Docker обеми.
- **Стъпки за решаване**:
  1. Спрете Docker контейнерите: `docker compose down`.
  2. Стартирайте платформата в локален режим. Backend софтуерът автоматично ще засече липсата на външна база данни и ще превключи на **SQLite режим**, като създаде локален файл `calac.db`. Това напълно елиминира нуждата от Docker PostgreSQL контейнер при демонстрации и локални тестове.

### Грешка: "402 Payment Required" или "Subscription required or expired" при тестване на API
- **Причина**: Настройките на сигурността в `SubscriptionMiddleware` блокират всички API заявки към складови процеси, ако текущият Tenant (клиент) няма записан активен абонамент в базата.
- **Стъпки за решаване**:
  - Вграденият `DbSeeder` автоматично вписва активен абонамент "starter" с валидност 1 година за демо клиента ("Демо компания"), но само при чисто/ново генериране на базата данни.
  - Ако сте променили настройките и базата данни е вече сийлната без абонамент:
    - При SQLite: Спрете сървъра, изтрийте файловете `calac.db`, `calac.db-shm` и `calac.db-wal` в директория `backend/src/CALAC.Api/` и рестартирайте API сървъра. Базата ще се изгради наново и ще бъде сийлната с валиден абонамент автоматично.
    - При PostgreSQL: Влезте в базата данни и изпълнете:
      ```sql
      INSERT INTO "TenantSubscriptions" ("Id", "TenantId", "PlanCode", "IsActive", "ExpiresAt", "CreatedAt")
      VALUES ('72a7bbee-1ae4-4b1e-b7a5-d74e2da1ecf7', '11111111-1111-1111-1111-111111111111', 'starter', 1, '2028-12-31 23:59:59', '2026-01-01 00:00:00');
      ```

### Грешка: "Address already in use" (Порт 5000 или 5173 е зает)
- **Причина**: Предишна инстанция на Kestrel сървъра или Node/Vite работи в заден план.
- **Стъпки за решаване**:
  - Открийте процеса, заемащ порта, и го прекратете:
    ```bash
    kill $(lsof -t -i :5000) 2>/dev/null || true
    kill $(lsof -t -i :5173) 2>/dev/null || true
    ```
