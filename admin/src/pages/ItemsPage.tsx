import { useState, type FormEvent } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { createItem, getItems, updateItem, deleteItem, type Item } from '../api/client';
import { LabelModal } from '../components/LabelModal';

export function ItemsPage() {
  const queryClient = useQueryClient();
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
  const [printId, setPrintId] = useState<string | null>(null);

  const { data: items = [], isLoading } = useQuery({
    queryKey: ['items'],
    queryFn: getItems,
  });

  const createMutation = useMutation({
    mutationFn: createItem,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['items'] });
      setShowForm(false);
      resetForm();
    },
    onError: () => setError('Грешка при създаване на артикул'),
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: any }) => updateItem(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['items'] });
      setShowForm(false);
      setEditingId(null);
      resetForm();
    },
    onError: () => setError('Грешка при редактиране на артикул'),
  });

  const deleteMutation = useMutation({
    mutationFn: deleteItem,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['items'] }),
    onError: () => setError('Грешка при изтриване на артикул'),
  });

  const resetForm = () => {
    setForm({
      sku: '',
      name: '',
      description: '',
      barcode: '',
      barcodeType: 'Unknown',
      imageUrl: '',
      weight: '',
      unitOfMeasure: '',
      isActive: true,
    });
  };

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
      deleteMutation.mutate(id);
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    const payload = {
      ...form,
      weight: form.weight ? parseFloat(form.weight) : undefined,
    };
    if (editingId) {
      updateMutation.mutate({ id: editingId, payload });
    } else {
      createMutation.mutate(payload);
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
          resetForm();
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
          <label>Тип баркод
            <select value={form.barcodeType} onChange={(e) => setForm({ ...form, barcodeType: e.target.value })}>
              <option value="Unknown">Unknown</option>
              <option value="Upc">UPC CODE</option>
              <option value="Ean">EAN CODE</option>
              <option value="Code39">CODE 39</option>
              <option value="Code128">CODE 128</option>
              <option value="Itf">ITF (Interleaved 2 of 5)</option>
              <option value="Code93">CODE 93</option>
              <option value="Codabar">CODABAR</option>
              <option value="Gs1DataBar">GS1 DATABAR</option>
              <option value="MsiPlessey">MSI PLESSEY</option>
              <option value="Codablock">CODABLOCK</option>
              <option value="QrCode">QR CODE</option>
              <option value="DataMatrix">DATAMATRIX CODE</option>
              <option value="Pdf417">PDF417</option>
              <option value="Aztec">AZTEC</option>
              <option value="MaxiCode">MAXICODE</option>
              <option value="HanXin">HAN XIN CODE</option>
              <option value="DotCode">DOTCODE</option>
              <option value="Gs1128">GS1-128</option>
            </select>
          </label>
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

      {isLoading ? (
        <div className="panel empty">Зареждане...</div>
      ) : items.length === 0 ? (
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
                  <button type="button" onClick={() => setPrintId(i.id)}>Печат</button>
                  <button type="button" onClick={() => handleDelete(i.id)}>Изтрий</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {printId && (
        <LabelModal type="item" id={printId} onClose={() => setPrintId(null)} />
      )}
    </div>
  );
}
