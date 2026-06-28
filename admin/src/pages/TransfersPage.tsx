import { useEffect, useState, type FormEvent } from 'react';
import {
  getTransfers,
  getTransfer,
  createTransfer,
  startTransfer,
  completeTransfer,
  updateTransferLine,
  getItems,
  getLocations,
  type TransferOrder
} from '../api/client';

export function TransfersPage() {
  const [transfers, setTransfers] = useState<TransferOrder[]>([]);
  const [items, setItems] = useState<any[]>([]);
  const [locations, setLocations] = useState<any[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [selectedTransferId, setSelectedTransferId] = useState<string | null>(null);
  const [selectedTransfer, setSelectedTransfer] = useState<TransferOrder | null>(null);
  const [form, setForm] = useState({
    name: '',
    reference: '',
    notes: '',
    lines: [] as Array<{ itemId: string; sourceLocationId: string; targetLocationId: string; quantity: number; notes?: string }>
  });
  const [error, setError] = useState('');

  const loadTransfers = () => getTransfers().then(setTransfers);
  useEffect(() => { loadTransfers(); }, []);

  const loadItemsAndLocations = async () => {
    const [itemsData, locationsData] = await Promise.all([getItems(), getLocations()]);
    setItems(itemsData);
    setLocations(locationsData);
  };
  useEffect(() => { loadItemsAndLocations(); }, []);

  const handleAddLine = () => {
    setForm({
      ...form,
      lines: [...form.lines, { itemId: '', sourceLocationId: '', targetLocationId: '', quantity: 0, notes: '' }]
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
      await createTransfer(form);
      setShowForm(false);
      setForm({ name: '', reference: '', notes: '', lines: [] });
      await loadTransfers();
    } catch {
      setError('Грешка при създаване на документ за трансфер');
    }
  };

  const handleStart = async (id: string) => {
    setError('');
    try {
      await startTransfer(id);
      await loadTransfers();
      if (selectedTransferId === id) await loadTransferDetails(id);
    } catch {
      setError('Грешка при стартиране на документа');
    }
  };

  const handleComplete = async (id: string) => {
    setError('');
    try {
      await completeTransfer(id);
      await loadTransfers();
      if (selectedTransferId === id) await loadTransferDetails(id);
    } catch {
      setError('Грешка при завършване на документа');
    }
  };

  const loadTransferDetails = async (id: string) => {
    setSelectedTransferId(id);
    const transfer = await getTransfer(id);
    setSelectedTransfer(transfer);
  };

  const handleUpdateLine = async (lineId: string, newValue: number) => {
    setError('');
    try {
      await updateTransferLine(lineId, { movedQuantity: newValue });
      if (selectedTransferId) await loadTransferDetails(selectedTransferId);
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
          <h1>Вътрешни трансфери</h1>
          <p>Управление на документи за трансфер</p>
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
                <label>От локация
                  <select required value={line.sourceLocationId} onChange={(e) => handleLineChange(index, 'sourceLocationId', e.target.value)}>
                    <option value="">Изберете локация</option>
                    {locations.map(loc => (
                      <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
                    ))}
                  </select>
                </label>
                <label>Към локация
                  <select required value={line.targetLocationId} onChange={(e) => handleLineChange(index, 'targetLocationId', e.target.value)}>
                    <option value="">Изберете локация</option>
                    {locations.map(loc => (
                      <option key={loc.id} value={loc.id}>{loc.code} - {loc.name}</option>
                    ))}
                  </select>
                </label>
                <label>Количество
                  <input required type="number" step="0.01" value={line.quantity} onChange={(e) => handleLineChange(index, 'quantity', parseFloat(e.target.value) || 0)} />
                </label>
                <label>Бележки<input value={line.notes} onChange={(e) => handleLineChange(index, 'notes', e.target.value)} /></label>
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
        {transfers.length === 0 ? (
          <div className="empty">Няма създадени документи</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Име</th>
                <th>Референция</th>
                <th>Статус</th>
                <th>Преместен на</th>
                <th>Завършен на</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {transfers.map((transfer) => (
                <tr key={transfer.id}>
                  <td><strong>{transfer.name}</strong></td>
                  <td>{transfer.reference || '—'}</td>
                  <td><span className={getStatusBadgeClass(transfer.status)}>{transfer.status}</span></td>
                  <td>{transfer.movedAt ? new Date(transfer.movedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>{transfer.completedAt ? new Date(transfer.completedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>
                    <button type="button" onClick={() => loadTransferDetails(transfer.id)}>Виж детайли</button>
                    {transfer.status === 'Draft' && (
                      <button type="button" onClick={() => handleStart(transfer.id)}>Стартирай</button>
                    )}
                    {transfer.status === 'InProgress' && (
                      <button type="button" onClick={() => handleComplete(transfer.id)}>Завърши</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedTransfer && (
        <div className="panel" style={{ marginTop: '2rem' }}>
          <h2>Детайли на документа</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>Артикул</th>
                <th>От локация</th>
                <th>Към локация</th>
                <th>Количество</th>
                <th>Преместено количество</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {selectedTransfer.lines.map((line) => {
                const isEditable = selectedTransfer.status === 'InProgress';
                const diff = line.movedQuantity !== undefined ? line.movedQuantity - line.quantity : null;
                return (
                  <tr key={line.id}>
                    <td>{line.itemName}</td>
                    <td>{line.sourceLocationName}</td>
                    <td>{line.targetLocationName}</td>
                    <td>{line.quantity}</td>
                    <td>
                      {isEditable ? (
                        <input
                          type="number"
                          step="0.01"
                          value={line.movedQuantity ?? ''}
                          onChange={(e) => {
                            const val = e.target.value;
                            if (val !== '') handleUpdateLine(line.id, parseFloat(val));
                          }}
                        />
                      ) : (
                        <span style={{ color: diff !== null && diff !== 0 ? '#dc2626' : 'inherit' }}>
                          {line.movedQuantity ?? '—'}
                        </span>
                      )}
                    </td>
                    <td>{line.movedAt ? new Date(line.movedAt).toLocaleString('bg-BG') : '—'}</td>
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
