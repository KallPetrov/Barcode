import React, { useEffect, useState } from 'react';
import { getSubscription, createCheckoutSession, createPortalSession } from '../api/client';
import type { TenantSubscription } from '../api/client';
import { Check, CreditCard, ExternalLink, Shield } from 'lucide-react';

const BillingPage: React.FC = () => {
  const [subscription, setSubscription] = useState<TenantSubscription | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadSubscription();
  }, []);

  const loadSubscription = async () => {
    try {
      const data = await getSubscription();
      setSubscription(data);
    } catch (error) {
      console.error('Failed to load subscription', error);
    } finally {
      setLoading(false);
    }
  };

  const handleUpgrade = async (planCode: string) => {
    try {
      const { url } = await createCheckoutSession({
        planCode,
        successUrl: window.location.origin + '/billing?success=true',
        cancelUrl: window.location.origin + '/billing?cancelled=true',
      });
      window.location.href = url;
    } catch (error) {
      console.error('Failed to initiate checkout', error);
      alert('Възникна грешка при свързването със Stripe.');
    }
  };

  const handlePortal = async () => {
    try {
      const { url } = await createPortalSession({
        returnUrl: window.location.origin + '/billing',
      });
      window.location.href = url;
    } catch (error) {
      console.error('Failed to open portal', error);
    }
  };

  if (loading) return <div className="p-8 text-center">Зареждане на информация за абонамента...</div>;

  const plans = [
    {
      code: 'starter',
      name: 'Starter',
      price: '49€',
      features: ['До 2 склада', '5 потребителя', 'Основни WMS функции', 'Mobile PDA support'],
    },
    {
      code: 'professional',
      name: 'Professional',
      price: '149€',
      features: ['Неограничени складове', '20 потребителя', 'ERP Интеграции', 'Advanced Analytics'],
    },
    {
      code: 'enterprise',
      name: 'Enterprise',
      price: '499€',
      features: ['Custom лимити', 'White-labeling', 'SLA поддръжка', 'Dedicated Support'],
    },
  ];

  return (
    <div className="max-w-6xl mx-auto p-4 md:p-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-2">Абонамент и Плащания</h1>
        <p className="text-gray-600">Управлявайте вашия план и финансови настройки.</p>
      </div>

      {subscription && subscription.isActive ? (
        <div className="bg-green-50 border border-green-200 rounded-lg p-6 mb-12 flex items-center justify-between">
          <div>
            <div className="flex items-center text-green-700 font-semibold mb-1">
              <Shield className="w-5 h-5 mr-2" />
              Активен абонамент: <span className="ml-2 uppercase">{subscription.planCode}</span>
            </div>
            <p className="text-green-600 text-sm">Вашият абонамент е активен и се подновява автоматично.</p>
          </div>
          <button
            onClick={handlePortal}
            className="flex items-center px-4 py-2 bg-white border border-green-300 text-green-700 rounded-md hover:bg-green-100 transition-colors shadow-sm"
          >
            <CreditCard className="w-4 h-4 mr-2" />
            Управление на плащанията
            <ExternalLink className="w-4 h-4 ml-2" />
          </button>
        </div>
      ) : (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-6 mb-12">
          <div className="flex items-center text-yellow-700 font-semibold mb-1">
            Внимание: Нямате активен абонамент
          </div>
          <p className="text-yellow-600 text-sm">Моля, изберете план по-долу, за да продължите да използвате всички функции на платформата.</p>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        {plans.map((plan) => (
          <div key={plan.code} className={`border rounded-xl p-8 flex flex-col ${subscription?.planCode === plan.code ? 'ring-2 ring-blue-500 border-transparent shadow-lg' : 'bg-white shadow-sm'}`}>
            <div className="mb-6">
              <h2 className="text-xl font-bold text-gray-900 mb-2">{plan.name}</h2>
              <div className="text-4xl font-extrabold text-gray-900">{plan.price}<span className="text-lg font-normal text-gray-500">/мес</span></div>
            </div>
            <ul className="space-y-4 mb-8 flex-grow">
              {plan.features.map((feature) => (
                <li key={feature} className="flex items-start text-gray-600">
                  <Check className="w-5 h-5 text-blue-500 mr-2 flex-shrink-0" />
                  {feature}
                </li>
              ))}
            </ul>
            <button
              onClick={() => handleUpgrade(plan.code)}
              disabled={subscription?.planCode === plan.code}
              className={`w-full py-3 px-4 rounded-lg font-semibold transition-colors ${
                subscription?.planCode === plan.code
                  ? 'bg-gray-100 text-gray-400 cursor-not-allowed'
                  : 'bg-blue-600 text-white hover:bg-blue-700'
              }`}
            >
              {subscription?.planCode === plan.code ? 'Текущ план' : 'Избор на план'}
            </button>
          </div>
        ))}
      </div>
    </div>
  );
};

export default BillingPage;
