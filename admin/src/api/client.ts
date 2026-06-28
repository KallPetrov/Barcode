import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';

export const api = axios.create({
  baseURL: API_URL,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      const refreshTokenValue = localStorage.getItem('refreshToken');

      if (refreshTokenValue) {
        try {
          const { token, refreshToken: newRefreshToken } = await refreshToken(refreshTokenValue);
          localStorage.setItem('token', token);
          localStorage.setItem('refreshToken', newRefreshToken);
          api.defaults.headers.common['Authorization'] = `Bearer ${token}`;
          return api(originalRequest);
        } catch (refreshError) {
          localStorage.removeItem('token');
          localStorage.removeItem('refreshToken');
          window.location.href = '/login';
          return Promise.reject(refreshError);
        }
      }
    }

    return Promise.reject(error);
  }
);

export interface User {
  id: string;
  username: string;
  fullName: string;
  role: 'Operator' | 'Supervisor' | 'Admin';
  tenantId: string;
  tenantName: string;
}

export interface Device {
  id: string;
  hardwareId: string;
  name: string;
  manufacturer?: string;
  model?: string;
  appVersion?: string;
  status: 'Offline' | 'Online' | 'Maintenance';
  batteryLevel?: number;
  assignedUserName?: string;
  registeredAt: string;
  lastSeenAt?: string;
}

export interface DashboardStats {
  totalDevices: number;
  onlineDevices: number;
  offlineDevices: number;
  activeTasks: number;
  urgentTasks: number;
  activeInventorySessions: number;
  activePickings: number;
  recentActivity: Array<{
    action: string;
    entityType?: string;
    details?: string;
    createdAt: string;
  }>;
  unreadAlertsCount?: number;
  recentAlerts?: Array<{
    id: string;
    title: string;
    message: string;
    level: string;
    isRead: boolean;
    createdAt: string;
  }>;
  serverTime: string;
}

export async function login(username: string, password: string) {
  const { data } = await api.post<{ token: string; refreshToken: string; user: User }>('/api/auth/login', { username, password });
  return data;
}

export async function getMe() {
  const { data } = await api.get<User>('/api/auth/me');
  return data;
}

export async function getDashboardStats() {
  const { data } = await api.get<DashboardStats>('/api/dashboard/stats');
  return data;
}

export async function getDevices() {
  const { data } = await api.get<Device[]>('/api/devices');
  return data;
}

export async function getUsers() {
  const { data } = await api.get<User[]>('/api/users');
  return data;
}

export async function createUser(payload: {
  username: string;
  password: string;
  fullName: string;
  role: string;
  pin?: string;
}) {
  const { data } = await api.post<User>('/api/users', payload);
  return data;
}

export interface Location {
  id: string;
  code: string;
  name: string;
  zone?: string;
  aisle?: string;
  rack?: string;
  level?: string;
  position?: string;
  isActive: boolean;
  createdAt: string;
}

export interface Item {
  id: string;
  sku: string;
  name: string;
  description?: string;
  barcode?: string;
  barcodeType?: string;
  imageUrl?: string;
  weight?: number;
  unitOfMeasure?: string;
  isActive: boolean;
  createdAt: string;
}

export interface InventoryStock {
  id: string;
  itemId: string;
  itemName: string;
  locationId: string;
  locationName: string;
  quantity: number;
  reservedQuantity?: number;
  batchNumber?: string;
  serialNumber?: string;
  expiryDate?: string;
  createdAt: string;
  updatedAt?: string;
}

export async function getLocations() {
  const { data } = await api.get<Location[]>('/api/locations');
  return data;
}

export async function createLocation(payload: {
  code: string;
  name: string;
  zone?: string;
  aisle?: string;
  rack?: string;
  level?: string;
  position?: string;
}) {
  const { data } = await api.post<Location>("/api/locations", payload);
  return data;
}

export async function updateLocation(
  id: string,
  payload: {
    code: string;
    name: string;
    zone?: string;
    aisle?: string;
    rack?: string;
    level?: string;
    position?: string;
    isActive: boolean;
  }
) {
  const { data } = await api.put<Location>(`/api/locations/${id}`, payload);
  return data;
}

