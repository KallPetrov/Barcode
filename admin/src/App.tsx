import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { AdminRoute, ProtectedRoute } from './components/ProtectedRoute';
import { AuthProvider } from './context/AuthContext';
import { DashboardPage } from './pages/DashboardPage';
import { DevicesPage } from './pages/DevicesPage';
import { LoginPage } from './pages/LoginPage';
import { UsersPage } from './pages/UsersPage';
import { LocationsPage } from './pages/LocationsPage';
import { ItemsPage } from './pages/ItemsPage';
import { InventoryPage } from './pages/InventoryPage';
import { InventorySessionsPage } from './pages/InventorySessionsPage';
import { GoodsReceiptsPage } from './pages/GoodsReceiptsPage';
import { TransfersPage } from './pages/TransfersPage';
import { PickingPage } from './pages/PickingPage';
import { ErpConfigurationsPage } from './pages/ErpConfigurationsPage';
import { WorkTasksPage } from './pages/WorkTasksPage';
import { OperatorPerformancePage } from './pages/OperatorPerformancePage';
import { AlertsPage } from './pages/AlertsPage';
import { AuditLogPage } from './pages/AuditLogPage';
import BillingPage from './pages/BillingPage';
import { ShippingDashboardPage } from './pages/ShippingDashboardPage';
import { EcommerceDashboardPage } from './pages/EcommerceDashboardPage';
import './App.css';

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<Layout />}>
              <Route index element={<DashboardPage />} />
              <Route path="devices" element={<DevicesPage />} />
              <Route path="locations" element={<LocationsPage />} />
              <Route path="items" element={<ItemsPage />} />
              <Route path="inventory" element={<InventoryPage />} />
              <Route path="inventory-sessions" element={<InventorySessionsPage />} />
              <Route path="goods-receipts" element={<GoodsReceiptsPage />} />
              <Route path="transfers" element={<TransfersPage />} />
              <Route path="picking" element={<PickingPage />} />
              <Route path="work-tasks" element={<WorkTasksPage />} />
              <Route path="operator-performance" element={<OperatorPerformancePage />} />
              <Route path="alerts" element={<AlertsPage />} />
              <Route path="audit-log" element={<AuditLogPage />} />
              <Route path="billing" element={<BillingPage />} />
              <Route path="shipping" element={<ShippingDashboardPage />} />
              <Route path="ecommerce" element={<EcommerceDashboardPage />} />
              <Route element={<AdminRoute />}>
                <Route path="users" element={<UsersPage />} />
                <Route path="erp-configurations" element={<ErpConfigurationsPage />} />
              </Route>
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
