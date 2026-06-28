import { useEffect, useState, type FormEvent } from 'react';
import { createItem, getItems, updateItem, deleteItem, type Item } from '../api/client';

export function ItemsPage() {
  const [items, setItems] = useState<Item[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState({
    sku: '',
    name: '',
    description: '',
    barcode: '',
    barcodeType: '',
    imageUrl: '',
    weight: '',
    unitOfMeasure: '',
    isActive: true,
  });
  const [error, setError] = useState('');

  const load = () => getItems().then(setItems);
  useEffect(() => { load(); }, []);

  const handleEdit = (item: Item) => {
    setEditingId(item.id);
    setForm({
      sku: item.sku,
      name: item.name,
      description: item.description ?? '',
      barcode: item.barcode ?? '',
      barcodeType: item.barcodeType ?? '',
      imageUrl: item.imageUrl ?? '',
      weight: item.weight?.toString() ?? '',
      unitOfMeasure: item.unitOfMeasure ?? '',
      isActive: item.isActive,
    });
    setShowForm(true);
  };

  const handleDelete = async (id: string) => {
    if (confirm('Сигурни ли сте, че искате да изтриете този артикул?')) {
      try {
        await deleteItem(id);
        await load();
      } catch {
        setError('Грешка при изтриване на артикул');
      }
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      const payload = {
        ...form,
        weight: form.weight ? parseFloat(form.weight) : undefined,
      };
      if (editingId) {
        await updateItem(editingId, payload);
      } else {
        await createItem(payload);
      }
      setShowForm(false);
      setEditingId(null);
      setForm({
        sku: '',
        name: '',
        description: '',
        barcode: '',
        barcodeType: '',
        imageUrl: '',
        weight: '',
        unitOfMeasure: '',
        isActive: true,
      });
      await load();
    } catch {
      setError(editingId ? 'Грешка при редактиране на артикул' : 'Грешка при създаване на артикул');
    }
  };

  return (
    <div>
      <header className="page-header row">
        <div>
          <h1>Артикули</h1>
          <p>Управление на складови артикули</p>
        </div>
        <button type="button" onClick={() => {
          setShowForm(!showForm);
          setEditingId(null);
          setForm({
            sku: '',
            name: '',
            description: '',
            barcode: '',
            barcodeType: '',
            imageUrl: '',
            weight: '',
            unitOfMeasure: '',
            isActive: true,
          });
        }}>
          {showForm ? 'Отказ' : '+ Нов артикул'}
        </button>
      </header>

      {showForm && (
        <form className="panel form-grid" onSubmit={handleSubmit}>
          {error && <div className="error">{error}</div>}
          <label>SKU<input required value={form.sku} onChange={(e) => setForm({ ...form, sku: e.target.value })} /></label>
          <label>Име<input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
          <label>Описание<textarea value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
          <label>Баркод<input value={form.barcode} onChange={(e) => setForm({ ...form, barcode: e.target.value })} /></label>
          <label>Тип баркод<input value={form.barcodeType} onChange={(e) => setForm({ ...form, barcodeType: e.target.value })} /></label>
          <label>Снимка URL<input value={form.imageUrl} onChange={(e) => setForm({ ...form, imageUrl: e.target.value })} /></label>
          <label>Тегло (кг)<input type="number" step="0.001" value={form.weight} onChange={(e) => setForm({ ...form, weight: e.target.value })} /></label>
          <label>Мярка<input value={form.unitOfMeasure} onChange={(e) => setForm({ ...form, unitOfMeasure: e.target.value })} /></label>
          <label>Активен<select value={form.isActive ? 'true' : 'false'} onChange={(e) => setForm({ ...form, isActive: e.target.value === 'true' })}>
            <option value='true'>Да</option>
            <option value='false'>Не</option>
          </select></label>
          <button type="submit">{editingId ? 'Запази' : 'Създай'}</button>
        </form>
      )}

      {items.length === 0 ? (
        <div className="panel empty">Няма създадени артикули</div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>SKU</th>
              <th>Име</th>
              <th>Описание</th>
              <th>Баркод</th>
              <th>Мярка</th>
              <th>Активен</th>
              <th>Действия</th>
            </tr>
          </thead>
          <tbody>
            {items.map((i) => (
              <tr key={i.id}>
                <td><code>{i.sku}</code></td>
                <td>{i.name}</td>
                <td>{i.description ?? '—'}</td>
                <td>{i.barcode ?? '—'}</td>
                <td>{i.unitOfMeasure ?? '—'}</td>
                <td><span className={`badge ${i.isActive ? 'online' : 'offline'}`}>{i.isActive ? 'Да' : 'Не'}</span></td>
                <td>
                  <button type="button" onClick={() => handleEdit(i)}>Редактирай</button>
                  <button type="button" onClick={() => handleDelete(i.id)}>Изтрий</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
