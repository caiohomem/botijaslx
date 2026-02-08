'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { customersApi, historyApi, reportsApi, CylinderHistory, DashboardStats, CustomerCylindersResult, CustomerCylinder } from '@/lib/api';
import { QrScanner } from '@/components/QrScanner';

type LookupMode = 'customer' | 'cylinder';

export default function DashboardPage() {
  const t = useTranslations();
  const router = useRouter();
  const [lookupMode, setLookupMode] = useState<LookupMode>('customer');
  const [searchInput, setSearchInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Customer lookup state
  const [customerResults, setCustomerResults] = useState<Array<{ customerId: string; name: string; phone: string }>>([]);
  const [selectedCustomerCylinders, setSelectedCustomerCylinders] = useState<CustomerCylindersResult | null>(null);
  const [expandedCylinder, setExpandedCylinder] = useState<string | null>(null);

  // Cylinder lookup state
  const [cylinderHistory, setCylinderHistory] = useState<CylinderHistory | null>(null);

  // Stats
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [statsLoading, setStatsLoading] = useState(true);

  useEffect(() => {
    loadStats();
  }, []);

  const loadStats = async () => {
    try {
      const data = await reportsApi.getStats();
      setStats(data);
    } catch (err) {
      console.error('Failed to load stats:', err);
    } finally {
      setStatsLoading(false);
    }
  };

  const handleSearchValue = async (value?: string) => {
    const query = (value ?? searchInput).trim();
    if (!query) return;

    setSearchInput(query);
    setLoading(true);
    setError(null);
    setCustomerResults([]);
    setSelectedCustomerCylinders(null);
    setCylinderHistory(null);
    setExpandedCylinder(null);

    try {
      if (lookupMode === 'customer') {
        const result = await customersApi.search(query);
        if (result.customers.length === 0) {
          setError(t('customer.notFound'));
        } else {
          setCustomerResults(result.customers);
        }
      } else {
        const result = await historyApi.scanCylinder(query);
        setCylinderHistory(result);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : lookupMode === 'customer' ? t('customer.notFound') : t('dashboard.cylinderNotFound'));
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => handleSearchValue();

  const handleSelectCustomer = async (customerId: string) => {
    setLoading(true);
    setError(null);

    try {
      const result = await customersApi.getCylinders(customerId);
      setSelectedCustomerCylinders(result);
      setCustomerResults([]);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar botijas do cliente');
    } finally {
      setLoading(false);
    }
  };

  const handleClear = () => {
    setSearchInput('');
    setCustomerResults([]);
    setSelectedCustomerCylinders(null);
    setCylinderHistory(null);
    setExpandedCylinder(null);
    setError(null);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  const getEventIcon = (eventType: string) => {
    switch (eventType) {
      case 'Received': return '📥';
      case 'LabelAssigned': return '🏷️';
      case 'MarkedReady': return '✅';
      case 'Delivered': return '📦';
      case 'ProblemReported': return '⚠️';
      default: return '•';
    }
  };

  const getStateColor = (state: string) => {
    switch (state) {
      case 'Received': return 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300';
      case 'Ready': return 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300';
      case 'Delivered': return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300';
      case 'Problem': return 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300';
      default: return 'bg-gray-100 text-gray-700';
    }
  };

  const hasResults = customerResults.length > 0 || selectedCustomerCylinders || cylinderHistory;

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>

      {/* Stats Cards */}
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard
            label={t('dashboard.stats.ordersOpen')}
            value={stats.ordersOpen}
            color="blue"
            onClick={() => router.push('/filling')}
          />
          <StatCard
            label={t('dashboard.stats.ordersReady')}
            value={stats.ordersReadyForPickup}
            color="green"
            onClick={() => router.push('/pickup')}
            highlight={stats.ordersAwaitingNotification > 0}
            sublabel={stats.ordersAwaitingNotification > 0
              ? t('dashboard.stats.awaitingNotification', { count: stats.ordersAwaitingNotification })
              : undefined}
          />
          <StatCard
            label={t('dashboard.stats.cylindersPending')}
            value={stats.cylindersReceived}
            color="amber"
            onClick={() => router.push('/filling')}
          />
          <StatCard
            label={t('dashboard.stats.cylindersReady')}
            value={stats.cylindersReady}
            color="green"
            onClick={() => router.push('/pickup')}
          />
          <StatCard
            label={t('dashboard.stats.filledToday')}
            value={stats.cylindersFilledToday}
            color="purple"
          />
          <StatCard
            label={t('dashboard.stats.filledThisWeek')}
            value={stats.cylindersFilledThisWeek}
            color="purple"
          />
          <StatCard
            label={t('dashboard.stats.completedToday')}
            value={stats.ordersCompletedToday}
            color="gray"
          />
          {stats.cylindersWithProblem > 0 && (
            <StatCard
              label={t('dashboard.stats.withProblem')}
              value={stats.cylindersWithProblem}
              color="red"
              highlight
            />
          )}
        </div>
      )}

      {statsLoading && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="p-4 border rounded-lg animate-pulse bg-muted/50 h-20" />
          ))}
        </div>
      )}

      {/* Lookup Section */}
      <div className="p-4 border rounded-lg bg-muted/30 space-y-4">
        {/* Mode Tabs */}
        <div className="flex gap-2">
          <button
            onClick={() => { setLookupMode('customer'); handleClear(); }}
            className={`px-4 py-2 rounded-lg font-medium ${
              lookupMode === 'customer'
                ? 'bg-primary text-primary-foreground'
                : 'border hover:bg-accent'
            }`}
          >
            {t('dashboard.searchByCustomer')}
          </button>
          <button
            onClick={() => { setLookupMode('cylinder'); handleClear(); }}
            className={`px-4 py-2 rounded-lg font-medium ${
              lookupMode === 'cylinder'
                ? 'bg-primary text-primary-foreground'
                : 'border hover:bg-accent'
            }`}
          >
            {t('dashboard.searchByCylinder')}
          </button>
        </div>

        {/* Camera Scanner (cylinder mode only) */}
        {lookupMode === 'cylinder' && (
          <QrScanner
            onScan={(code) => handleSearchValue(code)}
            label={t('dashboard.scanCamera')}
          />
        )}

        {/* Search Input */}
        <div className="flex gap-2">
          <input
            type="text"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
            placeholder={lookupMode === 'customer' ? t('dashboard.customerSearchPlaceholder') : t('dashboard.scanPlaceholder')}
            className="flex-1 px-4 py-3 border rounded-lg bg-background text-foreground text-lg"
          />
          <button
            onClick={handleSearch}
            disabled={!searchInput.trim() || loading}
            className="px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50 font-medium"
          >
            {loading ? '...' : t('dashboard.search')}
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg">
          {error}
        </div>
      )}

      {/* Customer Search Results */}
      {customerResults.length > 0 && (
        <div className="space-y-2">
          <h3 className="font-semibold">{t('dashboard.selectCustomer')}</h3>
          {customerResults.map((customer) => (
            <button
              key={customer.customerId}
              onClick={() => handleSelectCustomer(customer.customerId)}
              className="w-full p-4 border rounded-lg hover:bg-accent text-left"
            >
              <div className="font-semibold">{customer.name}</div>
              <div className="text-sm text-muted-foreground">{customer.phone}</div>
            </button>
          ))}
        </div>
      )}

      {/* Customer Cylinders View */}
      {selectedCustomerCylinders && (
        <div className="space-y-4">
          {/* Customer Header */}
          <div className="p-4 border rounded-lg bg-muted/30">
            <div className="font-semibold text-lg">{selectedCustomerCylinders.name}</div>
            <div className="text-sm text-muted-foreground">{selectedCustomerCylinders.phone}</div>
            <div className="text-sm text-muted-foreground mt-1">
              {selectedCustomerCylinders.cylinders.length} {t('order.cylinders')}
            </div>
          </div>

          {/* Cylinders List */}
          {selectedCustomerCylinders.cylinders.length === 0 ? (
            <div className="text-center text-muted-foreground py-8 border-2 border-dashed rounded-lg">
              {t('dashboard.noCylindersForCustomer')}
            </div>
          ) : (
            <div className="space-y-2">
              {selectedCustomerCylinders.cylinders.map((cylinder) => (
                <div key={cylinder.cylinderId} className="border rounded-lg overflow-hidden">
                  {/* Cylinder Header */}
                  <button
                    onClick={() => setExpandedCylinder(
                      expandedCylinder === cylinder.cylinderId ? null : cylinder.cylinderId
                    )}
                    className="w-full p-4 flex items-center justify-between hover:bg-muted/30 text-left"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm">
                        {cylinder.labelToken?.slice(0, 4) || '?'}
                      </div>
                      <div>
                        <div className="font-mono text-sm">
                          {cylinder.labelToken || cylinder.cylinderId.slice(0, 8)}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {t(`order.status.${cylinder.orderStatus.toLowerCase()}`)}
                        </div>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStateColor(cylinder.state)}`}>
                        {t(`cylinder.status.${cylinder.state.toLowerCase()}`)}
                      </span>
                      <span className="text-lg">{expandedCylinder === cylinder.cylinderId ? '▼' : '▶'}</span>
                    </div>
                  </button>

                  {/* Cylinder History (expandable) */}
                  {expandedCylinder === cylinder.cylinderId && (
                    <CylinderTimeline
                      history={cylinder.history}
                      formatDate={formatDate}
                      getEventIcon={getEventIcon}
                      t={t}
                    />
                  )}
                </div>
              ))}
            </div>
          )}

          <button
            onClick={handleClear}
            className="px-4 py-2 border rounded-lg hover:bg-accent"
          >
            {t('dashboard.clear')}
          </button>
        </div>
      )}

      {/* Cylinder History View */}
      {cylinderHistory && (
        <div className="space-y-4">
          {/* Header Card */}
          <div className="p-4 border rounded-lg">
            <div className="flex justify-between items-start mb-4">
              <div>
                <div className="text-sm text-muted-foreground">{t('dashboard.cylinder')}</div>
                <div className="font-mono text-lg">
                  {cylinderHistory.labelToken || cylinderHistory.cylinderId.slice(0, 8)}
                </div>
              </div>
              <span className={`px-3 py-1 rounded-full text-sm font-medium ${getStateColor(cylinderHistory.state)}`}>
                {t(`cylinder.status.${cylinderHistory.state.toLowerCase()}`)}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <div className="text-muted-foreground">{t('dashboard.createdAt')}</div>
                <div>{formatDate(cylinderHistory.createdAt)}</div>
              </div>
              {cylinderHistory.customerName && (
                <div>
                  <div className="text-muted-foreground">{t('customer.title')}</div>
                  <div>{cylinderHistory.customerName}</div>
                  <div className="text-xs text-muted-foreground">{cylinderHistory.customerPhone}</div>
                </div>
              )}
              {cylinderHistory.currentOrderStatus && (
                <div>
                  <div className="text-muted-foreground">{t('order.title')}</div>
                  <div>{t(`order.status.${cylinderHistory.currentOrderStatus.toLowerCase()}`)}</div>
                </div>
              )}
            </div>
          </div>

          {/* History Timeline */}
          <div className="p-4 border rounded-lg">
            <h3 className="font-semibold mb-4">{t('dashboard.history')}</h3>
            <CylinderTimeline
              history={cylinderHistory.history}
              formatDate={formatDate}
              getEventIcon={getEventIcon}
              t={t}
              inline
            />
          </div>

          <button
            onClick={handleClear}
            className="px-4 py-2 border rounded-lg hover:bg-accent"
          >
            {t('dashboard.clear')}
          </button>
        </div>
      )}

      {/* Empty State */}
      {!hasResults && !loading && !error && (
        <div className="text-center text-muted-foreground py-12 border-2 border-dashed rounded-lg">
          <div className="text-4xl mb-2">🔍</div>
          <div>{t('dashboard.scanToStart')}</div>
        </div>
      )}
    </div>
  );
}

// Timeline component for cylinder history
function CylinderTimeline({
  history,
  formatDate,
  getEventIcon,
  t,
  inline = false,
}: {
  history: Array<{ eventType: string; details?: string; timestamp: string }>;
  formatDate: (d: string) => string;
  getEventIcon: (e: string) => string;
  t: (key: string) => string;
  inline?: boolean;
}) {
  if (history.length === 0) {
    return (
      <div className={`text-center text-muted-foreground py-4 ${!inline ? 'px-4 pb-4' : ''}`}>
        {t('dashboard.noHistory')}
      </div>
    );
  }

  return (
    <div className={`space-y-3 ${!inline ? 'px-4 pb-4 border-t bg-muted/20' : ''}`}>
      {!inline ? null : null}
      {history.map((item, index) => (
        <div key={index} className={`flex gap-3 ${!inline && index === 0 ? 'pt-4' : ''}`}>
          <div className="text-xl">{getEventIcon(item.eventType)}</div>
          <div className="flex-1">
            <div className="font-medium">
              {t(`dashboard.events.${item.eventType}`)}
            </div>
            {item.details && (
              <div className="text-sm text-muted-foreground">
                {item.details}
              </div>
            )}
            <div className="text-xs text-muted-foreground">
              {formatDate(item.timestamp)}
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// Stat Card Component
function StatCard({
  label,
  value,
  color,
  highlight = false,
  sublabel,
  onClick,
}: {
  label: string;
  value: number;
  color: 'blue' | 'green' | 'amber' | 'red' | 'purple' | 'gray';
  highlight?: boolean;
  sublabel?: string;
  onClick?: () => void;
}) {
  const colorClasses = {
    blue: 'border-blue-200 dark:border-blue-800',
    green: 'border-green-200 dark:border-green-800',
    amber: 'border-amber-200 dark:border-amber-800',
    red: 'border-red-200 dark:border-red-800',
    purple: 'border-purple-200 dark:border-purple-800',
    gray: 'border-gray-200 dark:border-gray-700',
  };

  const valueClasses = {
    blue: 'text-blue-600 dark:text-blue-400',
    green: 'text-green-600 dark:text-green-400',
    amber: 'text-amber-600 dark:text-amber-400',
    red: 'text-red-600 dark:text-red-400',
    purple: 'text-purple-600 dark:text-purple-400',
    gray: 'text-gray-600 dark:text-gray-400',
  };

  const Component = onClick ? 'button' : 'div';

  return (
    <Component
      className={`p-4 border-2 rounded-lg text-left ${colorClasses[color]} ${highlight ? 'bg-opacity-10 animate-pulse' : ''} ${onClick ? 'cursor-pointer hover:bg-muted/50 transition-colors' : ''}`}
      onClick={onClick}
    >
      <div className={`text-2xl font-bold ${valueClasses[color]}`}>{value}</div>
      <div className="text-sm text-muted-foreground">{label}</div>
      {sublabel && (
        <div className="text-xs text-amber-600 dark:text-amber-400 mt-1">{sublabel}</div>
      )}
    </Component>
  );
}
