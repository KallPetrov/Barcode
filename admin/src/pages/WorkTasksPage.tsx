import { useEffect, useMemo, useState, type FormEvent } from 'react';
import {
  completeReminder,
  createReminder,
  createWorkTask,
  getReminders,
  getUsers,
  getWorkTasks,
  getSlaOverview,
  updateWorkTask,
  type ReminderItem,
  type User,
  type WorkTask,
  type SlaOverview,
} from '../api/client';

const priorityLabels: Record<number, string> = {
  0: 'Нисък',
  1: 'Среден',
  2: 'Висок',
  3: 'Спешен',
};

const statusLabels: Record<number, string> = {
  0: 'Отворена',
  1: 'В процес',
  2: 'Завършена',
  3: 'Отказана',
};

export function WorkTasksPage() {
  const [tasks, setTasks] = useState<WorkTask[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [reminders, setReminders] = useState<ReminderItem[]>([]);
  const [sla, setSla] = useState<SlaOverview | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState('');
  const [form, setForm] = useState({
    title: '',
    description: '',
    taskType: 'Picking',
    priority: 1,
    assignedUserId: '',
    reference: '',
    dueDate: '',
  });

  const loadData = async () => {
    const [tasksData, usersData, remindersData, slaData] = await Promise.all([getWorkTasks(), getUsers(), getReminders(), getSlaOverview()]);
    setTasks(tasksData);
    setUsers(usersData);
    setReminders(remindersData);
    setSla(slaData);
  };

  useEffect(() => {
    void loadData();
  }, []);

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createWorkTask({
        title: form.title,
        description: form.description || undefined,
        taskType: form.taskType,
        priority: form.priority,
        assignedUserId: form.assignedUserId || undefined,
        reference: form.reference || undefined,
        dueDate: form.dueDate || undefined,
      });
      setShowForm(false);
      setForm({ title: '', description: '', taskType: 'Picking', priority: 1, assignedUserId: '', reference: '', dueDate: '' });
      await loadData();
    } catch {
      setError('Грешка при създаване на задача');
    }
  };

  const handleStatusChange = async (id: string, status: number) => {
    setError('');
    try {
      await updateWorkTask(id, { status });
      await loadData();
    } catch {
      setError('Грешка при актуализация на задачата');
    }
  };

  const urgentTasks = useMemo(() => tasks.filter((task) => task.priority >= 2 && task.status < 2).length, [tasks]);

  const handleReminderCreate = async (task: WorkTask) => {
    await createReminder({
      title: `Напомняне: ${task.title}`,
      message: `Проверете задача ${task.title}`,
      userId: task.assignedUserId,
      relatedEntityId: task.id,
      relatedEntityType: 'WorkTask',
      dueAt: task.dueDate ?? new Date().toISOString(),
    });
    await loadData();
  };

  const handleReminderComplete = async (id: string) => {
    await completeReminder(id);
    await loadData();
  };

  const slaByTask = useMemo(() => new Map((sla?.tasks ?? []).map((task) => [task.id, task])), [sla]);

  const getTaskSlaStatus = (task: WorkTask) => slaByTask.get(task.id)?.slaStatus ?? 'Healthy';

  const getTaskSlaSeverity = (task: WorkTask) => {
    switch (getTaskSlaStatus(task)) {
      case 'Critical':
        return 0;
      case 'Warning':
        return 1;
      case 'Info':
        return 2;
      default:
        return 3;
    }
  };

  const sortedTasks = useMemo(() => {
    return [...tasks].sort((a, b) => {
      const severityDelta = getTaskSlaSeverity(a) - getTaskSlaSeverity(b);
      if (severityDelta !== 0) return severityDelta;

      if (b.priority !== a.priority) return b.priority - a.priority;
      if (a.status !== b.status) return a.status - b.status;
      return a.title.localeCompare(b.title, 'bg');
    });
  }, [tasks, slaByTask]);

  const getTaskSlaStyle = (task: WorkTask) => {
    const status = getTaskSlaStatus(task);
    switch (status) {
      case 'Critical':
        return { backgroundColor: '#fee2e2', borderLeft: '4px solid #dc2626' };
      case 'Warning':
        return { backgroundColor: '#fef3c7', borderLeft: '4px solid #d97706' };
      case 'Info':
        return { backgroundColor: '#dbeafe', borderLeft: '4px solid #2563eb' };
      default:
        return {};
    }
  };

  const renderSlaBadge = (task: WorkTask) => {
    const status = getTaskSlaStatus(task);
    if (status === 'Healthy' || status === 'Completed') return null;

    const config: Record<string, { label: string; color: string }> = {
      Critical: { label: 'CRITICAL', color: '#dc2626' },
      Warning: { label: 'WARNING', color: '#d97706' },
      Info: { label: 'INFO', color: '#2563eb' },
    };

    const current = config[status] ?? config.Warning;
    return (
      <span
        style={{
          display: 'inline-block',
          marginTop: 4,
          padding: '2px 8px',
          borderRadius: 999,
          fontSize: '0.75rem',
          fontWeight: 700,
          color: 'white',
          backgroundColor: current.color,
        }}
      >
        {current.label}
      </span>
    );
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Задачи към операторите</h1>
          <p>Разпределяйте задачи, приоритети и следете изпълнението</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>{showForm ? 'Отказ' : '+ Нова задача'}</button>
      </header>

      <div className="panel" style={{ marginBottom: '1.5rem' }}>
        <strong>Наблюдение:</strong> {urgentTasks} активни задачи с висок и спешен приоритет.
      </div>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleCreate}>
          {error && <div className="error">{error}</div>}
          <label>
            Заглавие
            <input required value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
          </label>
          <label>
            Описание
            <input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </label>
          <label>
            Тип
            <input value={form.taskType} onChange={(e) => setForm({ ...form, taskType: e.target.value })} />
          </label>
          <label>
            Приоритет
            <select value={form.priority} onChange={(e) => setForm({ ...form, priority: Number(e.target.value) })}>
              <option value={0}>Нисък</option>
              <option value={1}>Среден</option>
              <option value={2}>Висок</option>
              <option value={3}>Спешен</option>
            </select>
          </label>
          <label>
            Потребител
            <select value={form.assignedUserId} onChange={(e) => setForm({ ...form, assignedUserId: e.target.value })}>
              <option value="">— Не е избран —</option>
              {users.map((user) => (
                <option key={user.id} value={user.id}>{user.fullName}</option>
              ))}
            </select>
          </label>
          <label>
            Референция
            <input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} />
          </label>
          <label>
            Краен срок
            <input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} />
          </label>
          <button type="submit">Създай</button>
        </form>
      )}

      <div className="panel" style={{ marginBottom: '1.5rem' }}>
        <h2>Напомняния</h2>
        {reminders.length === 0 ? (
          <div className="empty">Няма напомняния</div>
        ) : (
          <ul>
            {reminders.map((reminder) => (
              <li key={reminder.id}>
                <strong>{reminder.title}</strong>
                {reminder.message && <> — {reminder.message}</>}
                <div style={{ color: '#6b7280', fontSize: '0.9rem' }}>
                  {reminder.userName ?? '—'} • {new Date(reminder.dueAt).toLocaleString('bg-BG')}
                </div>
                {!reminder.isCompleted && (
                  <button type="button" onClick={() => void handleReminderComplete(reminder.id)} style={{ marginTop: 6 }}>
                    Завърши
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="panel">
        <h2>Списък задачи</h2>
        {tasks.length === 0 ? (
          <div className="empty">Няма задачи</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Заглавие</th>
                <th>Тип</th>
                <th>Приоритет</th>
                <th>Статус</th>
                <th>SLA</th>
                <th>Потребител</th>
                <th>Краен срок</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {sortedTasks.map((task) => (
                <tr key={task.id} style={getTaskSlaStyle(task)}>
                  <td>
                    <strong>{task.title}</strong>
                    {task.reference && <div style={{ color: '#6b7280' }}>Ref: {task.reference}</div>}
                    {renderSlaBadge(task)}
                  </td>
                  <td>{task.taskType}</td>
                  <td>{priorityLabels[task.priority] ?? task.priority}</td>
                  <td>{statusLabels[task.status] ?? task.status}</td>
                  <td>{renderSlaBadge(task)}</td>
                  <td>{task.assignedUserName ?? '—'}</td>
                  <td>{task.dueDate ? new Date(task.dueDate).toLocaleDateString('bg-BG') : '—'}</td>
                  <td>
                    {task.status !== 2 && (
                      <>
                        <button type="button" onClick={() => handleStatusChange(task.id, 2)}>
                          Завърши
                        </button>
                        <button type="button" onClick={() => void handleReminderCreate(task)} style={{ marginLeft: 6 }}>
                          Напомни
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
