import { useEffect, useState } from 'react';
import { getDashboardStats, getOperatorHistory, getSlaOverview, type DashboardStats, type OperatorActionHistoryItem, type SlaOverview } from '../api/client';

export function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [history, setHistory] = useState<OperatorActionHistoryItem[]>([]);
  const [sla, setSla] = useState<SlaOverview | null>(null);

  useEffect(() => {
    getDashboardStats().then(setStats).catch(console.error);
    getOperatorHistory().then(setHistory).catch(console.error);
    getSlaOverview().then(setSla).catch(console.error);
  }, []);

  return (
    <div>
      <header className="page-header">
        <h1>Табло</h1>
        <p>Преглед на системата в реално време</p>
      </header>
      <div className="stats-grid">
        <div className="stat-card">
          <span>Общо устройства</span>
          <strong>{stats?.totalDevices ?? '—'}</strong>
        </div>
        <div className="stat-card">
          <span>Непрочетени известия</span>
          <strong>{stats?.unreadAlertsCount ?? 0}</strong>
        </div>
        <div className="stat-card online">
          <span>Онлайн</span>
          <strong>{stats?.onlineDevices ?? '—'}</strong>
        </div>
        <div className="stat-card offline">
          <span>Офлайн</span>
          <strong>{stats?.offlineDevices ?? '—'}</strong>
        </div>
        <div className="stat-card">
          <span>Активни задачи</span>
          <strong>{stats?.activeTasks ?? '—'}</strong>
        </div>
        <div className="stat-card">
          <span>Спешни задачи</span>
          <strong>{stats?.urgentTasks ?? '—'}</strong>
        </div>
        <div className="stat-card">
          <span>Инвентаризации в процес</span>
          <strong>{stats?.activeInventorySessions ?? '—'}</strong>
        </div>
        <div className="stat-card">
          <span>Активни picking</span>
          <strong>{stats?.activePickings ?? '—'}</strong>
        </div>
      </div>
      <section className="panel">
        <h2>Известия</h2>
        {stats?.recentAlerts && stats.recentAlerts.length > 0 ? (
          <ul>
            {stats.recentAlerts.map((alert) => (
              <li key={alert.id}>
                <strong>{alert.title}</strong> — {alert.message}
                <div style={{ color: '#6b7280', fontSize: '0.9rem' }}>
                  {alert.level} • {new Date(alert.createdAt).toLocaleString('bg-BG')}
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p>Няма известия.</p>
        )}
      </section>
      <section className="panel">
        <h2>Скорошна активност</h2>
        {stats?.recentActivity && stats.recentActivity.length > 0 ? (
          <ul>
            {stats.recentActivity.map((item, index) => (
              <li key={`${item.action}-${index}`}>
                <strong>{item.action}</strong> — {item.entityType ?? 'System'}: {item.details ?? 'Няма допълнителни детайли'}
                <div style={{ color: '#6b7280', fontSize: '0.9rem' }}>
                  {new Date(item.createdAt).toLocaleString('bg-BG')}
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p>Няма регистрирана активност.</p>
        )}
      </section>
      <section className="panel">
        <h2>SLA статус</h2>
        <div className="stats-grid">
          <div className="stat-card">
            <span>Общо задачи</span>
            <strong>{sla?.totalTasks ?? '—'}</strong>
          </div>
          <div className="stat-card offline">
            <span>Просрочени</span>
            <strong>{sla?.overdueTasks ?? 0}</strong>
          </div>
          <div className="stat-card">
            <span>At risk</span>
            <strong>{sla?.atRiskTasks ?? 0}</strong>
          </div>
          <div className="stat-card online">
            <span>On track</span>
            <strong>{sla?.onTrackTasks ?? 0}</strong>
          </div>
        </div>
        {sla?.tasks && sla.tasks.length > 0 ? (
          <ul>
            {sla.tasks.slice(0, 6).map((task) => (
              <li key={task.id}>
                <strong>{task.title}</strong> — {task.slaStatus}
                <div style={{ color: '#6b7280', fontSize: '0.9rem' }}>
                  {task.reference ? `Ref: ${task.reference} • ` : ''}
                  {task.dueDate ? new Date(task.dueDate).toLocaleString('bg-BG') : 'Без срок'}
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p>Няма данни за SLA.</p>
        )}
      </section>
      <section className="panel">
        <h2>История на операторските действия</h2>
        {history.length > 0 ? (
          <ul>
            {history.map((item) => (
              <li key={item.id}>
                <strong>{item.action}</strong> — {item.details ?? 'Няма допълнителни детайли'}
                <div style={{ color: '#6b7280', fontSize: '0.9rem' }}>
                  {item.userName ?? 'Система'} • {new Date(item.createdAt).toLocaleString('bg-BG')}
                </div>
              </li>
            ))}
          </ul>
        ) : (
          <p>Няма история.</p>
        )}
      </section>
      <section className="panel">
        <h2>Фаза 3 — В процес</h2>
        <ul>
          <li>JWT автентикация (потребител + PIN)</li>
          <li>Регистрация и мониторинг на PDA устройства</li>
          <li>Offline sync опашка (API)</li>
          <li>Audit log на операции</li>
          <li>Multi-tenant основа</li>
        </ul>
      </section>
    </div>
  );
}
