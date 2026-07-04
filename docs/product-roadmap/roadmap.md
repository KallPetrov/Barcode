# CALAC Product Roadmap

## Vision
CALAC да се развие в модулна WMS/warehouse execution платформа за складове, 3PL оператори и e-commerce fulfillment центрове, с възможност за SaaS доставка и по-широка интеграция с бизнес процеси.

## Текущо състояние (2026-07-04)

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
- Stripe billing и lifecycle на планове.
- White-label branding за tenants.
- Voice picking за PDA устройства.
- Route optimization за picking.
- Advanced analytics и AI-assisted recommendations.
- Shipping и e-commerce интеграции за външни логистични партньори.

## Фази на развитие

### 1. Functional expansion
- Analytics dashboard с KPI карти за picking accuracy, task duration и inventory turnover.
- Допълнително усъвършенстване на forecasting и alerting.
- Route optimization за picking пътища.

### 2. Integrations
- Shipping интеграции за Econt, Speedy и други локални партньори.
- E-commerce конектори за Shopify и WooCommerce.
- Разширяване на webhook и API ecosystem.

### 3. SaaS readiness
- Stripe billing и абонаментна логика.
- White-label admin panel и tenant branding.
- По-добра администраторска и customer onboarding experience.

### 4. UX improvements
- Voice picking за PDA устройства.
- Guided onboarding за admins и supervisors.

### 5. AI/ML opportunities
- Anomaly detection за inventory adjustments.
- Dynamic slotting recommendations.
- Computer vision за inbound counting.
