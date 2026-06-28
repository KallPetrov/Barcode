import { useEffect, useState, type FormEvent } from 'react';
import {
  getGoodsReceipts,
  getGoodsReceipt,
  createGoodsReceipt,
  startGoodsReceipt,
  completeGoodsReceipt,
  updateGoodsReceiptLine,
  getItems,
  getLocations,
  type GoodsReceipt
} from '../api/client';

export function GoodsReceiptsPage() {
  const [receipts, setReceipts] = useState<GoodsReceipt[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [selectedReceiptId, setSelectedReceiptId] = useState<string | null>(null);
  const [selectedReceipt, setSelectedReceipt] = useState<GoodsReceipt | null>(null);
  const [form, setForm] = useState({
    name: '',
    reference: '',
    supplierName: '',
    notes: '',
    lines: [] as Array<{ itemId: string; locationId: string; expectedQuantity: number; batchNumber?: string; serialNumber?: string; expiryDate?: string; notes?: string }>
  });
  const [error, setError] = useState('');

  const loadReceipts = () => getGoodsReceipts().then(setReceipts);
  useEffect(() => { loadReceipts(); }, []);

  const loadItemsAndLocations = async () => {
    const [itemsData, locationsData] = await Promise.all([getItems(), getLocations()]);
    setItems(itemsData);
    setLocations(locationsData);
  };
  useEffect(() => { loadItemsAndLocations(); }, []);

  const handleAddLine = () => {
    setForm({
      ...form,
      lines: [...form.lines, { itemId: '', locationId: '', expectedQuantity: 0, batchNumber: '', serialNumber: '', expiryDate: '', notes: '' }]
    });
  };

  const handleRemoveLine = (index: number) => {
    setForm({
      ...form,
      lines: form.lines.filter((_, i) => i !== index)
    });
  };

  const handleLineChange = (index: number, field: string, value: any) => {
    const newLines = [...form.lines];
    newLines[index] = { ...newLines[index], [field]: value };
    setForm({ ...form, lines: newLines });
  };

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createGoodsReceipt(form);
      setShowForm(false);
      setForm({ name: '', reference: '', supplierName: '', notes: '', lines: [] });
      await loadReceipts();
    } catch {
      setError('Грешка при създаване на документ за приемане');
    }
  };

  const handleStart = async (id: string) => {
    setError('');
    try {
      await startGoodsReceipt(id);
      await loadReceipts();
      if (selectedReceiptId === id) await loadReceiptDetails(id);
    } catch {
      setError('Грешка при стартиране на документа');
    }
  };

  const handleComplete = async (id: string) => {
    setError('');
    try {
      await completeGoodsReceipt(id);
      await loadReceipts();
      if (selectedReceiptId === id) await loadReceiptDetails(id);
    } catch {
      setError('Грешка при завършване на документа');
    }
  };

  const loadReceiptDetails = async (id: string) => {
    setSelectedReceiptId(id);
    const receipt = await getGoodsReceipt(id);
    setSelectedReceipt(receipt);
  };

  const handleUpdateLine = async (lineId: string, newValue: number) => {
    setError('');
    try {
      await updateGoodsReceiptLine(lineId, { receivedQuantity: newValue });
      if (selectedReceiptId) await loadReceiptDetails(selectedReceiptId);
    } catch {
      setError('Грешка при актуализация на реда');
    }
  };

  const getStatusBadgeClass = (status: string) => {
    switch (status) {
      case 'Draft': return 'badge';
      case 'InProgress': return 'badge online';
      case 'Completed': return 'badge online';
      default: return 'badge offline';
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Приемане на стоки</h1>
          <p>Управление на документи за приемане</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Нов документ'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleCreate}>
          {error && <div className="error">{error}</div>}
          <label>Име<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Референция<input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></label>
          <label>Доставчик<input value={form.supplierName} onChange={(e) => setForm({ ...form, supplierName: e.target.value })} /></label>
          <label>Бележки<input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></label>
          
          <div style={{ gridColumn: '1 / -1', marginTop: '1rem' }}>
            <h3>Редове</h3>
            {form.lines.map((line, index) => (
              <div key={index} className="form-grid" style={{ marginBottom: '1rem', padding: '1rem', border: '1px solid #ddd' }}>
                <label>Артикул
                  <select required value={line.itemId} onChange={(e) => handleLineChange(index, 'itemId', e.target.value)}>
                    <option value="">Изберете артикул</option>
                    {items.map(item => (
                      <option key={item.id} value={item.id}>{item.sku} - {item.name}</option>
                    ))}
                  </select>
                </label>
                <label>Локация
                  <select required value={line.locationId} onChange={(e) => handleLineChange(index, 'locationId', e.target.value)}>
                    <option value="">Изберете локация</option>
                    {locations.map(loc => (
                      <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
                    ))}
                  </select>
                </label>
                <label>Очаквано количество
                  <input required type="number" step="0.01" value={line.expectedQuantity} onChange={(e) => handleLineChange(index, 'expectedQuantity', parseFloat(e.target.value) || 0)} />
                </label>
                <label>Партида<input value={line.batchNumber} onChange={(e) => handleLineChange(index, 'batchNumber', e.target.value)} /></label>
                <label>Сериен номер<input value={line.serialNumber} onChange={(e) => handleLineChange(index, 'serialNumber', e.target.value)} /></label>
                <label>Срок на годност<input type="date" value={line.expiryDate} onChange={(e) => handleLineChange(index, 'expiryDate', e.target.value)} /></label>
                <button type="button" onClick={() => handleRemoveLine(index)}>Премахни ред</button>
              </div>
            ))}
            <button type="button" onClick={handleAddLine}>+ Добави ред</button>
          </div>
          
          <button type="submit" style={{ gridColumn: '1 / -1' }}>Създай</button>
        </form>
      )}

      <div className="panel">
        <h2>Списък документи</h2>
        {receipts.length === 0 ? (
          <div className="empty">Няма създадени документи</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Име</th>
                <th>Референция</th>
                <th>Доставчик</th>
                <th>Статус</th>
                <th>Приет на</th>
                <th>Завършен на</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {receipts.map((receipt) => (
                <tr key={receipt.id}>
                  <td><strong>{receipt.name}</strong></td>
                  <td>{receipt.reference || '—'}</td>
                  <td>{receipt.supplierName || '—'}</td>
                  <td><span className={getStatusBadgeClass(receipt.status)}>{receipt.status}</span></td>
                  <td>{receipt.receivedAt ? new Date(receipt.receivedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>{receipt.completedAt ? new Date(receipt.completedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>
                    <button type="button" onClick={() => loadReceiptDetails(receipt.id)}>Виж детайли</button>
                    {receipt.status === 'Draft' && (
                      <button type="button" onClick={() => handleStart(receipt.id)}>Стартирай</button>
                    )}
                    {receipt.status === 'InProgress' && (
                      <button type="button" onClick={() => handleComplete(receipt.id)}>Завърши</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedReceipt && (
        <div className="panel" style={{ marginTop: '2rem' }}>
          <h2>Детайли на документа</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>Артикул</th>
                <th>Локация</th>
                <th>Очаквано количество</th>
                <th>Прието количество</th>
                <th>Партида</th>
                <th>Сериен номер</th>
                <th>Срок на годност</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {selectedReceipt.lines.map((line) => {
                const isEditable = selectedReceipt.status === 'InProgress';
                const diff = line.receivedQuantity !== undefined ? line.receivedQuantity - line.expectedQuantity : null;
                return (
                  <tr key={line.id}>
                    <td>{line.itemName}</td>
                    <td>{line.locationName}</td>
                    <td>{line.expectedQuantity}</td>
                    <td>
                      {isEditable ? (
                        <input
                          type="number"
                          step="0.01"
                          value={line.receivedQuantity ?? ''}
                          onChange={(e) => {
                            const val = e.target.value;
                            if (val !== '') handleUpdateLine(line.id, parseFloat(val));
                          }}
                        />
                      ) : (
                        <span style={{ color: diff !== null && diff !== 0 ? '#dc2626' : 'inherit' }}>
                          {line.receivedQuantity ?? '—'}
                        </span>
                      )}
                    </td>
                    <td>{line.batchNumber ?? '—'}</td>
                    <td>{line.serialNumber ?? '—'}</td>
                    <td>{line.expiryDate ? new Date(line.expiryDate).toLocaleDateString('bg-BG') : '—'}</td>
                    <td>{line.receivedAt ? new Date(line.receivedAt).toLocaleString('bg-BG') : '—'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
