import React, { useEffect, useState } from 'react';
import { getShipments, generateWaybill } from '../api/client';
import type { Shipment } from '../api/client';
import { Package, Truck, ExternalLink, RefreshCw, Printer } from 'lucide-react';

export function ShippingDashboardPage() {
  const [shipments, setShipments] = useState<Shipment[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadShipments();
  }, []);

  const loadShipments = async () => {
    setLoading(true);
    try {
      const data = await getShipments();
      setShipments(data);
    } catch (error) {
      console.error('Failed to load shipments', error);
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateWaybill = async (id: string) => {
    try {
      await generateWaybill(id);
      loadShipments();
    } catch (error) {
      console.error('Failed to generate waybill', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'LabelGenerated': return 'bg-blue-100 text-blue-800';
      case 'InTransit': return 'bg-yellow-100 text-yellow-800';
      case 'Delivered': return 'bg-green-100 text-green-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="p-6">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold flex items-center gap-2">
          <Truck className="w-6 h-6 text-blue-600" />
          Управление на пратки (Shipping)
        </h1>
        <button
          onClick={loadShipments}
          className="flex items-center gap-2 px-4 py-2 bg-gray-100 rounded hover:bg-gray-200 transition-colors"
        >
          <RefreshCw className="w-4 h-4" /> Обнови
        </button>
      </div>

      <div className="bg-white rounded-lg shadow overflow-hidden">
        <table className="w-full text-left">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-6 py-3 text-sm font-semibold text-gray-600">ID / Реф.</th>
              <th className="px-6 py-3 text-sm font-semibold text-gray-600">Получател</th>
              <th className="px-6 py-3 text-sm font-semibold text-gray-600">Статус</th>
              <th className="px-6 py-3 text-sm font-semibold text-gray-600">Товарителница</th>
              <th className="px-6 py-3 text-sm font-semibold text-gray-600">Действия</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {loading ? (
              <tr><td colSpan={5} className="px-6 py-4 text-center">Зареждане...</td></tr>
            ) : shipments.length === 0 ? (
              <tr><td colSpan={5} className="px-6 py-4 text-center">Няма открити пратки</td></tr>
            ) : shipments.map(s => (
              <tr key={s.id}>
                <td className="px-6 py-4 text-sm">
                  <div className="font-medium text-gray-900">{s.referenceNumber || s.id.substring(0, 8)}</div>
                  <div className="text-gray-500 text-xs">{new Date(s.createdAt).toLocaleString('bg-BG')}</div>
                </td>
                <td className="px-6 py-4 text-sm">
                  <div className="font-medium text-gray-900">{s.receiverName}</div>
                  <div className="text-gray-500 text-xs">{s.receiverCity}, {s.receiverPhone}</div>
                </td>
                <td className="px-6 py-4 text-sm">
                  <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStatusColor(s.status)}`}>
                    {s.status}
                  </span>
                </td>
                <td className="px-6 py-4 text-sm">
                  {s.waybillNumber ? (
                    <div className="flex flex-col gap-1">
                      <span className="font-mono text-blue-600 font-semibold">{s.waybillNumber}</span>
                      {s.trackingUrl && (
                        <a href={s.trackingUrl} target="_blank" rel="noreferrer" className="text-xs flex items-center gap-1 text-gray-500 hover:text-blue-500">
                          Проследи <ExternalLink className="w-3 h-3" />
                        </a>
                      )}
                    </div>
                  ) : (
                    <span className="text-gray-400 italic">Негенерирана</span>
                  )}
                </td>
                <td className="px-6 py-4 text-sm">
                  <div className="flex gap-2">
                    {!s.waybillNumber && (
                      <button
                        onClick={() => handleGenerateWaybill(s.id)}
                        className="px-3 py-1 bg-blue-600 text-white rounded text-xs hover:bg-blue-700 transition-colors"
                      >
                        Генерирай Т-ца
                      </button>
                    )}
                    {s.labelPdfUrl && (
                      <a
                        href={s.labelPdfUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="p-1 bg-gray-100 rounded text-gray-600 hover:bg-gray-200 transition-colors"
                        title="Принтирай етикет"
                      >
                        <Printer className="w-4 h-4" />
                      </a>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
