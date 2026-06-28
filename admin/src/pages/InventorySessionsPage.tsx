import { useEffect, useState, type FormEvent } from 'react';
import {
  getInventorySessions,
  createInventorySession,
  startInventorySession,
  completeInventorySession,
  getInventoryCounts,
  updateInventoryCount,
  type InventorySession,
  type InventoryCount
} from '../api/client';

export function InventorySessionsPage() {
  const [sessions, setSessions] = useState<InventorySession[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [selectedSessionId, setSelectedSessionId] = useState<string | null>(null);
  const [counts, setCounts] = useState<InventoryCount[]>([]);
  const [form, setForm] = useState({ name: '', description: '' });
  const [error, setError] = useState('');

  const loadSessions = () => getInventorySessions().then(setSessions);
  useEffect(() => { loadSessions(); }, []);

  const loadCounts = async (sessionId: string) => {
    setSelectedSessionId(sessionId);
    const counts = await getInventoryCounts(sessionId);
    setCounts(counts);
  };

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createInventorySession(form);
      setShowForm(false);
      setForm({ name: '', description: '' });
      await loadSessions();
    } catch {
      setError('Грешка при създаване на сесия');
    }
  };

  const handleStart = async (id: string) => {
    setError('');
    try {
      await startInventorySession(id);
      await loadSessions();
      if (selectedSessionId === id) await loadCounts(id);
    } catch {
      setError('Грешка при стартиране на сесия');
    }
  };

  const handleComplete = async (id: string) => {
    setError('');
    try {
      await completeInventorySession(id);
      await loadSessions();
      if (selectedSessionId === id) await loadCounts(id);
    } catch {
      setError('Грешка при завършване на сесия');
    }
  };

  const handleUpdateCount = async (id: string, newValue: number) => {
    setError('');
    try {
      await updateInventoryCount(id, { countedQuantity: newValue });
      if (selectedSessionId) await loadCounts(selectedSessionId);
    } catch {
      setError('Грешка при актуализация на броенето');
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
          <h1>Инвентаризационни сесии</h1>
          <p>Управление на инвентаризации</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Нова сесия'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleCreate}>
          {error && <div className="error">{error}</div>}
          <label>Име<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Описание<input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
          <button type="submit">Създай</button>
        </form>
      )}

      <div className="panel">
        <h2>Списък сесии</h2>
        {sessions.length === 0 ? (
          <div className="empty">Няма създадени сесии</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Име</th>
                <th>Статус</th>
                <th>Стартирано на</th>
                <th>Завършено на</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {sessions.map((session) => (
                <tr key={session.id}>
                  <td><strong>{session.name}</strong></td>
                  <td><span className={getStatusBadgeClass(session.status)}>{session.status}</span></td>
                  <td>{session.startedAt ? new Date(session.startedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>{session.completedAt ? new Date(session.completedAt).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>
                    <button type="button" onClick={() => loadCounts(session.id)}>Виж броения</button>
                    {session.status === 'Draft' && (
                      <button type="button" onClick={() => handleStart(session.id)}>Стартирай</button>
                    )}
                    {session.status === 'InProgress' && (
                      <button type="button" onClick={() => handleComplete(session.id)}>Завърши</button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedSessionId && (
        <div className="panel" style={{ marginTop: '2rem' }}>
          <h2>Броения</h2>
          {counts.length === 0 ? (
            <div className="empty">Няма броения за тази сесия</div>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Артикул</th>
                  <th>Локация</th>
                  <th>Системно количество</th>
                  <th>Броено количество</th>
                  <th>Партида</th>
                  <th>Сериен номер</th>
                  <th>Действия</th>
                </tr>
              </thead>
              <tbody>
                {counts.map((count) => {
                  const isEditable = sessions.find(s => s.id === selectedSessionId)?.status === 'InProgress';
                  const diff = count.countedQuantity !== undefined ? count.countedQuantity - count.systemQuantity : null;
                  return (
                    <tr key={count.id}>
                      <td>{count.itemName}</td>
                      <td>{count.locationName}</td>
                      <td>{count.systemQuantity}</td>
                      <td>
                        {isEditable ? (
                          <input
                            type="number"
                            step="0.01"
                            value={count.countedQuantity ?? ''}
                            onChange={(e) => {
                              const val = e.target.value;
                              if (val !== '') handleUpdateCount(count.id, parseFloat(val));
                            }}
                          />
                        ) : (
                          <span style={{ color: diff !== null && diff !== 0 ? '#dc2626' : 'inherit' }}>
                            {count.countedQuantity ?? '—'}
                          </span>
                        )}
                      </td>
                      <td>{count.batchNumber ?? '—'}</td>
                      <td>{count.serialNumber ?? '—'}</td>
                      <td>{count.countedAt ? new Date(count.countedAt).toLocaleString('bg-BG') : '—'}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}