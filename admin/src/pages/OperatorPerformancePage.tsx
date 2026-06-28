import { useEffect, useState, type FormEvent } from 'react';
import {
  generateOperatorPerformance,
  getOperatorPerformance,
  getUsers,
  type OperatorPerformance,
  type User,
} from '../api/client';

export function OperatorPerformancePage() {
  const [reports, setReports] = useState<OperatorPerformance[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [form, setForm] = useState({ period: 'week-1', userId: '' });
  const [error, setError] = useState('');

  const loadData = async () => {
    const [reportsData, usersData] = await Promise.all([getOperatorPerformance(), getUsers()]);
    setReports(reportsData);
    setUsers(usersData);
  };

  useEffect(() => {
    void loadData();
  }, []);

  const handleGenerate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await generateOperatorPerformance(form);
      await loadData();
    } catch {
      setError('Грешка при генериране на отчета');
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Отчети за операторите</h1>
          <p>Следене на товарене, изпълнение и ефективност</p>
        </div>
      </header>

      <form className="panel form-grid" onSubmit={handleGenerate}>
        {error && <div className="error">{error}</div>}
        <label>
          Период
          <input value={form.period} onChange={(e) => setForm({ ...form, period: e.target.value })} />
        </label>
        <label>
          Оператор
          <select value={form.userId} onChange={(e) => setForm({ ...form, userId: e.target.value })} required>
            <option value="">— Избери оператор —</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>{user.fullName}</option>
            ))}
          </select>
        </label>
        <button type="submit">Генерирай отчет</button>
      </form>

      <div className="panel">
        <h2>История на отчети</h2>
        {reports.length === 0 ? (
          <div className="empty">Няма генерирани отчети</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Оператор</th>
                <th>Период</th>
                <th>Задачи</th>
                <th>Завършени</th>
                <th>Просрочени</th>
                <th>Picking</th>
                <th>Инвентаризации</th>
                <th>Ефективност</th>
              </tr>
            </thead>
            <tbody>
              {reports.map((report) => (
                <tr key={report.id}>
                  <td>{report.userName}</td>
                  <td>{report.period}</td>
                  <td>{report.tasksAssigned}</td>
                  <td>{report.tasksCompleted}</td>
                  <td>{report.tasksOverdue}</td>
                  <td>{report.pickingCompleted}</td>
                  <td>{report.inventorySessionsCompleted}</td>
                  <td>{report.efficiencyRate}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
