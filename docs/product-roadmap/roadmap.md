# CALAC Product Roadmap (Пътна карта)

## Визия
CALAC да се развие в модулна WMS/warehouse execution платформа за складове, 3PL оператори и e-commerce fulfillment центрове, с възможност за SaaS доставка и по-широка интеграция с бизнес процеси.

## Текущо състояние (2026-07-14)

### ✅ Завършено
- Основен backend за складови операции: локации, артикули, приемане, трансфери, picking, задачи.
- Multi-tenancy, JWT, RBAC и audit log.
- Planned cycle counting по зона/категория.
- Batch/wave picking.
- Forecasting за нива на наличности.
- Expiry alerts и защита от дублиране.
- Webhook subscriptions и partner API ключове.
- Self-service tenant onboarding и базово active subscription plan управление.
- Tenant branding configuration за white-label персонализация.
- OpenTelemetry, SignalR, ZPL/Labelary и PWA admin support.

### ⏭️ Следващи приоритети
- **Stripe Integration**: Автоматизирани плащания и лимити на ресурсите.
- **Advanced Logistics**: Директна интеграция с Econt/Speedy за товарителници.
- **Voice picking**: "Hands-free" операции чрез гласови команди.
- **Route Optimization**: Интелигентни маршрути в склада.
- **AI Analytics**: Прогнозиране на дефицити и аномалии.

## Фази на развитие

### 1. Функционално разширение (Functional expansion)
- **Analytics Dashboard**: Пълна визуализация на KPI метрики.
- **Custom Workflows**: Гъвкави процеси за приемане и експедиция.
- **Advanced Alerts**: Нотификации чрез Telegram/Slack за критични събития.

### 2. Интеграции (Integrations)
- **Shipping Hub**: Модул за управление на пратки към множество куриери.
- **E-commerce Connectors**: Готови плъгини за Shopify и WooCommerce.
- **ERP Adapters**: Разширяване на поддръжката за SAP Business One и Microsoft Business Central.

### 3. SaaS готовност (SaaS readiness)
- **Subscription Engine**: Гъвкаво ценообразуване според броя потребители или транзакции.
- **White-labeling**: Пълно брандиране на портала и мобилното приложение.
- **Partner Portal**: Специализиран достъп за външни доставчици.

### 4. UX и иновации (UX & Innovation)
- **Voice Guidance**: Гласови инструкции при picking и инвентаризация.
- **Interactive Map**: Визуална карта на склада в реално време.
- **Augmented Reality**: Навигация в склада чрез AR.

### 5. AI/ML възможности (AI/ML opportunities)
- **Predictive Maintenance**: Следене на състоянието на PDA устройствата.
- **Inventory Heatmap**: Визуализация на натоварването по зони.
- **Auto-slotting**: Оптимизация на складовите места.

### 6. Производство и качество (Manufacturing & QA)
- **Manufacturing Orders**: Управление на сглобяване и разглобяване (kitting).
- **QC Checkpoints**: Задължителни стъпки за контрол на качеството.

## 📈 Етапи (Milestones)

### Етап 1: SaaS Финанси (Q3 2026)
- Stripe SDK, Портал за абонаменти, Ограничаване на функции.

### Етап 2: Логистика и E-commerce (Q4 2026)
- Econt/Speedy API, Синхронизация с WooCommerce/Shopify.

### Етап 3: Гласово управление (Q1 2027)
- Voice UI, Гласово потвърждение на SKU.

### Етап 4: AI и Оптимизация (Q2 2027)
- Оптимизация на маршрути, Откриване на аномалии, Dynamic Slotting.

### Етап 5: Мащабиране за корпорации (Q3 2027)
- Multi-region, Разширено White-labeling, BI интеграция.

## ✨ Конкурентни предимства
- Offline-first архитектура, Пълна Traceability, Бърза ERP интеграция.

## 👥 Потребителски персони
- Оператор, Складов мениджър, Системен администратор.

## 🛡️ Съответствие (Compliance)
- GDPR, ISO 27001 Readiness, GS1 Standards.

## 🔭 Бъдещи R&D направления (отвъд 2027)
- Роботизирана интеграция (AMR).
- Computer Vision броене и дефектовка.
- IoT складове (Smart Shelves).

## 🛠️ Техническа стратегия

### API и сигурност
- v1 API Версиониране, mTLS, SSO/OIDC поддръжка.

### SLA и Мониторинг
- 99.9% наличност, Мониторинг на латентността (P95), Grafana табла.
