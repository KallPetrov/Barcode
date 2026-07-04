# <img src="https://raw.githubusercontent.com/simple-icons/simple-icons/develop/icons/barcode.svg" width="48" height="48" /> CALAC

![CALAC CI](https://github.com/calac/platform/actions/workflows/build.yml/badge.svg)
![Version](https://img.shields.io/badge/версия-0.21.0-blue.svg)
![License](https://img.shields.io/badge/лиценз-MIT-green.svg)
![Platform](https://img.shields.io/badge/платформа-Android%20%7C%20Web%20%7C%20API-lightgrey.svg)

**CALAC** е модулна WMS/warehouse execution платформа за складове, 3PL оператори и e-commerce fulfillment центрове. Към момента проектът е надграден от базовия прототип до работеща backend-ориентирана система с реални складови процеси, интеграции и SaaS подготовка.

---

## 📌 Текущо състояние (2026-07-06)

CALAC вече включва:
- **мулти-тенант сигурност** с JWT, refresh tokens, RBAC и audit log;
- **основни складови процеси** — локации, артикули, приемане, трансфери, picking, задачи;
- **по-напреднали операции** — planned cycle counting, batch/wave picking, FEFO/FIFO workflow и expiry alerts;
- **интеграционен слой** — webhooks, partner API ключове, ERP-ориентирана структура и SignalR нотификации;
- **SaaS readiness** — self-service onboarding и активиране на tenant subscription plan;
- **оперативна инфраструктура** — OpenTelemetry, Docker, PWA admin panel и ZPL/Labelary поддръжка.

---

## 🚀 Технологичен стек

| Компонент | Технологии |
|-----------|------------|
| **Backend** | .NET 8, C#, ASP.NET Core Web API, EF Core, PostgreSQL/SQLite, JWT, SignalR |
| **Admin Panel** | React, TypeScript, Vite, PWA |
| **Mobile Application** | Kotlin, Android, PDA-ready workflow |
| **Infrastructure** | Docker, OpenTelemetry, Prometheus, GitHub Actions |

---

## 📦 Реализирани модули

### 🔐 Сигурност и основа
- JWT автентикация и refresh token ротация
- RBAC за Admin, Supervisor и Operator
- Multi-tenancy с tenant isolation
- Audit log за критични операции

### 🏗️ Складови процеси
- Управление на локации и артикули
- Goods receipt и internal transfers
- Picking с FEFO/FIFO логика
- Управление на задачи и операторски workflow
- Planned cycle counting по зона/категория
- Batch и wave picking

### 🔄 Интеграции и мониторинг
- Webhook subscriptions за ERP/външни системи
- Partner API ключове
- SignalR нотификации в реално време
- OpenTelemetry метрики и monitoring
- ZPL/Labelary етикети за печат

### ☁️ SaaS подготовка
- Self-service tenant onboarding
- Tenant subscription plan activation
- Подготовка за по-нататъшно billing и white-label разширение

---

## 📂 Структура на проекта

```text
CALAC/
├── backend/           # .NET 8 REST API и бизнес логика
├── admin/             # React + TypeScript админ панел
├── mobile/android/    # Kotlin PDA/mobile приложение
├── docs/              # Техническа документация и roadmap
├── CHANGELOG.md       # История на версиите
├── ROADMAP.md         # Текуща продуктова стратегия
└── docker-compose.yml # Инфраструктура и dev среда
```

---

## 🛠️ Системни изисквания

- Backend: .NET 8 SDK
- Frontend: Node.js 20+ и npm
- Database: PostgreSQL или SQLite за локално развитие
- Mobile: Android Studio и PDA устройство

---

## 🚦 Бърз старт

### 1. Инфраструктура
```bash
docker compose up -d
```

### 2. Backend API
```bash
cd backend
dotnet restore
dotnet run --project src/CALAC.Api
```
- Swagger UI: http://localhost:5000/swagger

### 3. Админ панел
```bash
cd admin
npm install
npm run dev
```
- URL: http://localhost:5173

---

## 🗺️ Документация

- [ROADMAP.md](./ROADMAP.md) — текуща продуктова стратегия и приоритети
- [CHANGELOG.md](./CHANGELOG.md) — история на промените
- [docs/product-roadmap/roadmap.md](./docs/product-roadmap/roadmap.md) — допълнителен roadmap преглед
- [docs/Readme-New_systema.md](./docs/Readme-New_systema.md) — по-широко описание на системата
- [docs/moduli-new_system.md](./docs/moduli-new_system.md) — модулна структура

---

## 🔭 Следващи приоритети

- Stripe Integration и автоматизирано таксуване
- Advanced Logistics (Econt/Speedy)
- Voice picking за PDA устройства
- Route optimization за picking
- AI-Driven Analytics и аномалии
- Manufacturing (BOM) и QA контрол

© 2026 CALAC Platform. Всички права запазени.