export async function deleteLocation(id: string) {
  await api.delete(`/api/locations/${id}`);
}

export async function updateItem(
  id: string,
  payload: {
    sku: string;
    name: string;
    description?: string;
    barcode?: string;
    barcodeType?: string;
    imageUrl?: string;
    weight?: number;
    unitOfMeasure?: string;
    isActive: boolean;
  }
) {
  const { data } = await api.put<Item>(`/api/items/${id}`, payload);
  return data;
}

export async function deleteItem(id: string) {
  await api.delete(`/api/items/${id}`);
}

export interface InventorySession {
  id: string;
  name: string;
  description?: string;
  status: string;
  startedAt?: string;
  completedAt?: string;
  startedByUserId?: string;
  startedByUserName?: string;
  createdAt: string;
}

export interface WorkTask {
  id: string;
  title: string;
  description?: string;
  taskType: string;
  priority: number;
  status: number;
  assignedUserId?: string;
  assignedUserName?: string;
  reference?: string;
  dueDate?: string;
  createdAt: string;
  updatedAt?: string;
  completedAt?: string;
}

export interface OperatorPerformance {
  id: string;
  userId: string;
  userName: string;
  period: string;
  tasksAssigned: number;
  tasksCompleted: number;
  tasksOverdue: number;
  pickingCompleted: number;
  inventorySessionsCompleted: number;
  efficiencyRate: number;
  createdAt: string;
}

export interface AlertItem {
  id: string;
  title: string;
  message: string;
  level: string;
  isRead: boolean;
  createdAt: string;
}

export interface ReminderItem {
  id: string;
  title: string;
  message?: string;
  userId?: string;
  userName?: string;
  relatedEntityId?: string;
  relatedEntityType?: string;
  dueAt: string;
  isCompleted: boolean;
  createdAt: string;
  completedAt?: string;
}

export interface InventoryCount {
  id: string;
  sessionId: string;
  itemId: string;
  itemName: string;
  locationId: string;
  locationName: string;
  systemQuantity: number;
  countedQuantity?: number;
  batchNumber?: string;
  serialNumber?: string;
  countedByUserId?: string;
  countedByUserName?: string;
  countedAt?: string;
  createdAt: string;
}

export async function getInventorySessions() {
  const { data } = await api.get<InventorySession[]>('/api/inventorysessions');
  return data;
}

export async function getInventorySession(id: string) {
  const { data } = await api.get<InventorySession>(`/api/inventorysessions/${id}`);
  return data;
}

export async function createInventorySession(payload: { name: string; description?: string }) {
  const { data } = await api.post<InventorySession>('/api/inventorysessions', payload);
  return data;
}

export async function startInventorySession(id: string) {
  const { data } = await api.put<InventorySession>(`/api/inventorysessions/${id}/start`);
  return data;
}

export async function completeInventorySession(id: string) {
  const { data } = await api.put<InventorySession>(`/api/inventorysessions/${id}/complete`);
  return data;
}

export async function getInventoryCounts(sessionId: string) {
  const { data } = await api.get<InventoryCount[]>(`/api/inventorysessions/${sessionId}/counts`);
  return data;
}

export async function updateInventoryCount(id: string, payload: { countedQuantity: number }) {
  const { data } = await api.put<InventoryCount>(`/api/inventorysessions/counts/${id}`, payload);
  return data;
}

export async function getItems() {
  const { data } = await api.get<Item[]>('/api/items');
  return data;
}

export async function getWorkTasks() {
  const { data } = await api.get<WorkTask[]>('/api/worktasks');
  return data;
}

export async function createWorkTask(payload: {
  title: string;
  description?: string;
  taskType: string;
  priority: number;
  assignedUserId?: string;
  reference?: string;
  dueDate?: string;
}) {
  const { data } = await api.post<WorkTask>('/api/worktasks', payload);
  return data;
}

