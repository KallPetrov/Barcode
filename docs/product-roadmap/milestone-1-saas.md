# Milestone 1: SaaS Financials (Stripe Integration)

## Цел
Автоматизация на процеса по абониране, плащане и управление на финансовите взаимоотношения с тенантите.

## Ключови компоненти

### 1. Stripe Бекенд Интеграция
- Интеграция на `Stripe.net` NuGet пакета.
- Създаване на `BillingService` за комуникация със Stripe API.
- Обработка на Stripe Webhooks (payment_succeeded, subscription_deleted).

### 2. Абонаментни планове (Tiers)
- **Starter**: До 2 склада, 5 потребителя, основни WMS функции.
- **Professional**: Неограничени складове, 20 потребителя, API достъп, ERP интеграции.
- **Enterprise**: Custom лимити, White-labeling, SLA поддръжка.

### 3. Feature Gating
- Middleware в .NET API за проверка на правата според абонаментния план.
- Динамично скриване/показване на елементи в админ панела според плана.

### 4. Billing Portal
- Интеграция на Stripe Customer Portal за самообслужване.
- История на плащанията и изтегляне на фактури.
