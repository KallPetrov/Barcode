import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export function Layout() {
  const { user, logout } = useAuth();

  return (
    <div className="layout">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-icon">▣</span>
          <div>
            <strong>CALAC</strong>
            <small>Админ конзола</small>
          </div>
        </div>
        <nav>
          <NavLink to="/" end>Табло</NavLink>
          <NavLink to="/devices">Устройства</NavLink>
          <NavLink to="/locations">Локации</NavLink>
          <NavLink to="/items">Артикули</NavLink>
          <NavLink to="/inventory">Инвентар</NavLink>
          <NavLink to="/inventory-sessions">Инвентаризации</NavLink>
          <NavLink to="/goods-receipts">Приемане на стоки</NavLink>
          <NavLink to="/transfers">Трансфери</NavLink>
          <NavLink to="/picking">Picking</NavLink>
          <NavLink to="/work-tasks">Операторски задачи</NavLink>
          <NavLink to="/operator-performance">Отчети</NavLink>
          <NavLink to="/alerts">Известия</NavLink>
          <NavLink to="/audit-log">Одит лог</NavLink>
          {user?.role === 'Admin' && <NavLink to="/erp-configurations">ERP Конфигурации</NavLink>}
          {user?.role === 'Admin' && <NavLink to="/users">Потребители</NavLink>}
        </nav>
        <div className="sidebar-footer">
          <div className="user-info">
            <strong>{user?.fullName}</strong>
            <span>{user?.role}</span>
          </div>
          <button type="button" onClick={logout}>Изход</button>
        </div>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}
