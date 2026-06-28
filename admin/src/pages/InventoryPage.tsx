import { useEffect, useState, type FormEvent } from 'react';
import { getInventoryStock, addInventoryStock, getItems, getLocations, type InventoryStock, type Item, type Location } from '../api/client';

export function InventoryPage() {
  const [stock, setStock] = useState<InventoryStock[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({
    itemId: '',
    locationId: '',
    quantity: 0,
    batchNumber: '',
    serialNumber: '',
    expiryDate: ''
  });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    try {
      const [stockData, itemsData, locationsData] = await Promise.all([
        getInventoryStock(),
        getItems(),
        getLocations()
      ]);
      setStock(stockData);
      setItems(itemsData);
      setLocations(locationsData);
    } catch (err) {
      setError('Грешка при зареждане на данните');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadData(); }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await addInventoryStock(form);
      setShowForm(false);
      setForm({ itemId: '', locationId: '', quantity: 0, batchNumber: '', serialNumber: '', expiryDate: '' });
      await loadData();
    } catch {
      setError('Грешка при добавяне на наличност');
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Инвентар</h1>
          <p>Управление на наличностите</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Добави наличност'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleSubmit}>
          {error && <div className="error">{error}</div>}
          <label>Артикул
            <select required value={form.itemId} onChange={(e) => setForm({ ...form, itemId: e.target.value })}>
              <option value="">Изберете артикул</option>
              {items.map(item => (
                <option key={item.id} value={item.id}>{item.sku} - {item.name}</option>
              ))}
            </select>
          </label>
          <label>Локация
            <select required value={form.locationId} onChange={(e) => setForm({ ...form, locationId: e.target.value })}>
              <option value="">Изберете локация</option>
              {locations.map(loc => (
                <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
              ))}
            </select>
          </label>
          <label>Количество
            <input required type="number" step="0.01" value={form.quantity} onChange={(e) => setForm({ ...form, quantity: parseFloat(e.target.value) || 0 })} />
          </label>
          <label>Партида
            <input value={form.batchNumber} onChange={(e) => setForm({ ...form, batchNumber: e.target.value })} />
          </label>
          <label>Сериен номер
            <input value={form.serialNumber} onChange={(e) => setForm({ ...form, serialNumber: e.target.value })} />
          </label>
          <label>Срок на годност
            <input type="date" value={form.expiryDate} onChange={(e) => setForm({ ...form, expiryDate: e.target.value })} />
          </label>
          <button type="submit">Добави</button>
        </form>
      )}

      {loading ? (
        <p>Зареждане...</p>
      ) : stock.length === 0 ? (
        <div className="panel empty">Няма наличности</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Артикул</th>
              <th>Локация</th>
              <th>Количество</th>
              <th>Партида</th>
              <th>Сериен номер</th>
              <th>Срок на годност</th>
            </tr>
          </thead>
          <tbody>
            {stock.map((s) => (
              <tr key={s.id}>
                <td>{s.itemName}</td>
                <td>{s.locationName}</td>
                <td>{s.quantity}</td>
                <td>{s.batchNumber ?? '—'}</td>
                <td>{s.serialNumber ?? '—'}</td>
                <td>{s.expiryDate ? new Date(s.expiryDate).toLocaleDateString('bg-BG') : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
