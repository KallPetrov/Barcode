import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { changePassword } from '../api/client';

export function ChangePasswordPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      setError('Паролите не съвпадат');
      return;
    }
    if (newPassword.length < 8) {
      setError('Паролата трябва да е поне 8 символа');
      return;
    }

    setError('');
    setLoading(true);
    try {
      await changePassword(newPassword);
      // After password change, we might need to re-login or the token might be updated
      // For now, let's just force logout so they login with new password
      alert('Паролата е променена успешно. Моля, влезте отново.');
      logout();
      navigate('/login');
    } catch (err: any) {
      setError(err.message || 'Грешка при смяна на парола');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <h1>Смяна на парола</h1>
        <p>Вашият акаунт изисква задължителна смяна на парола</p>

        {error && <div className="error">{error}</div>}

        <label>
          Нова парола
          <input
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            required
          />
        </label>

        <label>
          Потвърдете паролата
          <input
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
          />
        </label>

        <button type="submit" disabled={loading}>
          {loading ? 'Обработка...' : 'Смени парола'}
        </button>

        <button type="button" onClick={() => logout()} className="secondary">
          Изход
        </button>
      </form>
    </div>
  );
}
