import { useEffect, useState, type FormEvent } from 'react';
import {
  getErpConfigurations,
  createErpConfiguration,
  updateErpConfiguration,
  deleteErpConfiguration,
  testErpConnection,
  syncErpItems,
  syncErpInventory,
  type ErpConfiguration
} from '../api/client';

export function ErpConfigurationsPage() {
  const [configs, setConfigs] = useState<ErpConfiguration[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingConfig, setEditingConfig] = useState<ErpConfiguration | null>(null);
  const [form, setForm] = useState({
    name: '',
    providerType: 'Odoo',
    apiUrl: '',
    apiKey: '',
    username: '',
    password: '',
    databaseName: '',
    autoSyncItems: false,
    autoSyncInventory: false,
    isActive: true,
    settingsJson: ''
  });
  const [error, setError] = useState('');

  const loadConfigs = () => getErpConfigurations().then(setConfigs);
  useEffect(() => { loadConfigs(); }, []);

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await createErpConfiguration(form);
      setShowForm(false);
      setForm({ name: '', providerType: 'Odoo', apiUrl: '', apiKey: '', username: '', password: '', databaseName: '', autoSyncItems: false, autoSyncInventory: false, isActive: true, settingsJson: '' });
      await loadConfigs();
    } catch {
      setError('Грешка при създаване на ERP конфигурация');
    }
  };

  const handleUpdate = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    if (!editingConfig) return;
    try {
      await updateErpConfiguration(editingConfig.id, form);
      setShowForm(false);
      setEditingConfig(null);
      setForm({ name: '', providerType: 'Odoo', apiUrl: '', apiKey: '', username: '', password: '', databaseName: '', autoSyncItems: false, autoSyncInventory: false, isActive: true, settingsJson: '' });
      await loadConfigs();
    } catch {
      setError('Грешка при актуализация на ERP конфигурация');
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Сигурни ли сте, че искате да изтриете тази конфигурация?')) return;
    setError('');
    try {
      await deleteErpConfiguration(id);
      await loadConfigs();
    } catch {
      setError('Грешка при изтриване на ERP конфигурация');
    }
  };

  const handleTestConnection = async (id: string) => {
    setError('');
    try {
      const success = await testErpConnection(id);
      if (success) {
        alert('Връзката е успешна!');
      } else {
        alert('Връзката не е успешна. Проверете настройките.');
      }
    } catch {
      setError('Грешка при тестване на връзката');
    }
  };

  const handleSyncItems = async (id: string) => {
    if (!confirm('Сигурни ли сте, че искате да синхронизирате артикулите?')) return;
    setError('');
    try {
      await syncErpItems(id);
      alert('Синхронизацията на артикулите е стартирана');
      await loadConfigs();
    } catch {
      setError('Грешка при синхронизация на артикулите');
    }
  };

  const handleSyncInventory = async (id: string) => {
    if (!confirm('Сигурни ли сте, че искате да синхронизирате наличностите?')) return;
    setError('');
    try {
      await syncErpInventory(id);
      alert('Синхронизацията на наличностите е стартирана');
      await loadConfigs();
    } catch {
      setError('Грешка при синхронизация на наличностите');
    }
  };

  const handleEdit = (config: ErpConfiguration) => {
    setEditingConfig(config);
    setForm({
      name: config.name,
      providerType: config.providerType,
      apiUrl: config.apiUrl || '',
      apiKey: '',
      username: '',
      password: '',
      databaseName: config.databaseName || '',
      autoSyncItems: config.autoSyncItems,
      autoSyncInventory: config.autoSyncInventory,
      isActive: config.isActive,
      settingsJson: ''
    });
    setShowForm(true);
  };

  const handleCancelEdit = () => {
    setEditingConfig(null);
    setShowForm(false);
    setForm({ name: '', providerType: 'Odoo', apiUrl: '', apiKey: '', username: '', password: '', databaseName: '', autoSyncItems: false, autoSyncInventory: false, isActive: true, settingsJson: '' });
  };

  const getStatusBadgeClass = (isActive: boolean) => isActive ? 'badge online' : 'badge offline';

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>ERP Конфигурации</h1>
          <p>Управление на връзки с ERP системи (Odoo, Dynamics 365)</p>
        </div>
        <button type="button" onClick={() => setShowForm(!showForm)}>
          {showForm ? 'Отказ' : '+ Нова конфигурация'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={editingConfig ? handleUpdate : handleCreate}>
          {error && <div className="error">{error}</div>}
          <label>Име<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>ERP система
            <select required value={form.providerType} onChange={(e) => setForm({ ...form, providerType: e.target.value })}>
              <option value="Odoo">Odoo</option>
              <option value="Dynamics365">Dynamics 365</option>
              <option value="Sap">SAP</option>
              <option value="Custom">Custom</option>
            </select>
          </label>
          <label>API URL<input value={form.apiUrl} onChange={(e) => setForm({ ...form, apiUrl: e.target.value })} placeholder="https://erp.example.com" /></label>
          <label>Име на база данни<input value={form.databaseName} onChange={(e) => setForm({ ...form, databaseName: e.target.value })} /></label>
          <label>API Key<input value={form.apiKey} onChange={(e) => setForm({ ...form, apiKey: e.target.value })} type="password" /></label>
          <label>Потребителско име<input value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} /></label>
          <label>Парола<input value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} type="password" /></label>
          <label>
            <input type="checkbox" checked={form.autoSyncItems} onChange={(e) => setForm({ ...form, autoSyncItems: e.target.checked })} />
            Автоматична синхронизация на артикули
          </label>
          <label>
            <input type="checkbox" checked={form.autoSyncInventory} onChange={(e) => setForm({ ...form, autoSyncInventory: e.target.checked })} />
            Автоматична синхронизация на наличности
          </label>
          <label>
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
            Активна
          </label>
          <button type="submit" style={{ gridColumn: '1 / -1' }}>{editingConfig ? 'Актуализирай' : 'Създай'}</button>
          {editingConfig && <button type="button" onClick={handleCancelEdit} style={{ gridColumn: '1 / -1' }}>Отказ</button>}
        </form>
      )}

      <div className="panel">
        <h2>Списък конфигурации</h2>
        {configs.length === 0 ? (
          <div className="empty">Няма създадени ERP конфигурации</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Име</th>
                <th>ERP система</th>
                <th>API URL</th>
                <th>База данни</th>
                <th>Статус</th>
                <th>Последна синхронизация</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {configs.map((config) => (
                <tr key={config.id}>
                  <td><strong>{config.name}</strong></td>
                  <td>{config.providerType}</td>
                  <td>{config.apiUrl || '—'}</td>
                  <td>{config.databaseName || '—'}</td>
                  <td><span className={getStatusBadgeClass(config.isActive)}>{config.isActive ? 'Активна' : 'Неактивна'}</span></td>
                  <td>{config.lastSyncAt ? new Date(config.lastSyncAt).toLocaleString('bg-BG') : '—'}</td>
                  <td>
                    <button type="button" onClick={() => handleTestConnection(config.id)}>Тествай връзка</button>
                    <button type="button" onClick={() => handleSyncItems(config.id)}>Синхр. артикули</button>
                    <button type="button" onClick={() => handleSyncInventory(config.id)}>Синхр. наличности</button>
                    <button type="button" onClick={() => handleEdit(config)}>Редактирай</button>
                    <button type="button" onClick={() => handleDelete(config.id)}>Изтрий</button>
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
