import { useEffect, useState, type FormEvent } from 'react';
import { createUser, getUsers, type User } from '../api/client';

export function UsersPage() {
  const [users, setUsers] = useState<User[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ username: '', password: '', fullName: '', role: 'Operator', pin: '' });
  const [error, setError] = useState('');

  const load = () => getUsers().then(setUsers);
  useEffect(() => { load(); }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createUser({ ...form, pin: form.pin || undefined });
      setShowForm(false);
      setForm({ username: '', password: '', fullName: '', role: 'Operator', pin: '' });
      await load();
    } catch {
      setError('Грешка при създаване на потребител');
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Потребители</h1>
          <p>Управление на акаунти и роли</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Нов потребител'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleSubmit}>
          {error && <div className="error">{error}</div>}
          <label>Потребител<input required value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} /></label>
          <label>Парола<input required type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></label>
          <label>Име<input required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} /></label>
          <label>Роля
            <select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
              <option value="Operator">Operator</option>
              <option value="Supervisor">Supervisor</option>
              <option value="Admin">Admin</option>
            </select>
          </label>
          <label>PIN (опционално)<input value={form.pin} onChange={(e) => setForm({ ...form, pin: e.target.value })} maxLength={6} /></label>
          <button type="submit">Създай</button>
        </form>
      )}

      <table className="data-table">
        <thead>
          <tr><th>Потребител</th><th>Име</th><th>Роля</th></tr>
        </thead>
        <tbody>
          {users.map((u) => (
            <tr key={u.id}>
              <td>{u.username}</td>
              <td>{u.fullName}</td>
              <td>{u.role}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
