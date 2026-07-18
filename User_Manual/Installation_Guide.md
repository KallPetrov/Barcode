# 🚀 Ръководство за инсталация и инфраструктура (Installation Guide)

Този документ описва детайлно системните изисквания, стъпките за инсталиране и конфигурация на CALAC в различни среди — от локално развитие (Development) до производствени системи (Production).

---

## 🏗️ Системни изисквания

За правилната работа на всички компоненти на CALAC са необходими следните софтуерни пакети:

| Компонент | Изискван софтуер | Минимална версия | Бележки |
|-----------|------------------|------------------|---------|
| **Backend API** | .NET SDK | 10.0 (LTS) | Необходимо за компилиране и изпълнение |
| **Admin Panel** | Node.js & npm | Node 20+, npm 10+ | Използва се за разработка и Vite build |
| **Mobile App** | JDK & Gradle | JDK 17, Gradle 8+ | За изграждане на Android пакета (.apk) |
| **База данни** | PostgreSQL | 16.0 | Използва се в Production |
| **База данни (Dev)**| SQLite | 3.x | Автоматичен fallback за локални тестове |
| **Кеширане** | Redis | 7.x-alpine | Използва се за разпределено кеширане |
| **Оркестрация** | Docker & Compose | Docker 24+, Compose v2+ | За контейнеризиране на услугите |

---

## 🛠️ 1. Локално инсталиране за разработчици (Development Mode)

Локалната среда е проектирана да работи максимално лесно с автоматичен fallback към SQLite, елиминирайки нуждата от тежка инсталация на бази данни за бързи тестове.

### Стъпка 1: Клониране на хранилището
```bash
git clone https://github.com/calac/platform.git
cd platform
```

### Стъпка 2: Конфигуриране на Backend API
Backend платформата чете настройките си от `backend/src/CALAC.Api/appsettings.json`.
При стартиране .NET проверява връзката `DefaultConnection`. Ако тя сочи към `localhost` (и липсва работещ PostgreSQL) или е напълно празна, системата **автоматично превключва към SQLite база данни `calac.db`** в работната директория.

За да стартирате Backend-а:
```bash
cd backend
dotnet restore
dotnet build
cd src/CALAC.Api
dotnet run
```
- Сървърът стартира на адрес: `http://localhost:5000`
- Достъп до Swagger UI документация: `http://localhost:5000/swagger`

### Стъпка 3: Конфигуриране и стартиране на уеб панела (Admin Panel)
Влезте в директория `admin/` и инсталирайте нужните Node.js пакети:
```bash
cd admin
npm install
```

Конфигурирайте променливите на средата. По подразбиране Vite се свързва с API на `http://localhost:5000`. Можете да пренапишете това, като създадете файл `.env.development` в директория `admin/`:
```env
VITE_API_URL=http://localhost:5000
```

Стартирайте локалния Vite сървър за разработка:
```bash
npm run dev
```
- Уеб интерфейсът ще бъде достъпен на: `http://localhost:5173`

---

## 📦 2. Инсталиране чрез Docker Compose (Production / Staging)

В производствена среда системата изисква PostgreSQL за устойчивост на данните и Redis за висока производителност.

### Стъпка 1: Конфигурация на `docker-compose.yml`
Основният Docker Compose файл съдържа дефиниции на следните услуги:
- `postgres` (Порт 5432) — Основна релационна база данни.
- `redis` (Порт 6379) — Кеш памет и SignalR backplane за събития.

Примерна производствена конфигурация:
```yaml
services:
  postgres:
    image: postgres:16-alpine
    container_name: calac-postgres
    environment:
      POSTGRES_USER: ${DB_USER:-calac}
      POSTGRES_PASSWORD: ${DB_PASSWORD:-calac123}
      POSTGRES_DB: ${DB_NAME:-calac_platform}
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U calac -d calac_platform"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: calac-redis
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

volumes:
  postgres_data:
  redis_data:
```

### Стъпка 2: Стартиране на Docker контейнерите
```bash
docker compose up -d
```

Проверете статуса на контейнерите:
```bash
docker compose ps
```

---

## 🔄 3. Управление на бази данни и миграции (EF Core Migrations)

CALAC използва Entity Framework Core за управление на базата данни. Всички промени по домейните се записват като миграции в проекта `CALAC.Infrastructure`.

### Добавяне на нова миграция (при промяна на моделите)
За да добавите миграция, трябва да имате инсталиран глобалния dotnet-ef инструмент и да добавите пътя му в променливите на средата:
```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef migrations add <ИмеНаМиграция> --project src/CALAC.Infrastructure --startup-project src/CALAC.Api
```

### Прилагане на миграциите в базата данни
Системата автоматично прилага миграциите при стартиране на API приложението чрез вградения метод `EnsureCreatedAsync` или чрез прилагане на миграции:
```bash
dotnet ef database update --project src/CALAC.Infrastructure --startup-project src/CALAC.Api
```

---

## ⚡ 4. Покриване на проблеми (Troubleshooting)

### Проблеми с Docker на специфични Linux системи (overlayfs грешки)
Ако стартирате Docker Compose на среди с ограничения на ядрото или ограничени права (като виртуални контейнери/пясъчници), PostgreSQL контейнерът може да не успее да се стартира поради грешки при монтиране (`overlay: invalid argument`).
* **Решение**: CALAC разполага с вграден **SQLite fallback**. Спрете Docker контейнерите чрез `docker compose down` и стартирайте backend приложението локално — то автоматично ще създаде олекотена, напълно функционираща локална база данни `calac.db`, готова за пълноценна работа и демонстрации.

### Грешка "402 Payment Required" на API
Ако получавате грешка за липса на активен абонамент при заявки към API:
* **Решение**: Проверете дали SQLite или PostgreSQL е прясно сийдната. Вграденият `DbSeeder` автоматично създава безплатен активен абонамент "starter" за тестов клиент ("Демо компания"). Ако базата ви е стара, изтрийте файла `calac.db` (при SQLite) или почистете PostgreSQL таблиците и рестартирайте API сървъра, за да се задейства сийдването отново.