export async function updateWorkTask(id: string, payload: {
  title?: string;
  description?: string;
  taskType?: string;
  priority?: number;
  status?: number;
  assignedUserId?: string;
  reference?: string;
  dueDate?: string;
}) {
  const { data } = await api.put<WorkTask>(`/api/worktasks/${id}`, payload);
  return data;
}

export async function getOperatorPerformance() {
  const { data } = await api.get<OperatorPerformance[]>('/api/operatorperformance');
  return data;
}

export async function generateOperatorPerformance(payload: { period: string; userId: string }) {
  const { data } = await api.post<OperatorPerformance>('/api/operatorperformance/generate', payload);
  return data;
}

export async function getAlerts() {
  const { data } = await api.get<AlertItem[]>('/api/alerts');
  return data;
}

export async function createAlert(payload: { title: string; message: string; level: string }) {
  const { data } = await api.post<AlertItem>('/api/alerts', payload);
  return data;
}

export async function markAlertRead(id: string) {
  await api.put(`/api/alerts/${id}/read`);
}

export async function getReminders() {
  const { data } = await api.get<ReminderItem[]>('/api/reminders');
  return data;
}

export async function createReminder(payload: { title: string; message?: string; userId?: string; relatedEntityId?: string; relatedEntityType?: string; dueAt: string }) {
  const { data } = await api.post<ReminderItem>('/api/reminders', payload);
  return data;
}

export async function completeReminder(id: string) {
  await api.put(`/api/reminders/${id}/complete`);
}

export interface OperatorActionHistoryItem {
  id: string;
  action: string;
  details?: string;
  userName?: string;
  createdAt: string;
}

export interface SlaTaskMetric {
  id: string;
  title: string;
  reference?: string;
  status: string;
  slaStatus: string;
  daysRemaining?: number;
  isOverdue: boolean;
  dueDate?: string;
  createdAt: string;
}

export interface SlaOverview {
  totalTasks: number;
  overdueTasks: number;
  atRiskTasks: number;
  onTrackTasks: number;
  tasks: SlaTaskMetric[];
}

export async function getOperatorHistory() {
  const { data } = await api.get<OperatorActionHistoryItem[]>('/api/operatorhistory');
  return data;
}

export async function getSlaOverview() {
  const { data } = await api.get<SlaOverview>('/api/sla/overview');
  return data;
}

export async function createItem(payload: {
  sku: string;
  name: string;
  description?: string;
  barcode?: string;
  barcodeType?: string;
  imageUrl?: string;
  weight?: number;
  unitOfMeasure?: string;
}) {
  const { data } = await api.post<Item>('/api/items', payload);
  return data;
}

export async function getInventoryStock() {
  const { data } = await api.get<InventoryStock[]>('/api/inventory/stock');
  return data;
}

export async function addInventoryStock(payload: {
  itemId: string;
  locationId: string;
  quantity: number;
  batchNumber?: string;
  serialNumber?: string;
  expiryDate?: string;
}) {
  const { data } = await api.post<InventoryStock>('/api/inventory/stock', payload);
  return data;
}

export interface PickingOrder {
  id: string;
  name: string;
  reference?: string;
  strategy: string;
  status: string;
  assignedUserId?: string;
  assignedUserName?: string;
  startedByUserId?: string;
  startedByUserName?: string;
  completedByUserId?: string;
  completedByUserName?: string;
  startedAt?: string;
  completedAt?: string;
  notes?: string;
  createdAt: string;
  lines: PickingOrderLine[];
}

export interface PickingOrderLine {
  id: string;
  itemId: string;
  itemName: string;
  sourceLocationId?: string;
  sourceLocationName?: string;
  targetLocationId?: string;
  targetLocationName?: string;
  quantity: number;
  pickedQuantity?: number;
  notes?: string;
  stockLines: PickingStockLine[];
}

export interface PickingStockLine {
  id: string;
  inventoryStockId: string;
  itemName: string;
  locationName: string;
  quantity: number;
  batchNumber?: string;
  serialNumber?: string;
  expiryDate?: string;
  pickedByUserId?: string;
  pickedByUserName?: string;
  pickedAt?: string;
}

