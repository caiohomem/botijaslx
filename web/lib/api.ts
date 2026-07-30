import { formatPhoneForWhatsAppByType, PhoneMode } from './phone';

const PROD_API_BASE_URL = 'https://botijaslx.onrender.com';

const BROWSER_API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE ||
  process.env.NEXT_PUBLIC_API_URL ||
  (process.env.NODE_ENV === 'production' ? PROD_API_BASE_URL : 'http://localhost:8080');

const SERVER_API_BASE_URL =
  process.env.INTERNAL_API_BASE ||
  BROWSER_API_BASE_URL;

function getApiBaseUrl(): string {
  return typeof window === 'undefined' ? SERVER_API_BASE_URL : BROWSER_API_BASE_URL;
}

const API_KEY = process.env.NEXT_PUBLIC_API_KEY ?? 'oficina';

function withApiKey(headers: HeadersInit = {}): HeadersInit {
  const result = new Headers(headers);
  if (API_KEY) {
    result.set('X-Api-Key', API_KEY);
  }
  return result;
}

export async function waitForApiReady(): Promise<void> {
  const response = await fetch(`${BROWSER_API_BASE_URL}/health`, {
    method: 'GET',
    cache: 'no-store',
  });

  if (!response.ok) {
    throw new Error(`API healthcheck failed with status ${response.status}`);
  }
}

export async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await fetch(`${getApiBaseUrl()}${endpoint}`, {
    ...options,
    headers: withApiKey({
      'Content-Type': 'application/json',
      ...options.headers,
    }),
  });

  if (!response.ok) {
    const rawBody = await response.text().catch(() => '');
    let message = '';
    if (rawBody) {
      try {
        const parsed = JSON.parse(rawBody) as { error?: string; title?: string; detail?: string };
        message = parsed.error || parsed.detail || parsed.title || '';
      } catch {
        message = rawBody.slice(0, 200).trim();
      }
    }
    throw new Error(message || `HTTP ${response.status}`);
  }

  // Handle 204 No Content
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  return response.json();
}

export interface AppSettings {
  storeName: string;
  storePhone: string;
  storeLink: string;
  appTitle: string;
  whatsAppMessageTemplate: string;
  shippingReadyMessageTemplate: string;
  welcomeMessageTemplate: string;
  thankYouMessageTemplate: string;
  deadlineMessageTemplate: string;
  printerType: 'label' | 'a4';
  labelWidthMm: number;
  labelHeightMm: number;
  debugEnabled: boolean;
  soundNotificationsDisabled: boolean;
  updatedAt?: string;
}

