import { useEffect, useState } from 'react';
import { getDevices, type Device } from '../api/client';

const statusLabel: Record<Device['status'], string> = {
  Online: 'Онлайн',
  Offline: 'Офлайн',
  Maintenance: 'Поддръжка',
};

export function DevicesPage() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getDevices()
      .then(setDevices)
      .finally(() => setLoading(false));
  }, []);

  return (
    <div>
      <header className="page-header">
        <h1>Устройства</h1>
        <p>Регистрирани PDA терминали</p>
      </header>
      {loading ? (
        <p>Зареждане...</p>
      ) : devices.length === 0 ? (
        <div className="panel empty">Няма регистрирани устройства. Стартирайте PDA приложението.</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Име</th>
              <th>Hardware ID</th>
              <th>Производител</th>
              <th>Модел</th>
              <th>Статус</th>
              <th>Батерия</th>
              <th>Оператор</th>
              <th>Последна активност</th>
            </tr>
          </thead>
          <tbody>
            {devices.map((d) => (
              <tr key={d.id}>
                <td>{d.name}</td>
                <td><code>{d.hardwareId}</code></td>
                <td>{d.manufacturer ?? '—'}</td>
                <td>{d.model ?? '—'}</td>
                <td><span className={`badge ${d.status.toLowerCase()}`}>{statusLabel[d.status]}</span></td>
                <td>{d.batteryLevel != null ? `${d.batteryLevel}%` : '—'}</td>
                <td>{d.assignedUserName ?? '—'}</td>
                <td>{d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleString('bg-BG') : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