export async function getPickingOrders() {
  const { data } = await api.get<PickingOrder[]>('/api/picking');
  return data;
}

export async function getPickingOrder(id: string) {
  const { data } = await api.get<PickingOrder>(`/api/picking/${id}`);
  return data;
}

export async function createPickingOrder(payload: {
  name: string;
  reference?: string;
  strategy: string;
  assignedUserId?: string;
  notes?: string;
  lines: Array<{
    itemId: string;
    sourceLocationId?: string;
    targetLocationId?: string;
    quantity: number;
    notes?: string;
  }>;
}) {
  const { data } = await api.post<PickingOrder>('/api/picking', payload);
  return data;
}

export async function startPickingOrder(id: string) {
  const { data } = await api.put<PickingOrder>(`/api/picking/${id}/start`);
  return data;
}

export async function completePickingOrder(id: string) {
  const { data } = await api.put<PickingOrder>(`/api/picking/${id}/complete`);
  return data;
}

export async function updatePickingStockLine(id: string, payload: { pickedQuantity: number }) {
  const { data } = await api.put<PickingOrder>(`/api/picking/stockline/${id}`, payload);
  return data;
}

export interface GoodsReceipt {
  id: string;
  name: string;
  reference?: string;
  supplierName?: string;
  status: string;
  receivedByUserId?: string;
  receivedByUserName?: string;
  completedByUserId?: string;
  completedByUserName?: string;
  receivedAt?: string;
  completedAt?: string;
  notes?: string;
  createdAt: string;
  lines: GoodsReceiptLine[];
}

export interface GoodsReceiptLine {
  id: string;
  itemId: string;
  itemName: string;
  locationId: string;
  locationName: string;
  expectedQuantity: number;
  receivedQuantity?: number;
  batchNumber?: string;
  serialNumber?: string;
  expiryDate?: string;
  receivedAt?: string;
  notes?: string;
}

export async function getGoodsReceipts() {
  const { data } = await api.get<GoodsReceipt[]>('/api/goodsreceipts');
  return data;
}

export async function getGoodsReceipt(id: string) {
  const { data } = await api.get<GoodsReceipt>(`/api/goodsreceipts/${id}`);
  return data;
}

export async function createGoodsReceipt(payload: {
  name: string;
  reference?: string;
  supplierName?: string;
  notes?: string;
  lines: Array<{
    itemId: string;
    locationId: string;
    expectedQuantity: number;
    batchNumber?: string;
    serialNumber?: string;
    expiryDate?: string;
    notes?: string;
  }>;
}) {
  const { data } = await api.post<GoodsReceipt>('/api/goodsreceipts', payload);
  return data;
}

export async function startGoodsReceipt(id: string) {
  const { data } = await api.put<GoodsReceipt>(`/api/goodsreceipts/${id}/start`);
  return data;
}

export async function completeGoodsReceipt(id: string) {
  const { data } = await api.put<GoodsReceipt>(`/api/goodsreceipts/${id}/complete`);
  return data;
}

export async function updateGoodsReceiptLine(id: string, payload: { receivedQuantity: number }) {
  const { data } = await api.put<GoodsReceipt>(`/api/goodsreceipts/lines/${id}`, payload);
  return data;
}

export interface TransferOrder {
  id: string;
  name: string;
  reference?: string;
  status: string;
  movedByUserId?: string;
  movedByUserName?: string;
  completedByUserId?: string;
  completedByUserName?: string;
  movedAt?: string;
  completedAt?: string;
  notes?: string;
  createdAt: string;
  lines: TransferOrderLine[];
}

export interface TransferOrderLine {
  id: string;
  itemId: string;
  itemName: string;
  sourceLocationId: string;
  sourceLocationName: string;
  targetLocationId: string;
  targetLocationName: string;
  quantity: number;
  movedQuantity?: number;
  movedAt?: string;
  notes?: string;
}

