# CALAC Roadmap

## Цел
CALAC да се развие в модулна WMS/warehouse execution платформа за складове, 3PL оператори и e-commerce fulfillment центрове, с възможност за SaaS доставка и по-широка интеграция с бизнес процеси.

## Текущо състояние (2026-07-08)

### ✅ Направено до момента
- Основен backend за складови операции: локации, артикули, приемане, трансфери, picking и задачи.
- Multi-tenancy, JWT, RBAC и audit log.
- Planned cycle counting по зона/категория.
- Batch/wave picking workflow.
- Forecasting за нива на наличности.
- Expiry alerts с защита от дублиране.
- Webhook subscriptions и partner API ключове.
- Self-service tenant onboarding и базово активиране на subscription plan.
- Tenant branding configuration за white-label персонализация.
- OpenTelemetry, SignalR, ZPL/Labelary и PWA admin support.

### ⏭️ Следващи приоритети
- **Stripe Integration**: Пълна автоматизация на плащанията, trial периоди и управление на лимити (active users/tenants).
- **Advanced Logistics**: Интеграция със застрахователни компании и куриерски услуги (Econt/Speedy) за автоматизирано генериране на товарителници.
- **Voice picking & UX**: Внедряване на гласови команди за PDA устройства с цел "hands-free" работа в склада.
- **Route Optimization**: Алгоритми за най-кратък път при picking и подредба на задачите по зони.
- **AI-Driven Analytics**: Прогнозиране на закъснения, оптимизация на слотовете (dynamic slotting) и откриване на аномалии в инвентара.

## Планирани фази

### 1. Еволюция на функционалностите (Functional expansion)
- **Analytics Dashboard**: KPI карти за точност на picking-а, продължителност на задачите и оборот на инвентара.
- **Advanced Reporting**: Експорт на детайлни справки в PDF/Excel и автоматично изпращане на имейл към мениджмънта.
- **Custom Workflows**: Възможност за дефиниране на специфични стъпки при приемане и проверка на стоката.

### 2. Силна екосистема от интеграции (Integrations)
- **Logistics Connectors**: Директна връзка с Econt, Speedy, DHL и FedEx.
- **E-commerce Hub**: Двупосочна синхронизация с Shopify, WooCommerce, Magento и Amazon.
- **Marketplace Sync**: Интеграция с Emag и други регионални маркетплейси.
- **Hardware Abstraction Layer**: Разширена поддръжка за индустриални етикетни принтери и IoT сензори за температура/влажност.

### 3. SaaS & Scalability
- **Stripe Billing**: Автоматизирано фактуриране и управление на абонаментни планове.
- **White-label Capabilities**: Възможност за пълно персонализиране на URL, лога и цветове за големи корпоративни клиенти.
- **Multi-Region Support**: Поддръжка на различни часови зони, валути и данъчни изисквания.

### 4. Иновации в UX (Next-Gen UX)
- **Voice Picking**: Поддръжка на Text-to-Speech и Speech-to-Text за PDA устройства.
- **Augmented Reality (AR)**: Експериментални функции за насочване на операторите чрез AR очила или камера на PDA.
- **Gamification**: Система за точки и класации за операторите с цел повишаване на мотивацията.

### 5. Интелигентно управление (AI/ML)
- **Anomaly Detection**: Автоматично откриване на подозрителни корекции в наличностите.
- **Dynamic Slotting**: Препоръки за преместване на бързооборотни стоки в по-достъпни зони.
- **Computer Vision**: Броене на стоки при приемане чрез камерата на PDA устройството.

### 6. Производство и качество (Manufacturing & QA)
- **Bill of Materials (BOM)**: Управление на компоненти за леки производствени процеси и комплектоване.
- **Quality Control (QC)**: Дефиниране на контролни листове и задължителни снимки при приемане на критични стоки.

## 📈 Детайлни етапи (Milestones)

### Milestone 1: SaaS Financials (Q3 2026)
- [ ] Интеграция на Stripe SDK в бекенда.
- [ ] Портал за управление на абонаменти в админ панела.
- [ ] Автоматично ограничаване на функционалности според избрания план.

### Milestone 2: Logistics & E-commerce (Q4 2026)
- [ ] API конектори за Еконт и Спиди (генериране на товарителници).
- [ ] WooCommerce & Shopify Sync (импорт на поръчки, експорт на наличности).
- [ ] Единно табло за статус на пратките.

### Milestone 3: Voice-Activated Warehouse (Q1 2027)
- [ ] Гласов интерфейс за Kotlin PDA приложението.
- [ ] Текстово потвърждение на сканирани SKU чрез Speech-to-Text.
- [ ] Гласово насочване към следваща локация.

### Milestone 4: AI & Optimization (Q2 2027)
- [ ] Внедряване на алгоритми за Route Optimization (най-кратък път в склада).
- [ ] AI-базиран Anomaly Detection за откриване на несъответствия в наличностите.
- [ ] Dynamic Slotting Engine (препоръки за пренареждане на стоки).

### Milestone 5: Enterprise Scaling (Q3 2027)
- [ ] Multi-region поддръжка (валути, часови зони, данъци).
- [ ] Advanced White-labeling (персонализирани домейни и пълно брандиране).
- [ ] BI интеграция за комплексни корпоративни справки.
