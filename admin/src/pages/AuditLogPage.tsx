import { useEffect, useState } from 'react';
import { type AuditLogItem, getAuditLogs } from '../api/client';

export function AuditLogPage() {
  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadLogs();
  }, []);

  async function loadLogs() {
    try {
      setLoading(true);
      const data = await getAuditLogs();
      setLogs(data);
      setError(null);
    } catch (err) {
      console.error('Failed to load audit logs:', err);
      setError('Неуспешно зареждане на одит лог');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>Одит лог</h1>
        <button onClick={loadLogs} className="btn-secondary" disabled={loading}>
          {loading ? 'Зареждане...' : 'Опресни'}
        </button>
      </header>

      {error && <div className="error-message">{error}</div>}

      <div className="card">
        <div className="table-container">
          <table>
            <thead>
              <tr>
                <th>Дата</th>
                <th>Потребител</th>
                <th>Устройство</th>
                <th>Действие</th>
                <th>Обект</th>
                <th>Детайли</th>
                <th>IP адрес</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((log) => (
                <tr key={log.id}>
                  <td>{new Date(log.createdAt).toLocaleString()}</td>
                  <td>{log.userName || '-'}</td>
                  <td>{log.deviceName || '-'}</td>
                  <td>
                    <span className="badge">{log.action}</span>
                  </td>
                  <td>
                    {log.entityType && (
                      <span className="text-muted">
                        {log.entityType} ({log.entityId})
                      </span>
                    )}
                  </td>
                  <td className="text-small">{log.details}</td>
                  <td>{log.ipAddress}</td>
                </tr>
              ))}
              {logs.length === 0 && !loading && (
                <tr>
                  <td colSpan={7} className="text-center">Няма намерени записи</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