export async function getTransfers() {
  const { data } = await api.get<TransferOrder[]>('/api/transfers');
  return data;
}

export async function getTransfer(id: string) {
  const { data } = await api.get<TransferOrder>(`/api/transfers/${id}`);
  return data;
}

export async function createTransfer(payload: {
  name: string;
  reference?: string;
  notes?: string;
  lines: Array<{
    itemId: string;
    sourceLocationId: string;
    targetLocationId: string;
    quantity: number;
    notes?: string;
  }>;
}) {
  const { data } = await api.post<TransferOrder>('/api/transfers', payload);
  return data;
}

export async function startTransfer(id: string) {
  const { data } = await api.put<TransferOrder>(`/api/transfers/${id}/start`);
  return data;
}

export async function completeTransfer(id: string) {
  const { data } = await api.put<TransferOrder>(`/api/transfers/${id}/complete`);
  return data;
}

export async function updateTransferLine(id: string, payload: { movedQuantity: number }) {
  const { data } = await api.put<TransferOrder>(`/api/transfers/lines/${id}`, payload);
  return data;
}

export interface ErpConfiguration {
  id: string;
  name: string;
  providerType: string;
  apiUrl?: string;
  databaseName?: string;
  isActive: boolean;
  autoSyncItems: boolean;
  autoSyncInventory: boolean;
  lastSyncAt?: string;
  createdAt: string;
  updatedAt?: string;
}

export async function getErpConfigurations() {
  const { data } = await api.get<ErpConfiguration[]>('/api/erpconfigurations');
  return data;
}

export async function getErpConfiguration(id: string) {
  const { data } = await api.get<ErpConfiguration>(`/api/erpconfigurations/${id}`);
  return data;
}

export async function createErpConfiguration(payload: {
  name: string;
  providerType: string;
  apiUrl?: string;
  apiKey?: string;
  username?: string;
  password?: string;
  databaseName?: string;
  autoSyncItems: boolean;
  autoSyncInventory: boolean;
  settingsJson?: string;
}) {
  const { data } = await api.post<ErpConfiguration>('/api/erpconfigurations', payload);
  return data;
}

export async function updateErpConfiguration(id: string, payload: {
  name: string;
  providerType: string;
  apiUrl?: string;
  apiKey?: string;
  username?: string;
  password?: string;
  databaseName?: string;
  isActive: boolean;
  autoSyncItems: boolean;
  autoSyncInventory: boolean;
  settingsJson?: string;
}) {
  const { data } = await api.put<ErpConfiguration>(`/api/erpconfigurations/${id}`, payload);
  return data;
}

export async function deleteErpConfiguration(id: string) {
  await api.delete(`/api/erpconfigurations/${id}`);
}

export async function testErpConnection(id: string) {
  const { data } = await api.put<boolean>(`/api/erpconfigurations/${id}/test`);
  return data;
}

export async function syncErpItems(id: string) {
  await api.put(`/api/erpconfigurations/${id}/sync-items`);
}

export async function syncErpInventory(id: string) {
  await api.put(`/api/erpconfigurations/${id}/sync-inventory`);
}

export interface AuditLogItem {
  id: string;
  userId?: string;
  userName?: string;
  deviceId?: string;
  deviceName?: string;
  action: string;
  entityType?: string;
  entityId?: string;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}

export async function getAuditLogs() {
  const { data } = await api.get<AuditLogItem[]>('/api/audit');
  return data;
}

export async function refreshToken(token: string) {
  const { data } = await api.post<{ token: string; refreshToken: string; user: User }>('/api/auth/refresh', { refreshToken: token });
  return data;
}

export async function logoutApi(token: string) {
  await api.post('/api/auth/logout', { refreshToken: token });
}

export async function getItemLabel(id: string, qty: number = 1) {
  const { data } = await api.get<string>(`/api/labels/item/${id}?qty=${qty}`);
  return data;
}

export async function getLocationLabel(id: string) {
  const { data } = await api.get<string>(`/api/labels/location/${id}`);
  return data;
}
