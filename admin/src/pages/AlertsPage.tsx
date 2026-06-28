import { useEffect, useState } from 'react';
import { createAlert, getAlerts, markAlertRead } from '../api/client';

export function AlertsPage() {
  const [alerts, setAlerts] = useState<any[]>([]);
  const [title, setTitle] = useState('');
  const [message, setMessage] = useState('');
  const [level, setLevel] = useState('Warning');

  const loadAlerts = async () => {
    const data = await getAlerts();
    setAlerts(data);
  };

  useEffect(() => { void loadAlerts(); }, []);

  const onCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !message.trim()) return;
    await createAlert({ title, message, level });
    setTitle('');
    setMessage('');
    await loadAlerts();
  };

  const onMarkRead = async (id: string) => {
    await markAlertRead(id);
    await loadAlerts();
  };

  return (
    <div>
      <h2>Известия и аларми</h2>
      <form onSubmit={onCreate} style={{ display: 'grid', gap: 8, maxWidth: 480 }}>
        <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Заглавие" />
        <textarea value={message} onChange={(e) => setMessage(e.target.value)} placeholder="Съобщение" rows={4} />
        <select value={level} onChange={(e) => setLevel(e.target.value)}>
          <option value="Info">Info</option>
          <option value="Warning">Warning</option>
          <option value="Critical">Critical</option>
        </select>
        <button type="submit">Създай</button>
      </form>
      <div style={{ marginTop: 16, display: 'grid', gap: 8 }}>
        {alerts.map((a) => (
          <div key={a.id} style={{ border: '1px solid #ccc', padding: 12, borderRadius: 8 }}>
            <strong>{a.title}</strong>
            <div>{a.message}</div>
            <small>{a.level} • {new Date(a.createdAt).toLocaleString()}</small>
            {!a.isRead && <button onClick={() => void onMarkRead(a.id)} style={{ marginLeft: 12 }}>Маркирай като прочетено</button>}
          </div>
        ))}
      </div>
    </div>
  );
}
