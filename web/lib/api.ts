const BROWSER_API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE ||
  process.env.NEXT_PUBLIC_API_URL ||
  'http://localhost:8080';

const SERVER_API_BASE_URL =
  process.env.INTERNAL_API_BASE ||
  BROWSER_API_BASE_URL;

function getApiBaseUrl(): string {
  return typeof window === 'undefined' ? SERVER_API_BASE_URL : BROWSER_API_BASE_URL;
}

export async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await fetch(`${getApiBaseUrl()}${endpoint}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(error.error || `HTTP ${response.status}`);
  }

  return response.json();
}

// Customers
export const customersApi = {
  create: (data: { name: string; phone: string }) =>
    apiRequest<{ customerId: string; name: string; phone: string }>('/api/customers', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  search: (query?: string) =>
    apiRequest<{ customers: Array<{ customerId: string; name: string; phone: string }> }>(
      `/api/customers${query ? `?query=${encodeURIComponent(query)}` : ''}`
    ),

  getCylinders: (customerId: string) =>
    apiRequest<CustomerCylindersResult>(`/api/customers/${customerId}/cylinders`),
};

export interface CustomerCylinderHistoryItem {
  eventType: string;
  details?: string;
  timestamp: string;
}

export interface CustomerCylinder {
  cylinderId: string;
  labelToken?: string;
  state: string;
  createdAt: string;
  orderId: string;
  orderStatus: string;
  history: CustomerCylinderHistoryItem[];
}

export interface CustomerCylindersResult {
  customerId: string;
  name: string;
  phone: string;
  cylinders: CustomerCylinder[];
}

// Orders
export const ordersApi = {
  create: (customerId: string) =>
    apiRequest<{
      orderId: string;
      customerId: string;
      status: string;
      createdAt: string;
      cylinderCount: number;
    }>('/api/orders', {
      method: 'POST',
      body: JSON.stringify({ customerId }),
    }),

  addCylinder: (orderId: string, cylinderId?: string) =>
    apiRequest<{
      cylinderId: string;
      labelToken?: string;
      state: string;
    }>(`/api/orders/${orderId}/cylinders`, {
      method: 'POST',
      body: JSON.stringify({ cylinderId }),
    }),

  scanCylinder: (orderId: string, qrToken: string) =>
    apiRequest<{
      cylinderId: string;
      labelToken?: string;
      state: string;
    }>(`/api/orders/${orderId}/cylinders/scan`, {
      method: 'POST',
      body: JSON.stringify({ qrToken }),
    }),
};

// Cylinders / Filling
export interface FillingQueueItem {
  cylinderId: string;
  labelToken?: string;
  state: string;
  receivedAt: string;
  orderId: string;
  customerName: string;
  customerPhone: string;
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
}

export interface ReportProblemResult {
  cylinderId: string;
  state: string;
  problemType: string;
  notes: string;
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

  reportProblem: (cylinderId: string, type: string, notes: string) =>
    apiRequest<ReportProblemResult>(`/api/cylinders/${cylinderId}/report-problem`, {
      method: 'POST',
      body: JSON.stringify({ type, notes }),
    }),

  assignLabel: (cylinderId: string, qrToken: string) =>
    apiRequest<AssignLabelResult>(`/api/cylinders/${cylinderId}/assign-label`, {
      method: 'POST',
      body: JSON.stringify({ qrToken }),
    }),
};

// Pickup
export interface PickupCylinder {
  cylinderId: string;
  labelToken?: string;
  state: string;
  isDelivered: boolean;
}

export interface PickupOrder {
  orderId: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  status: string;
  createdAt: string;
  notifiedAt?: string;
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
};

// WhatsApp helper
export const generateWhatsAppLink = (phone: string, message: string): string => {
  // Remove non-digits and ensure proper format
  const cleanPhone = phone.replace(/\D/g, '');
  const encodedMessage = encodeURIComponent(message);
  return `https://wa.me/${cleanPhone}?text=${encodedMessage}`;
};

// Print Jobs
export interface PrintJob {
  printJobId: string;
  status: string;
  quantity: number;
  createdAt: string;
}

export const printJobsApi = {
  create: (
    quantity: number, 
    customerName?: string,
    customerPhone?: string,
    storeId?: string, 
    templateId?: string
  ) =>
    apiRequest<PrintJob>('/api/printjobs', {
      method: 'POST',
      body: JSON.stringify({
        storeId: storeId || '00000000-0000-0000-0000-000000000001',
        quantity,
        templateId: templateId || null,
        customerName: customerName || null,
        customerPhone: customerPhone || null,
      }),
    }),
};

// Cylinder History
export interface CylinderHistoryItem {
  eventType: string;
  details?: string;
  orderId?: string;
  timestamp: string;
}

export interface CylinderHistory {
  cylinderId: string;
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
  ordersCompletedToday: number;
  ordersCompletedThisWeek: number;
  ordersAwaitingNotification: number;
  cylindersReceived: number;
  cylindersReady: number;
  cylindersWithProblem: number;
  cylindersFilledToday: number;
  cylindersFilledThisWeek: number;
  totalCustomers: number;
}

export const reportsApi = {
  getStats: () => apiRequest<DashboardStats>('/api/reports/stats'),
};
