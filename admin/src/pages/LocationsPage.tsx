import { useEffect, useState, type FormEvent } from 'react';
import { createLocation, getLocations, updateLocation, deleteLocation, type Location } from '../api/client';

export function LocationsPage() {
  const [locations, setLocations] = useState<Location[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({
    code: '',
    name: '',
    zone: '',
    aisle: '',
    rack: '',
    level: '',
    position: '',
    isActive: true,
  });
  const [error, setError] = useState('');

  const load = () => getLocations().then(setLocations);
  useEffect(() => { load(); }, []);

  const handleEdit = (location: Location) => {
    setEditingId(location.id);
    setForm({
      code: location.code,
      name: location.name,
      zone: location.zone ?? '',
      aisle: location.aisle ?? '',
      rack: location.rack ?? '',
      level: location.level ?? '',
      position: location.position ?? '',
      isActive: location.isActive,
    });
    setShowForm(true);
  };

  const handleDelete = async (id: string) => {
    if (confirm('Сигурни ли сте, че искате да изтриете тази локация?')) {
      try {
        await deleteLocation(id);
        await load();
      } catch {
        setError('Грешка при изтриване на локация');
      }
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      if (editingId) {
        await updateLocation(editingId, form);
      } else {
        await createLocation(form);
      }
      setShowForm(false);
      setEditingId(null);
      setForm({ code: '', name: '', zone: '', aisle: '', rack: '', level: '', position: '', isActive: true });
      await load();
    } catch {
      setError(editingId ? 'Грешка при редактиране на локация' : 'Грешка при създаване на локация');
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Локации</h1>
          <p>Управление на складови локации</p>
        </div>
        <button type="button" onClick={() => {
          setShowForm(!showForm);
          setEditingId(null);
          setForm({ code: '', name: '', zone: '', aisle: '', rack: '', level: '', position: '', isActive: true });
        }}>
          {showForm ? 'Отказ' : '+ Нова локация'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleSubmit}>
          {error && <div className="error">{error}</div>}
          <label>Код<input required value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></label>
          <label>Име<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Зона<input value={form.zone} onChange={(e) => setForm({ ...form, zone: e.target.value })} /></label>
          <label>Ред<input value={form.aisle} onChange={(e) => setForm({ ...form, aisle: e.target.value })} /></label>
          <label>Рафт<input value={form.rack} onChange={(e) => setForm({ ...form, rack: e.target.value })} /></label>
          <label>Ниво<input value={form.level} onChange={(e) => setForm({ ...form, level: e.target.value })} /></label>
          <label>Позиция<input value={form.position} onChange={(e) => setForm({ ...form, position: e.target.value })} /></label>
          <label>Активна<select value={form.isActive ? 'true' : 'false'} onChange={(e) => setForm({ ...form, isActive: e.target.value === 'true' })}>
            <option value='true'>Да</option>
            <option value='false'>Не</option>
          </select></label>
          <button type="submit">{editingId ? 'Запази' : 'Създай'}</button>
        </form>
      )}

      {locations.length === 0 ? (
        <div className="panel empty">Няма създадени локации</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Код</th>
              <th>Име</th>
              <th>Зона</th>
              <th>Ред</th>
              <th>Рафт</th>
              <th>Ниво</th>
              <th>Позиция</th>
              <th>Активна</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            {locations.map((l) => (
              <tr key={l.id}>
                <td><code>{l.code}</code></td>
                <td>{l.name}</td>
                <td>{l.zone ?? '—'}</td>
                <td>{l.aisle ?? '—'}</td>
                <td>{l.rack ?? '—'}</td>
                <td>{l.level ?? '—'}</td>
                <td>{l.position ?? '—'}</td>
                <td><span className={`badge ${l.isActive ? 'online' : 'offline'}`}>{l.isActive ? 'Да' : 'Не'}</span></td>
                <td>
                  <button type="button" onClick={() => handleEdit(l)}>Редактирай</button>
                  <button type="button" onClick={() => handleDelete(l.id)}>Изтрий</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
