import { useEffect, useState, type FormEvent } from 'react';
import {
  getPickingOrders,
  getPickingOrder,
  createPickingOrder,
  startPickingOrder,
  completePickingOrder,
  updatePickingStockLine,
  getItems,
  getLocations,
  type PickingOrder,
  type Item,
  type Location,
} from '../api/client';

export function PickingPage() {
  const [orders, setOrders] = useState<PickingOrder[]>([]);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [selectedOrder, setSelectedOrder] = useState<PickingOrder | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [items, setItems] = useState<Item[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [form, setForm] = useState({
    name: '',
    reference: '',
    strategy: 'FIFO',
    notes: '',
    lines: [{ itemId: '', sourceLocationId: '', targetLocationId: '', quantity: 0, notes: '' }],
  });
  const [error, setError] = useState('');

  const loadOrders = () => getPickingOrders().then(setOrders);
  useEffect(() => {
    loadOrders();
    getItems().then(setItems);
    getLocations().then(setLocations);
  }, []);

  const handleSelectOrder = async (id: string) => {
    setSelectedOrderId(id);
    const order = await getPickingOrder(id);
    setSelectedOrder(order);
  };

  const handleCreateLine = () => {
    setForm({
      ...form,
      lines: [...form.lines, { itemId: '', sourceLocationId: '', targetLocationId: '', quantity: 0, notes: '' }],
    });
  };

  const handleUpdateLine = (index: number, key: string, value: any) => {
    const newLines = [...form.lines];
    newLines[index] = { ...newLines[index], [key]: value };
    setForm({ ...form, lines: newLines });
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createPickingOrder({
        ...form,
        lines: form.lines.filter(l => l.itemId && l.quantity > 0),
      });
      setShowForm(false);
      setForm({
        name: '',
        reference: '',
        strategy: 'FIFO',
        notes: '',
        lines: [{ itemId: '', sourceLocationId: '', targetLocationId: '', quantity: 0, notes: '' }],
      });
      await loadOrders();
    } catch {
      setError('Грешка при създаване на поръчка');
    }
  };

  const handleStart = async (id: string) => {
    try {
      await startPickingOrder(id);
      await loadOrders();
      if (selectedOrderId === id) {
        const updated = await getPickingOrder(id);
        setSelectedOrder(updated);
      }
    } catch {
      setError('Грешка при стартиране');
    }
  };

  const handleComplete = async (id: string) => {
    try {
      await completePickingOrder(id);
      await loadOrders();
      if (selectedOrderId === id) {
        const updated = await getPickingOrder(id);
        setSelectedOrder(updated);
      }
    } catch {
      setError('Грешка при завършване');
    }
  };

  const handleUpdateStockLine = async (lineId: string, qty: number) => {
    try {
      const updated = await updatePickingStockLine(lineId, { pickedQuantity: qty });
      if (selectedOrderId === updated.id) {
        setSelectedOrder(updated);
      }
      await loadOrders();
    } catch {
      setError('Грешка при актуализация');
    }
  };

  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case 'Draft':
        return 'badge';
      case 'InProgress':
        return 'badge online';
      case 'Completed':
        return 'badge online';
      default:
        return 'badge offline';
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Picking поръчки</h1>
          <p>Управление на picking операции</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Нова поръчка'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleSubmit} style={{ gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
          {error && <div className="error">{error}</div>}
          <label style={{ gridColumn: 'span 2' }}>Име <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Референция <input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></label>
          <label>Стратегия
            <select value={form.strategy} onChange={(e) => setForm({ ...form, strategy: e.target.value })}>
              <option value="FIFO">FIFO (First In, First Out)</option>
              <option value="FEFO">FEFO (First Expired, First Out)</option>
            </select>
          </label>
          <label style={{ gridColumn: 'span 2' }}>Бележки <textarea value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></label>
          <div style={{ gridColumn: 'span 2' }}>
            <h4 style={{ marginBottom: '0.5rem' }}>Линии</h4>
            {form.lines.map((line, index) => (
              <div key={index} style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr', gap: '0.5rem', marginBottom: '0.5rem', alignItems: 'end' }}>
                <label>Артикул
                  <select required value={line.itemId} onChange={(e) => handleUpdateLine(index, 'itemId', e.target.value)}>
                    <option value="">Избери...</option>
                    {items.map(i => <option key={i.id} value={i.id}>{i.sku} - {i.name}</option>)}
                  </select>
                </label>
                <label>От локация
                  <select value={line.sourceLocationId} onChange={(e) => handleUpdateLine(index, 'sourceLocationId', e.target.value)}>
                    <option value="">Избери...</option>
                    {locations.map(l => <option key={l.id} value={l.id}>{l.code} - {l.name}</option>)}
                  </select>
                </label>
                <label>До локация
                  <select value={line.targetLocationId} onChange={(e) => handleUpdateLine(index, 'targetLocationId', e.target.value)}>
                    <option value="">Избери...</option>
                    {locations.map(l => <option key={l.id} value={l.id}>{l.code} - {l.name}</option>)}
                  </select>
                </label>
                <label>К-во
                  <input type="number" min="0" step="0.01" required value={line.quantity} onChange={(e) => handleUpdateLine(index, 'quantity', parseFloat(e.target.value) || 0)} />
                </label>
              </div>
            ))}
            <button type="button" style={{ marginBottom: '1rem' }} onClick={handleCreateLine}>+ Добави линия</button>
          </div>
          <div style={{ gridColumn: 'span 2' }}>
            <button type="submit">Създай поръчка</button>
          </div>
        </form>
      )}

      {selectedOrder ? (
        <div className="panel" style={{ marginTop: '2rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <div>
              <h3>{selectedOrder.name} <span className={getStatusBadgeClass(selectedOrder.status)} style={{ marginLeft: '1rem' }}>{selectedOrder.status}</span></h3>
              <p style={{ color: '#666' }}>{selectedOrder.reference}</p>
            </div>
            <div className="row">
              <button type="button" onClick={() => setSelectedOrder(null)}>Назад</button>
              {selectedOrder.status === 'Draft' && <button type="button" onClick={() => handleStart(selectedOrder.id)}>Стартирай</button>}
              {selectedOrder.status === 'InProgress' && <button type="button" onClick={() => handleComplete(selectedOrder.id)}>Завърши</button>}
            </div>
          </div>
          {selectedOrder.lines.map(line => (
            <div key={line.id} style={{ marginBottom: '2rem', paddingBottom: '1rem', borderBottom: '1px solid #ddd' }}>
              <h4 style={{ marginBottom: '1rem' }}>{line.itemName} (заявено: {line.quantity})</h4>
              {line.stockLines.map(sl => (
                <div key={sl.id} style={{ display: 'flex', gap: '1rem', alignItems: 'center', padding: '0.5rem', background: '#f8f9fa', borderRadius: '4px', marginBottom: '0.5rem' }}>
                  <span style={{ flex: 1 }}><strong>Локация:</strong> {sl.locationName}</span>
                  <span style={{ flex: 1 }}><strong>К-во:</strong> {sl.quantity}</span>
                  {sl.batchNumber && <span><strong>Партида:</strong> {sl.batchNumber}</span>}
                  {sl.serialNumber && <span><strong>Сериен номер:</strong> {sl.serialNumber}</span>}
                  {sl.expiryDate && <span><strong>Срок:</strong> {new Date(sl.expiryDate).toLocaleDateString('bg-BG')}</span>}
                  {selectedOrder.status === 'InProgress' && (
                    <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                      <label>Взето к-во:
                        <input
                          type="number"
                          min="0"
                          max={sl.quantity}
                          step="0.01"
                          value={sl.pickedAt ? sl.quantity : ''}
                          disabled={!!sl.pickedAt}
                          style={{ width: '100px' }}
                          onChange={(e) => {
                            if (!sl.pickedAt) {
                              handleUpdateStockLine(sl.id, parseFloat(e.target.value) || 0);
                            }
                          }}
                        />
                      </label>
                      {!sl.pickedAt && (
                        <button type="button" onClick={() => handleUpdateStockLine(sl.id, sl.quantity)} style={{ padding: '0.25rem 0.5rem', fontSize: '0.875rem' }}>Маркирай като взето</button>
                      )}
                    </div>
                  )}
                  {sl.pickedAt && (
                    <span style={{ color: '#009688', fontWeight: 'bold' }}>
                      ✓ Взето от {sl.pickedByUserName} на {new Date(sl.pickedAt).toLocaleString('bg-BG')}
                    </span>
                  )}
                </div>
              ))}
            </div>
          ))}
        </div>
      ) : (
        <div>
          {orders.length === 0 ? (
            <div className="panel empty">Няма picking поръчки</div>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Име</th>
                  <th>Референция</th>
                  <th>Статус</th>
                  <th>Стратегия</th>
                  <th>Създадено на</th>
                  <th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {orders.map(order => (
                  <tr key={order.id}>
                    <td><strong>{order.name}</strong></td>
                    <td>{order.reference}</td>
                    <td><span className={getStatusBadgeClass(order.status)}>{order.status}</span></td>
                    <td>{order.strategy}</td>
                    <td>{new Date(order.createdAt).toLocaleDateString('bg-BG')}</td>
                    <td>
                      <button type="button" onClick={() => handleSelectOrder(order.id)}>Виж детайли</button>
                      {order.status === 'Draft' && <button type="button" onClick={() => handleStart(order.id)}>Стартирай</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
