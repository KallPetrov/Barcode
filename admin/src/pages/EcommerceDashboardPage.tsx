import React, { useEffect, useState } from 'react';
import { getEcommerceStores, getEcommerceOrders, syncEcommerceStore, EcommerceStore, EcommerceOrder } from '../api/client';
import { ShoppingCart, Store, RefreshCw, ExternalLink, Package } from 'lucide-react';

export function EcommerceDashboardPage() {
  const [stores, setStores] = useState<EcommerceStore[]>([]);
  const [orders, setOrders] = useState<EcommerceOrder[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [storesData, ordersData] = await Promise.all([
        getEcommerceStores(),
        getEcommerceOrders()
      ]);
      setStores(storesData);
      setOrders(ordersData);
    } catch (error) {
      console.error('Failed to load ecommerce data', error);
    } finally {
      setLoading(false);
    }
  };

  const handleSyncStore = async (id: string) => {
    try {
      await syncEcommerceStore(id);
      loadData();
    } catch (error) {
      console.error('Failed to sync store', error);
    }
  };

  return (
    <div className="p-6 space-y-8">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <ShoppingCart className="w-6 h-6 text-indigo-600" />
          E-commerce Интеграция
        </h1>
        <button
          onClick={loadData}
          className="flex items-center gap-2 px-4 py-2 bg-gray-100 rounded hover:bg-gray-200 transition-colors"
        >
          <RefreshCw className="w-4 h-4" /> Обнови
        </button>
      </div>

      {/* Свързани магазини */}
      <section>
        <div className="flex justify-between items-center mb-4">
          <h2 className="text-lg font-semibold flex items-center gap-2">
            <Store className="w-5 h-5" /> Свързани магазини
          </h2>
          <button className="text-sm text-indigo-600 font-medium hover:underline">+ Добави магазин</button>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {stores.length === 0 ? (
            <div className="col-span-full bg-white p-8 text-center border-2 border-dashed rounded-lg text-gray-500">
              Няма свързани магазини
            </div>
          ) : stores.map(store => (
            <div key={store.id} className="bg-white p-4 rounded-lg shadow-sm border flex justify-between items-center">
              <div>
                <h3 className="font-bold text-gray-900">{store.name}</h3>
                <p className="text-xs text-gray-500">{store.platformType} • {store.storeUrl}</p>
                <div className="mt-1 flex items-center gap-2">
                  <span className={`w-2 h-2 rounded-full ${store.isActive ? 'bg-green-500' : 'bg-red-500'}`}></span>
                  <span className="text-xs text-gray-600">{store.isActive ? 'Активен' : 'Прекъснат'}</span>
                </div>
              </div>
              <button
                onClick={() => handleSyncStore(store.id)}
                className="p-2 text-indigo-600 hover:bg-indigo-50 rounded transition-colors"
                title="Синхронизирай поръчки"
              >
                <RefreshCw className="w-5 h-5" />
              </button>
            </div>
          ))}
        </div>
      </section>

      {/* Последни поръчки */}
      <section>
        <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
          <Package className="w-5 h-5" /> Последни поръчки от магазини
        </h2>
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <table className="w-full text-left">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-sm font-semibold text-gray-600">Поръчка #</th>
                <th className="px-6 py-3 text-sm font-semibold text-gray-600">Клиент</th>
                <th className="px-6 py-3 text-sm font-semibold text-gray-600">Сума</th>
                <th className="px-6 py-3 text-sm font-semibold text-gray-600">Статус</th>
                <th className="px-6 py-3 text-sm font-semibold text-gray-600">Дата</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {loading ? (
                <tr><td colSpan={5} className="px-6 py-4 text-center text-gray-500">Зареждане на поръчки...</td></tr>
              ) : orders.length === 0 ? (
                <tr><td colSpan={5} className="px-6 py-4 text-center text-gray-500">Няма открити поръчки</td></tr>
              ) : orders.map(order => (
                <tr key={order.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-6 py-4 text-sm">
                    <div className="font-bold text-indigo-600">#{order.orderNumber}</div>
                    <div className="text-xs text-gray-400">{order.ecommerceStoreName}</div>
                  </td>
                  <td className="px-6 py-4 text-sm font-medium text-gray-900">{order.customerName}</td>
                  <td className="px-6 py-4 text-sm font-bold text-gray-900">{order.totalAmount} {order.currency}</td>
                  <td className="px-6 py-4 text-sm">
                    <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded-full text-xs font-semibold">
                      {order.status}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-sm text-gray-500">
                    {new Date(order.orderCreatedAt).toLocaleString('bg-BG')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
