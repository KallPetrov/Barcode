import { useEffect, useState } from 'react';
import { getItemLabel, getLocationLabel } from '../api/client';

interface LabelModalProps {
  type: 'item' | 'location';
  id: string;
  onClose: () => void;
}

export function LabelModal({ type, id, onClose }: LabelModalProps) {
  const [zpl, setZpl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchLabel = async () => {
      try {
        setLoading(true);
        const data = type === 'item' ? await getItemLabel(id) : await getLocationLabel(id);
        setZpl(data);
      } catch (err) {
        console.error('Failed to fetch label:', err);
        setError('Неуспешно генериране на етикет');
      } finally {
        setLoading(false);
      }
    };
    fetchLabel();
  }, [type, id]);

  const previewUrl = zpl
    ? `https://api.labelary.com/v1/printers/8dpmm/labels/4x6/0/${encodeURIComponent(zpl)}`
    : null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <header className="modal-header">
          <h2>Преглед на етикет</h2>
          <button className="btn-close" onClick={onClose}>&times;</button>
        </header>
        <div className="modal-body text-center">
          {loading && <p>Генериране...</p>}
          {error && <p className="error">{error}</p>}
          {previewUrl && (
            <div className="label-preview">
              <img src={previewUrl} alt="Label Preview" style={{ maxWidth: '100%', height: 'auto', border: '1px solid #ccc' }} />
              <div className="mt-4">
                <button className="btn-primary" onClick={() => window.print()}>Печат</button>
                <button className="btn-secondary" onClick={() => {
                   const blob = new Blob([zpl!], { type: 'text/plain' });
                   const url = URL.createObjectURL(blob);
                   const a = document.createElement('a');
                   a.href = url;
                   a.download = `label_${id}.zpl`;
                   a.click();
                }}>Изтегли ZPL</button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