// Customers
export const customersApi = {
  create: (data: { name: string; phone: string; phoneType: string }) =>
    apiRequest<{ customerId: string; name: string; phone: string; phoneType: string }>('/api/customers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  search: (query?: string) =>
    apiRequest<{ customers: Array<{ customerId: string; name: string; phone: string; phoneType: string }> }>(
      `/api/customers${query ? `?query=${encodeURIComponent(query)}` : ''}`
    ),

  getCylinders: (customerId: string) =>
    apiRequest<CustomerCylindersResult>(`/api/customers/${customerId}/cylinders`),

  updatePhone: (customerId: string, phone: string, phoneType: string) =>
    apiRequest<{ customerId: string; name: string; phone: string; phoneType: string }>(
      `/api/customers/${customerId}/phone`,
      {
        method: 'PUT',
        body: JSON.stringify({ phone, phoneType }),
      }
    ),

  updateName: (customerId: string, name: string) =>
    apiRequest<{ customerId: string; name: string; phone: string; phoneType: string }>(
      `/api/customers/${customerId}/name`,
      {
        method: 'PUT',
        body: JSON.stringify({ name }),
      }
    ),

  delete: (customerId: string) =>
    apiRequest<void>(`/api/customers/${customerId}`, {
      method: 'DELETE',
    }),
};

export interface CustomerCylinderHistoryItem {
  id: string;
  eventType: string;
  details?: string;
  timestamp: string;
}

export interface CustomerCylinder {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  createdAt: string;
  orderId: string;
  orderStatus: string;
  fulfillmentMethod: string;
  refillPaid: boolean;
  shippingPaid: boolean;
  orderCreatedAt: string;
  orderCompletedAt?: string;
  orderCancelledAt?: string;
  orderCancellationNotes?: string;
  history: CustomerCylinderHistoryItem[];
}

export interface CustomerOrder {
  orderId: string;
  status: string;
  fulfillmentMethod: string;
  refillPaid: boolean;
  shippingPaid: boolean;
  createdAt: string;
  completedAt?: string;
  cancelledAt?: string;
  cancellationNotes?: string;
  cylinders: CustomerCylinder[];
}

export interface CustomerCylindersResult {
  customerId: string;
  name: string;
  phone: string;
  phoneType: string;
  cylinders: CustomerCylinder[];
  orders: CustomerOrder[];
}

// Orders
export const ordersApi = {
  create: (data: {
    customerId: string;
    fulfillmentMethod: 'Pickup' | 'Shipping';
    refillPaid: boolean;
    shippingPaid: boolean;
  }) =>
    apiRequest<{
      orderId: string;
      customerId: string;
      status: string;
      fulfillmentMethod: string;
      refillPaid: boolean;
      shippingPaid: boolean;
      createdAt: string;
      shippedAt?: string;
      cylinderCount: number;
    }>('/api/orders', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  addCylinder: (orderId: string, cylinderId?: string) =>
    apiRequest<{
      cylinderId: string;
      sequentialNumber: number;
      labelToken?: string;
      state: string;
    }>(`/api/orders/${orderId}/cylinders`, {
      method: 'POST',
      body: JSON.stringify({ cylinderId }),
    }),

  addCylindersBatch: (orderId: string, quantity: number) =>
    apiRequest<{
      cylinders: Array<{
        cylinderId: string;
        sequentialNumber: number;
        labelToken?: string;
        state: string;
      }>;
    }>(`/api/orders/${orderId}/cylinders/batch`, {
      method: 'POST',
      body: JSON.stringify({ quantity }),
    }),

  scanCylinder: (orderId: string, qrToken: string) =>
    apiRequest<{
      cylinderId: string;
      sequentialNumber: number;
      labelToken?: string;
      state: string;
    }>(`/api/orders/${orderId}/cylinders/scan`, {
      method: 'POST',
      body: JSON.stringify({ qrToken }),
    }),

  cancel: (orderId: string, notes: string) =>
    apiRequest<{
      orderId: string;
      status: string;
      cancelledAt?: string;
      cancellationNotes?: string;
    }>(`/api/orders/${orderId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),

  deleteEmpty: (orderId: string) =>
    apiRequest<void>(`/api/orders/${orderId}`, {
      method: 'DELETE',
    }),
};

// Cylinders / Filling
export interface FillingQueueItem {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  receivedAt: string;
  orderId: string;
  customerName: string;
  customerPhone: string;
  customerPhoneType: string;
  fulfillmentMethod: string;
  totalCylindersInOrder: number;
  readyCylindersInOrder: number;
}

export interface MarkReadyResult {
  cylinderId: string;
  state: string;
  orderId: string;
  orderStatus: string;
  totalCylindersInOrder: number;
  readyCylindersInOrder: number;
  isOrderComplete: boolean;
  wasAlreadyReady?: boolean;
}

export interface ReportProblemResult {
  cylinderId: string;
  state: string;
  problemType: string;
  notes: string;
}

export interface ProblemCylinder {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  occurrenceNotes?: string;
  createdAt: string;
  orderId?: string;
  orderStatus?: string;
  customerId?: string;
  customerName?: string;
  customerPhone?: string;
  customerPhoneType?: string;
}

export interface AssignLabelResult {
  cylinderId: string;
  labelToken: string;
  previousLabelToken?: string;
}

export const cylindersApi = {
  getFillingQueue: () =>
    apiRequest<{ cylinders: FillingQueueItem[] }>('/api/cylinders/filling-queue'),

  markReady: (cylinderId: string) =>
    apiRequest<MarkReadyResult>(`/api/cylinders/${cylinderId}/mark-ready`, {
      method: 'POST',
    }),

  markReadyBatch: (orderId: string) =>
    apiRequest<{
      orderId: string;
      markedCount: number;
      isOrderComplete: boolean;
      totalCylindersInOrder: number;
    }>('/api/cylinders/batch/mark-ready', {
      method: 'POST',
      body: JSON.stringify({ orderId }),
    }),

  reportProblem: (cylinderId: string, type: string, notes: string) =>
    apiRequest<ReportProblemResult>(`/api/cylinders/${cylinderId}/report-problem`, {
      method: 'POST',
      body: JSON.stringify({ type, notes }),
    }),

  getProblems: () =>
    apiRequest<{ cylinders: ProblemCylinder[] }>('/api/cylinders/problems'),

  assignLabel: (cylinderId: string, qrToken: string) =>
    apiRequest<AssignLabelResult>(`/api/cylinders/${cylinderId}/assign-label`, {
      method: 'POST',
      body: JSON.stringify({ qrToken }),
    }),

  delete: (cylinderId: string) =>
    apiRequest<void>(`/api/cylinders/${cylinderId}`, {
      method: 'DELETE',
    }),

  undoHistoryAction: (cylinderId: string, historyEntryId: string, comment: string) =>
    apiRequest<{
      cylinderId: string;
      state: string;
      orderId?: string;
      orderStatus?: string;
      undoneEventType: string;
    }>(`/api/cylinders/${cylinderId}/history/${historyEntryId}/undo`, {
      method: 'POST',
      body: JSON.stringify({ comment }),
    }),
};

// Pickup
export interface PickupCylinder {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  occurrenceNotes?: string;
  isDelivered: boolean;
}

export interface PickupOrder {
  orderId: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  customerPhoneType: string;
  status: string;
  fulfillmentMethod: string;
  refillPaid: boolean;
  shippingPaid: boolean;
  createdAt: string;
  readyAt?: string;
  notifiedAt?: string;
  shippedAt?: string;
  needsNotification: boolean;
  totalCylinders: number;
  deliveredCylinders: number;
  cylinders: PickupCylinder[];
}

export interface DeliverCylinderResult {
  cylinderId: string;
  state: string;
  orderId: string;
  orderStatus: string;
  totalCylinders: number;
  deliveredCylinders: number;
  isOrderComplete: boolean;
}

export interface MarkNotifiedResult {
  orderId: string;
  notifiedAt: string;
}

export interface MarkShippedResult {
  orderId: string;
  orderStatus: string;
  shippedAt: string;
  totalCylinders: number;
}

export const pickupApi = {
  getReadyForPickup: (search?: string) =>
    apiRequest<{ orders: PickupOrder[] }>(
      `/api/orders/ready-for-pickup${search ? `?search=${encodeURIComponent(search)}` : ''}`
    ),

  deliverCylinder: (orderId: string, cylinderId: string) =>
    apiRequest<DeliverCylinderResult>(
      `/api/orders/${orderId}/cylinders/${cylinderId}/deliver`,
      { method: 'POST' }
    ),

  markNotified: (orderId: string) =>
    apiRequest<MarkNotifiedResult>(
      `/api/orders/${orderId}/mark-notified`,
      { method: 'POST' }
    ),

  markShipped: (orderId: string) =>
    apiRequest<MarkShippedResult>(
      `/api/orders/${orderId}/mark-shipped`,
      { method: 'POST' }
    ),
};

// Tracking (public self-service page)
export interface TrackingOrder {
  status: string;
  fulfillmentMethod: string;
  createdAt: string;
  readyAt?: string;
  completedAt?: string;
  shippedAt?: string;
  cancelledAt?: string;
  totalCylinders: number;
  receivedCylinders: number;
  readyCylinders: number;
  problemCylinders: number;
  deliveredCylinders: number;
}

export interface TrackingResult {
  customerName?: string;
  orders: TrackingOrder[];
}

export const trackingApi = {
  getStatus: (phone: string) =>
    apiRequest<TrackingResult>(`/api/tracking/status?phone=${encodeURIComponent(phone)}`),
};

// WhatsApp helper
export const generateWhatsAppLink = (phone: string, message: string, phoneType: PhoneMode = 'pt'): string => {
  const cleanPhone = formatPhoneForWhatsAppByType(phone, phoneType);
  const encodedMessage = encodeURIComponent(message);
  return `https://wa.me/${cleanPhone}?text=${encodedMessage}`;
};

// Cylinder History
export interface CylinderHistoryItem {
  id: string;
  eventType: string;
  details?: string;
  orderId?: string;
  timestamp: string;
}

export interface CylinderHistory {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  createdAt: string;
  currentOrderId?: string;
  currentOrderStatus?: string;
  customerName?: string;
  customerPhone?: string;
  history: CylinderHistoryItem[];
}

export const historyApi = {
  getByCylinderId: (cylinderId: string) =>
    apiRequest<CylinderHistory>(`/api/cylinders/${cylinderId}/history`),

  scanCylinder: (qrToken: string) =>
    apiRequest<CylinderHistory>(`/api/cylinders/scan/${encodeURIComponent(qrToken)}`),
};

// Reports
export interface DashboardStats {
  ordersOpen: number;
  ordersReadyForPickup: number;
  ordersReadyForShipping: number;
  ordersCompletedToday: number;
  ordersCompletedThisWeek: number;
  ordersAwaitingNotification: number;
  ordersAwaitingNotificationPickup: number;
  ordersAwaitingNotificationShipping: number;
  cylindersReceived: number;
  cylindersReady: number;
  cylindersWithProblem: number;
  cylindersFilledToday: number;
  cylindersReceivedToday: number;
  cylindersFilledThisWeek: number;
  totalCustomers: number;
  dailySeries: DashboardDailySeriesPoint[];
}

export interface DashboardDailySeriesPoint {
  date: string;
  received: number;
  ready: number;
  delivered: number;
}

export const reportsApi = {
  getStats: (days = 7) => apiRequest<DashboardStats>(`/api/reports/stats?days=${days}`),
};

// Business dashboard
export interface BusinessFinanceSettings {
  refillPriceEur: number;
  sourceCylinderCostEur: number;
  sourceCylinderGasKg: number;
  consumerCylinderGasG: number;
  fillsPerSourceCylinder: number;
  gasCostPerFillEur: number;
}

export interface BusinessDailyPoint {
  date: string;
  filled: number;
  delivered: number;
  revenue: number;
  gasCost: number;
  profit: number;
}

export interface BusinessTopCustomer {
  customerId: string;
  name: string;
  deliveredFills: number;
  revenue: number;
  lastDeliveredAt?: string;
}

export interface BusinessWeekdayStat {
  dayOfWeek: number;
  dayName: string;
  averageFills: number;
}

export interface BusinessMonthlyPoint {
  month: string;
  label: string;
  filled: number;
  delivered: number;
  revenue: number;
  gasCost: number;
  profit: number;
  growthPercent: number;
  isPartial: boolean;
  projectedDelivered?: number | null;
  projectedRevenue?: number | null;
  projectedProfit?: number | null;
  isForecast: boolean;
}

export interface BusinessMonthlyAnalysis {
  history: BusinessMonthlyPoint[];
  forecast: BusinessMonthlyPoint[];
  averageMonthlyGrowthPercent: number;
  averageMonthlyDelivered: number;
  averageMonthlyRevenue: number;
  averageMonthlyProfit: number;
  bestMonth?: string | null;
  bestMonthProfit: number;
  trendSlopePerMonth: number;
  totalRevenue: number;
  totalProfit: number;
  closedMonths: number;
}

export interface BusinessOverview {
  days: number;
  settings: BusinessFinanceSettings;
  fillsDelivered: number;
  fillsProduced: number;
  revenue: number;
  gasCost: number;
  grossProfit: number;
  marginPercent: number;
  sourceCylindersConsumed: number;
  prevFillsDelivered: number;
  prevFillsProduced: number;
  prevRevenue: number;
  prevGrossProfit: number;
  revenueChangePercent: number;
  profitChangePercent: number;
  fillsChangePercent: number;
  dailySeries: BusinessDailyPoint[];
  averageDailyFills: number;
  forecastDays: number;
  forecastFills: number;
  forecastRevenue: number;
  forecastGasCost: number;
  forecastProfit: number;
  forecastSourceCylinders: number;
  daysUntilNextSourceCylinder: number;
  pipelineReadyCount: number;
  pipelineValue: number;
  unpaidCompletedOrders: number;
  problemCylinders: number;
  topCustomers: BusinessTopCustomer[];
  weekdayStats: BusinessWeekdayStat[];
  monthly: BusinessMonthlyAnalysis;
}

export const businessApi = {
  getOverview: (days = 30) =>
    apiRequest<BusinessOverview>(`/api/business/overview?days=${days}`),

  getSettings: () =>
    apiRequest<BusinessFinanceSettings>('/api/business/settings'),

  updateSettings: (data: {
    refillPriceEur: number;
    sourceCylinderCostEur: number;
    sourceCylinderGasKg: number;
    consumerCylinderGasG: number;
  }) =>
    apiRequest<BusinessFinanceSettings>('/api/business/settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
};

export const settingsApi = {
  get: () => apiRequest<AppSettings>('/api/settings'),

  update: (data: AppSettings) =>
    apiRequest<AppSettings>('/api/settings', {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
};

export interface DebugCustomerSnapshot {
  exportedAt?: string;
  customer?: {
    customerId: string;
    name: string;
    phone: string;
    phoneType?: string;
    createdAt: string;
  };
  customers?: Array<{
    customerId: string;
    name: string;
    phone: string;
    phoneType?: string;
    createdAt: string;
  }>;
  orders: Array<{
    orderId: string;
    customerId: string;
    status: string;
    createdAt: string;
    completedAt?: string;
    notifiedAt?: string;
    shippedAt?: string;
    cancelledAt?: string;
    cancellationNotes?: string;
  }>;
  cylinderRefs: Array<{
    orderId: string;
    cylinderId: string;
    state: string;
  }>;
  cylinders: Array<{
    cylinderId: string;
    sequentialNumber: number;
    labelToken?: string;
    state: string;
    occurrenceNotes?: string;
    createdAt: string;
  }>;
  cylinderHistory: Array<{
    id: string;
    cylinderId: string;
    eventType: string;
    details?: string;
    orderId?: string;
    timestamp: string;
  }>;
  printJobs?: Array<{
    printJobId: string;
    storeId: string;
    quantity: number;
    templateId?: string;
    status: string;
    errorMessage?: string;
    createdAt: string;
    completedAt?: string;
  }>;
  appSettings?: Array<{
    appSettingsId: string;
    storeName: string;
    storePhone: string;
    storeLink: string;
    appTitle: string;
    whatsAppMessageTemplate: string;
    welcomeMessageTemplate: string;
    thankYouMessageTemplate: string;
    printerType: string;
    labelWidthMm: number;
    labelHeightMm: number;
    debugEnabled: boolean;
    soundNotificationsDisabled: boolean;
    updatedAt: string;
  }>;
  omittedTables: string[];
}

export const debugApi = {
  getFullSnapshot: () =>
    apiRequest<DebugCustomerSnapshot>('/api/debug/all'),

  getCustomerSnapshot: (customerId: string) =>
    apiRequest<DebugCustomerSnapshot>(`/api/debug/customer/${customerId}`),

  deleteCustomer: (customerId: string) =>
    apiRequest<void>(`/api/debug/customers/${customerId}`, { method: 'DELETE' }),

  deleteOrder: (orderId: string) =>
    apiRequest<void>(`/api/debug/orders/${orderId}`, { method: 'DELETE' }),

  deleteCylinderRef: (orderId: string, cylinderId: string) =>
    apiRequest<void>(`/api/debug/cylinder-refs/${orderId}/${cylinderId}`, { method: 'DELETE' }),

  deleteCylinder: (cylinderId: string) =>
    apiRequest<void>(`/api/debug/cylinders/${cylinderId}`, { method: 'DELETE' }),

  deleteCylinderHistory: (historyId: string) =>
    apiRequest<void>(`/api/debug/cylinder-history/${historyId}`, { method: 'DELETE' }),

  exportDatabase: async () => {
    const response = await fetch(`${getApiBaseUrl()}/api/debug/export`, {
      headers: withApiKey(),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw new Error(error.error || `HTTP ${response.status}`);
    }

    return response.json() as Promise<DebugCustomerSnapshot>;
  },

  importDatabase: (snapshot: DebugCustomerSnapshot) =>
    apiRequest<{ importedAt: string; customers: number; orders: number; cylinders: number }>('/api/debug/import', {
      method: 'POST',
      body: JSON.stringify(snapshot),
    }),
};
