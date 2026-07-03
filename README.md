# <img src="https://raw.githubusercontent.com/simple-icons/simple-icons/develop/icons/barcode.svg" width="48" height="48" /> CALAC

![CALAC CI](https://github.com/calac/platform/actions/workflows/build.yml/badge.svg)
![Version](https://img.shields.io/badge/версия-0.17.0-blue.svg)
![License](https://img.shields.io/badge/лиценз-MIT-green.svg)
![Platform](https://img.shields.io/badge/платформа-Android%20%7C%20Web%20%7C%20API-lightgrey.svg)

**CALAC** е модерна, високопроизводителна модулна платформа, проектирана за индустриална работа с баркод скенери и PDA устройства (**Zebra, Honeywell, Datalogic**). Системата осигурява пълен контрол върху складовите процеси, проследяемост на наличностите и оптимизация на логистичните операции.

---

## 🚀 Технологичен стек

| Компонент | Технологии |
|-----------|------------|
| **Backend** | ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?logo=postgresql&logoColor=white) ![Entity Framework](https://img.shields.io/badge/EF%20Core-512BD4?logo=dotnet&logoColor=white) |
| **Admin Panel** | ![React](https://img.shields.io/badge/React-20232A?logo=react&logoColor=61DAFB) ![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?logo=typescript&logoColor=white) ![Vite](https://img.shields.io/badge/Vite-646CFF?logo=vite&logoColor=white) ![PWA](https://img.shields.io/badge/PWA-5A0FC8?logo=pwa&logoColor=white) |
| **Mobile Application** | ![Kotlin](https://img.shields.io/badge/Kotlin-7F52FF?logo=kotlin&logoColor=white) ![Android](https://img.shields.io/badge/Android-3DDC84?logo=android&logoColor=white) |
| **Infrastructure** | ![Docker](https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=white) ![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-000000?logo=opentelemetry&logoColor=white) ![GitHub Actions](https://img.shields.io/badge/CI/CD-2088FF?logo=github-actions&logoColor=white) |

---

## 📦 Реализирани модули

Платформата е изградена на модулен принцип, като към момента са напълно функционални следните направления:

### 🔐 1. Сигурност и Основа
- **JWT Автентикация:** Поддръжка на стандартен вход и бърз достъп чрез PIN.
- **Refresh Token Rotation:** Автоматично обновяване на сесиите и сигурно отписване.
- **RBAC:** Управление на потребители и роли (Admin, Supervisor, Operator).
- **Multi-tenancy:** Пълна изолация на данните на ниво база данни чрез Global Query Filters.
- **Audit Log:** Детайлно проследяване на всяко действие в системата.

### 🏗️ 2. Складова Инфраструктура
- **Управление на Локации:** Йерархична структура (Зона, Ред, Рафт, Ниво, Позиция).
- **Номенклатура Артикули:** Поддръжка на SKU, баркодове (1D/2D), мерни единици и тегло.
- **ZPL Печат:** Генерация и печат на етикети за артикули и локации (Zebra стандарт).
- **Labelary Интеграция:** Визуализация на етикети директно в админ панела.

### ⚙️ 3. Оперативни Модули
- **Инвентаризация:** Управление на сесии, частични и пълни инвентаризации, Cycle Counting.
- **Приемане на стоки (Goods Receipt):** Заприходяване срещу документи с проверка на количества.
- **Вътрешни Трансфери:** Преместване на стока между складови локации.
- **Комисиониране (Picking):** Оптимизиран workflow със стратегии **FEFO (First-Expired, First-Out)** и **FIFO**.
- **Управление на Задачи:** Разпределение на задачи към оператори с приоритети и статуси в реално време.

### 🔄 4. Интеграция и Мониторинг
- **ERP Адаптер:** Базова интеграция с Odoo и Dynamics 365.
- **Offline Sync:** Опашка за операции при липса на мрежа с Last-Write-Wins стратегия.
- **Real-time Нотификации:** Сигнализация чрез SignalR за критични складови събития.
- **Телеметрия:** Мониторинг на метрики чрез OpenTelemetry и Prometheus.
- **PWA Поддръжка:** Админ панелът може да бъде инсталиран като мобилно приложение.

---

## 📂 Структура на проекта

```text
CALAC/
├── backend/           # .NET 8 REST API (Clean Architecture)
├── admin/             # React 19 админ панел (Vite + TypeScript)
├── mobile/android/    # Kotlin PDA приложение (Room DB + Retrofit)
├── docs/              # Техническа документация и изисквания
├── CHANGELOG.md       # История на версиите
└── docker-compose.yml # Инфраструктура (PostgreSQL, Redis, Prometheus)
```

---

## 🛠️ Системни изисквания

- **Backend:** .NET 8 SDK
- **Frontend:** Node.js 20+ & NPM
- **Database:** PostgreSQL (чрез Docker)
- **Mobile:** Android Studio & PDA устройство (Zebra/Honeywell)

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
- **Swagger UI:** `http://localhost:5000/swagger`

### 3. Админ Панел
```bash
cd admin
npm install
npm run dev
```
- **URL:** `http://localhost:5173`

---

## 📈 План за развитие

- [ ] **AI Optimization:** Прогнозиране на наличности.
- [ ] **Voice Picking:** Гласови напътствия за оператори.
- [ ] **3D Warehouse Map:** Визуализация на склада.
- [ ] **Advanced Analytics:** BI табла за мениджмънта.

Подробна история на промените можете да намерите в [CHANGELOG.md](./CHANGELOG.md).

---

© 2026 CALAC Platform. Всички права запазени.
