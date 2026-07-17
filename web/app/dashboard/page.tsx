'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { customersApi, historyApi, reportsApi, cylindersApi, ordersApi, CylinderHistory, DashboardStats, CustomerCylindersResult, CustomerOrder, ProblemCylinder } from '@/lib/api';
import { QrScanner } from '@/components/QrScanner';
import { CustomerSearch } from '@/components/CustomerSearch';

type LookupMode = 'customer' | 'cylinder' | 'problems';
type ChartRange = number;

export default function DashboardPage() {
  const t = useTranslations();
  const router = useRouter();
  const [chartRange, setChartRange] = useState<ChartRange>(7);
  const [lookupMode, setLookupMode] = useState<LookupMode>('customer');
  const [searchInput, setSearchInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [problemLoading, setProblemLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Customer lookup state
  const [customerResults, setCustomerResults] = useState<Array<{ customerId: string; name: string; phone: string }>>([]);
  const [selectedCustomerCylinders, setSelectedCustomerCylinders] = useState<CustomerCylindersResult | null>(null);
  const [expandedCylinder, setExpandedCylinder] = useState<string | null>(null);
  const [undoModal, setUndoModal] = useState<{ cylinderId: string; historyEntryId: string; eventType: string } | null>(null);
  const [undoComment, setUndoComment] = useState('');
  const [undoLoading, setUndoLoading] = useState(false);
  const [cancelModal, setCancelModal] = useState<{ orderId: string } | null>(null);
  const [cancelNotes, setCancelNotes] = useState('');
  const [cancelLoading, setCancelLoading] = useState(false);

  // Cylinder lookup state
  const [cylinderHistory, setCylinderHistory] = useState<CylinderHistory | null>(null);
  const [problemCylinders, setProblemCylinders] = useState<ProblemCylinder[]>([]);

  // Stats
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [statsLoading, setStatsLoading] = useState(true);

  useEffect(() => {
    loadStats(chartRange);
  }, [chartRange]);

  const loadStats = async (days: ChartRange) => {
    setStatsLoading(true);
    try {
      const data = await reportsApi.getStats(days);
      setStats(data);
    } catch (err) {
      console.error('Failed to load stats:', err);
    } finally {
      setStatsLoading(false);
    }
  };

  const loadProblemCylinders = async () => {
    setProblemLoading(true);
    setError(null);

    try {
      const result = await cylindersApi.getProblems();
      setProblemCylinders(result.cylinders);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar botijas com problema');
      setProblemCylinders([]);
    } finally {
      setProblemLoading(false);
    }
  };

  const resetLookupState = () => {
    setSearchInput('');
    setCustomerResults([]);
    setSelectedCustomerCylinders(null);
    setCylinderHistory(null);
    setExpandedCylinder(null);
    setError(null);
  };

  const handleSelectLookupMode = (mode: LookupMode) => {
    resetLookupState();
    setLookupMode(mode);

    if (mode === 'problems') {
      void loadProblemCylinders();
    } else {
      setProblemCylinders([]);
      setProblemLoading(false);
    }
  };

  const handleSearchValue = async (value?: string) => {
    const query = (value ?? searchInput).trim();
    if (!query) return;

    setSearchInput(query);
    setLoading(true);
    setError(null);
    setProblemCylinders([]);
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
      setSearchInput('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar botijas do cliente');
    } finally {
      setLoading(false);
    }
  };

  // Handle QR scan in customer mode to identify customer from cylinder
  const handleCustomerModeScan = async (qrCode: string) => {
    setLoading(true);
    setError(null);

    try {
      // First, get the cylinder to find the customer
      const cylinderResult = await historyApi.scanCylinder(qrCode);

      // If cylinder has a customer associated, load that customer's cylinders
      if (cylinderResult.customerName && cylinderResult.customerPhone) {
        // Search for the customer by name
        const customers = await customersApi.search(cylinderResult.customerName);
        if (customers.customers.length > 0) {
          // Select the first matching customer
          await handleSelectCustomer(customers.customers[0].customerId);
        } else {
          setError(t('customer.notFound'));
        }
      } else {
        setError(t('dashboard.cylinderNotFound'));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t('customer.notFound'));
    } finally {
      setLoading(false);
    }
  };

  const handleClear = () => {
    setLookupMode('customer');
    resetLookupState();
    setProblemCylinders([]);
    setProblemLoading(false);
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
      case 'ActionUndone': return '↩️';
      case 'OrderCancelled': return '✕';
      default: return '•';
    }
  };

  const refreshCurrentView = async () => {
    if (cylinderHistory) {
      const refreshed = await historyApi.getByCylinderId(cylinderHistory.cylinderId);
      setCylinderHistory(refreshed);
      return;
    }

    if (selectedCustomerCylinders) {
      const refreshed = await customersApi.getCylinders(selectedCustomerCylinders.customerId);
      setSelectedCustomerCylinders(refreshed);
    }
  };

  const handleUndoAction = async () => {
    if (!undoModal || !undoComment.trim()) return;

    setUndoLoading(true);
    setError(null);

    try {
      await cylindersApi.undoHistoryAction(undoModal.cylinderId, undoModal.historyEntryId, undoComment.trim());
      await refreshCurrentView();
      setUndoModal(null);
      setUndoComment('');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('dashboard.undoFailed'));
    } finally {
      setUndoLoading(false);
    }
  };

  const handleCancelOrder = async () => {
    if (!cancelModal || !cancelNotes.trim()) return;

    setCancelLoading(true);
    setError(null);

    try {
      await ordersApi.cancel(cancelModal.orderId, cancelNotes.trim());
      if (selectedCustomerCylinders) {
        const refreshed = await customersApi.getCylinders(selectedCustomerCylinders.customerId);
        setSelectedCustomerCylinders(refreshed);
      }
      setCancelModal(null);
      setCancelNotes('');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('dashboard.cancelOrderFailed'));
    } finally {
      setCancelLoading(false);
    }
  };

  const getStateColor = (state: string) => {
    switch (state) {
      case 'Received': return 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300';
      case 'Ready': return 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300';
      case 'Delivered': return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300';
      case 'Problem': return 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300';
      case 'Cancelled': return 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300';
      default: return 'bg-gray-100 text-gray-700';
    }
  };

  const hasResults = customerResults.length > 0 || selectedCustomerCylinders || cylinderHistory || problemLoading || lookupMode === 'problems';

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('dashboard.title')}</h1>

      {/* Stats Cards */}
      {stats && (
        <div className="space-y-6">
          <DashboardSection title={t('dashboard.sections.now')}>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard
                label={t('dashboard.stats.cylindersPending')}
                value={stats.cylindersReceived}
                color="amber"
                onClick={() => router.push('/filling')}
                sublabel={t('dashboard.stats.stillFilling')}
              />
              <StatCard
                label={t('dashboard.stats.ordersReadyForPickup')}
                value={stats.ordersReadyForPickup}
                color="green"
                onClick={() => router.push('/pickup?filter=pickup')}
                highlight={(stats.ordersAwaitingNotificationPickup ?? 0) > 0}
                sublabel={(stats.ordersAwaitingNotificationPickup ?? 0) > 0
                  ? t('dashboard.stats.awaitingNotification', { count: stats.ordersAwaitingNotificationPickup })
                  : t('dashboard.stats.readyForStorePickup')}
              />
              <StatCard
                label={t('dashboard.stats.ordersReadyForShipping')}
                value={stats.ordersReadyForShipping ?? 0}
                color="blue"
                onClick={() => router.push('/pickup?filter=shipping')}
                highlight={(stats.ordersAwaitingNotificationShipping ?? 0) > 0}
                sublabel={(stats.ordersAwaitingNotificationShipping ?? 0) > 0
                  ? t('dashboard.stats.awaitingNotification', { count: stats.ordersAwaitingNotificationShipping })
                  : t('dashboard.stats.readyForShipping')}
              />
              <StatCard
                label={t('dashboard.stats.ordersOpen')}
                value={stats.ordersOpen}
                color="gray"
                onClick={() => router.push('/filling')}
                sublabel={t('dashboard.stats.stillFilling')}
              />
            </div>
          </DashboardSection>

          <DashboardSection title={t('dashboard.sections.today')}>
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard
                label={t('dashboard.stats.filledToday')}
                value={stats.cylindersFilledToday}
                color="purple"
              />
              <StatCard
                label={t('dashboard.stats.completedToday')}
                value={stats.ordersCompletedToday}
                color="gray"
              />
              <StatCard
                label={t('dashboard.stats.filledThisWeek')}
                value={stats.cylindersFilledThisWeek}
                color="purple"
                sublabel={t('dashboard.stats.last7Days')}
              />
              {stats.cylindersWithProblem > 0 ? (
                <StatCard
                  label={t('dashboard.stats.withProblem')}
                  value={stats.cylindersWithProblem}
                  color="red"
                  highlight
                  onClick={() => handleSelectLookupMode('problems')}
                />
              ) : (
                <StatCard
                  label={t('dashboard.stats.withProblem')}
                  value={0}
                  color="gray"
                  sublabel={t('dashboard.stats.noIssues')}
                />
              )}
            </div>
          </DashboardSection>

          <DashboardSection
            title={t('dashboard.sections.activity')}
            actions={
              <ChartRangeSelector
                value={chartRange}
                onChange={(value) => setChartRange(value)}
                t={t}
              />
            }
          >
            <DailyActivityChart points={stats.dailySeries} range={chartRange} t={t} />
          </DashboardSection>
        </div>
      )}

      {statsLoading && (
        <div className="space-y-6">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="p-4 border rounded-lg animate-pulse bg-muted/50 h-36" />
          ))}
        </div>
      )}

      <DashboardSection title={t('dashboard.sections.history')}>
        <div className="p-4 border rounded-lg bg-muted/30 space-y-4">
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => handleSelectLookupMode('customer')}
              className={`px-4 py-2 rounded-lg font-medium ${
                lookupMode === 'customer'
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t('dashboard.searchByCustomer')}
            </button>
            <button
              onClick={() => handleSelectLookupMode('cylinder')}
              className={`px-4 py-2 rounded-lg font-medium ${
                lookupMode === 'cylinder'
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t('dashboard.searchByCylinder')}
            </button>
            <button
              onClick={() => handleSelectLookupMode('problems')}
              className={`px-4 py-2 rounded-lg font-medium ${
                lookupMode === 'problems'
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t('dashboard.problemsTab')}
            </button>
          </div>

          {lookupMode === 'customer' && (
            <>
              <QrScanner
                onScan={(code) => handleCustomerModeScan(code)}
                label={t('dashboard.scanCamera')}
              />

              <CustomerSearch
                onSelect={(customer) => handleSelectCustomer(customer.customerId)}
                onCreateNew={() => {}}
                disabled={loading}
              />
            </>
          )}

          {lookupMode === 'cylinder' && (
            <>
              <QrScanner
                onScan={(code) => handleSearchValue(code)}
                label={t('dashboard.scanCamera')}
              />
              <div className="flex gap-2">
                <input
                  type="text"
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                  placeholder={t('dashboard.scanPlaceholder')}
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
            </>
          )}

          {lookupMode === 'problems' && (
            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <div className="text-sm text-muted-foreground">
                  {t('dashboard.problems.description')}
                </div>
                <button
                  onClick={() => void loadProblemCylinders()}
                  disabled={problemLoading}
                  className="px-3 py-2 border rounded-lg hover:bg-accent disabled:opacity-50 text-sm font-medium"
                >
                  {problemLoading ? t('common.loading') : t('pickup.refresh')}
                </button>
              </div>

              {problemLoading && problemCylinders.length === 0 ? (
                <div className="flex items-center justify-center gap-2 py-8 text-muted-foreground">
                  <span className="inline-block animate-spin">⟳</span>
                  {t('common.loading')}
                </div>
              ) : problemCylinders.length === 0 ? (
                <div className="text-center text-muted-foreground py-12 border-2 border-dashed rounded-lg">
                  <div className="text-4xl mb-2">⚠️</div>
                  <div>{t('dashboard.problems.empty')}</div>
                </div>
              ) : (
                <div className="space-y-2">
                  {problemCylinders.map((cylinder) => (
                    <ProblemCylinderCard
                      key={cylinder.cylinderId}
                      cylinder={cylinder}
                      t={t}
                      formatDate={formatDate}
                      onViewHistory={async (cylinderId) => {
                        setLoading(true);
                        setError(null);
                        setLookupMode('cylinder');
                        setCustomerResults([]);
                        setSelectedCustomerCylinders(null);
                        setExpandedCylinder(null);
                        setSearchInput('');

                        try {
                          const result = await historyApi.getByCylinderId(cylinderId);
                          setCylinderHistory(result);
                        } catch (err) {
                          setError(err instanceof Error ? err.message : t('dashboard.cylinderNotFound'));
                        } finally {
                          setLoading(false);
                        }
                      }}
                    />
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </DashboardSection>

      {/* Error */}
      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg">
          {error}
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

          {/* Orders List */}
          {(selectedCustomerCylinders.orders?.length ?? 0) === 0 ? (
            <div className="text-center text-muted-foreground py-8 border-2 border-dashed rounded-lg">
              {t('dashboard.noCylindersForCustomer')}
            </div>
          ) : (
            <div className="space-y-3">
              {selectedCustomerCylinders.orders.map((order) => (
                <OrderHistoryCard
                  key={order.orderId}
                  order={order}
                  expandedCylinder={expandedCylinder}
                  setExpandedCylinder={setExpandedCylinder}
                  getStateColor={getStateColor}
                  formatDate={formatDate}
                  getEventIcon={getEventIcon}
                  t={t}
                  onUndoAction={(cylinderId, historyEntryId, eventType) => {
                    setUndoModal({ cylinderId, historyEntryId, eventType });
                    setUndoComment('');
                  }}
                  onCancelOrder={(orderId) => {
                    setCancelModal({ orderId });
                    setCancelNotes('');
                  }}
                />
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
                <div className="font-mono text-lg font-bold">
                  #{String(cylinderHistory.sequentialNumber).padStart(4, '0')}
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
              onUndoAction={(historyEntryId, eventType) => {
                setUndoModal({ cylinderId: cylinderHistory.cylinderId, historyEntryId, eventType });
                setUndoComment('');
              }}
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

      {undoModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-background rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('dashboard.undoTitle')}</h3>
            <p className="text-sm text-muted-foreground">
              {t('dashboard.undoDescription', { action: t(`dashboard.events.${undoModal.eventType}`) })}
            </p>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('dashboard.undoComment')}</label>
              <textarea
                value={undoComment}
                onChange={(e) => setUndoComment(e.target.value)}
                placeholder={t('dashboard.undoCommentPlaceholder')}
                className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
              />
            </div>
            <div className="flex gap-3 pt-2">
              <button
                onClick={() => {
                  setUndoModal(null);
                  setUndoComment('');
                }}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={handleUndoAction}
                disabled={!undoComment.trim() || undoLoading}
                className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50"
              >
                {undoLoading ? t('common.loading') : t('dashboard.deleteAction')}
              </button>
            </div>
          </div>
        </div>
      )}

      {cancelModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-background rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('dashboard.cancelOrderTitle')}</h3>
            <p className="text-sm text-muted-foreground">
              {t('dashboard.cancelOrderDescription')}
            </p>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('dashboard.cancelOrderNotes')}</label>
              <textarea
                value={cancelNotes}
                onChange={(e) => setCancelNotes(e.target.value)}
                placeholder={t('dashboard.cancelOrderNotesPlaceholder')}
                className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
              />
            </div>
            <div className="flex gap-3 pt-2">
              <button
                onClick={() => {
                  setCancelModal(null);
                  setCancelNotes('');
                }}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={handleCancelOrder}
                disabled={!cancelNotes.trim() || cancelLoading}
                className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50"
              >
                {cancelLoading ? t('common.loading') : t('dashboard.cancelOrder')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function DashboardSection({
  title,
  actions,
  children
}: {
  title: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          {title}
        </h2>
        {actions}
      </div>
      {children}
    </section>
  );
}

function ChartRangeSelector({
  value,
  onChange,
  t
}: {
  value: ChartRange;
  onChange: (value: ChartRange) => void;
  t: ReturnType<typeof useTranslations>;
}) {
  const presetOptions = [7, 30, 60, 90];
  const isCustom = !presetOptions.includes(value);

  return (
    <div className="flex flex-wrap items-center gap-1 rounded-lg border p-1 bg-muted/20">
      {presetOptions.map((option) => (
        <button
          key={option}
          onClick={() => onChange(option)}
          className={`px-2.5 py-1 text-xs rounded-md transition-colors ${
            value === option
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:bg-accent'
          }`}
        >
          {t(`dashboard.range.${option}d`)}
        </button>
      ))}
      <label className={`flex items-center gap-1 rounded-md px-2 py-1 text-xs ${
        isCustom ? 'bg-primary text-primary-foreground' : 'text-muted-foreground'
      }`}>
        <span>{t('dashboard.range.custom')}</span>
        <input
          type="number"
          min={1}
          max={365}
          value={value}
          onChange={(event) => {
            const nextValue = Number(event.target.value);
            if (Number.isFinite(nextValue)) {
              onChange(Math.min(365, Math.max(1, nextValue)));
            }
          }}
          className={`w-16 rounded border px-1 py-0.5 text-xs ${
            isCustom ? 'bg-primary text-primary-foreground border-primary-foreground/40' : 'bg-background text-foreground'
          }`}
        />
      </label>
    </div>
  );
}

function OrderHistoryCard({
  order,
  expandedCylinder,
  setExpandedCylinder,
  getStateColor,
  formatDate,
  getEventIcon,
  t,
  onUndoAction,
  onCancelOrder,
}: {
  order: CustomerOrder;
  expandedCylinder: string | null;
  setExpandedCylinder: (value: string | null) => void;
  getStateColor: (state: string) => string;
  formatDate: (value: string) => string;
  getEventIcon: (eventType: string) => string;
  t: ReturnType<typeof useTranslations>;
  onUndoAction: (cylinderId: string, historyEntryId: string, eventType: string) => void;
  onCancelOrder: (orderId: string) => void;
}) {
  const canCancel = order.status === 'Open' || order.status === 'ReadyForPickup';
  const durationText = formatDurationBetween(order.createdAt, order.completedAt ?? order.cancelledAt);

  return (
    <div className="border rounded-lg overflow-hidden bg-background">
      <div className="p-4 bg-muted/30 border-b space-y-3">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="font-semibold">
              {t('order.title')}
            </div>
            <div className="text-xs text-muted-foreground">
              {t('pickup.createdAt')}: {formatDate(order.createdAt)}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStateColor(order.status)}`}>
              {t(`order.status.${order.status.toLowerCase()}`)}
            </span>
            {canCancel && (
              <button
                onClick={() => onCancelOrder(order.orderId)}
                className="px-3 py-1.5 rounded-lg border border-red-300 text-xs font-medium text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-300 dark:hover:bg-red-950/30"
              >
                {t('dashboard.cancelOrder')}
              </button>
            )}
          </div>
        </div>

        <div className="grid gap-2 text-sm md:grid-cols-4">
          <OrderMeta label={t('dashboard.orderFulfillment')} value={order.fulfillmentMethod === 'Shipping' ? t('pickup.filters.shipping') : t('pickup.filters.pickup')} />
          <OrderMeta label={t('dashboard.orderShipping')} value={order.fulfillmentMethod === 'Shipping' ? (order.shippingPaid ? t('dashboard.paid') : t('dashboard.unpaid')) : t('dashboard.notApplicable')} />
          <OrderMeta label={t('dashboard.orderDeadline')} value={formatOrderDeadline(order.createdAt, order.status, t)} />
          <OrderMeta label={t('dashboard.orderDuration')} value={durationText ?? t('dashboard.inProgress')} />
        </div>

        {order.cancellationNotes && (
          <div className="rounded-lg bg-red-50 p-3 text-sm text-red-800 dark:bg-red-950/30 dark:text-red-200">
            {order.cancellationNotes}
          </div>
        )}
      </div>

      <div className="divide-y">
        {order.cylinders.map((cylinder) => {
          const expandedKey = `${order.orderId}:${cylinder.cylinderId}`;

          return (
            <div key={expandedKey}>
              <button
                onClick={() => setExpandedCylinder(expandedCylinder === expandedKey ? null : expandedKey)}
                className="w-full p-4 flex items-center justify-between hover:bg-muted/30 text-left"
              >
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm font-bold">
                    #{cylinder.sequentialNumber}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStateColor(cylinder.state)}`}>
                    {t(`cylinder.status.${cylinder.state.toLowerCase()}`)}
                  </span>
                  <span className="text-lg">{expandedCylinder === expandedKey ? '▼' : '▶'}</span>
                </div>
              </button>

              {expandedCylinder === expandedKey && (
                <CylinderTimeline
                  history={cylinder.history}
                  formatDate={formatDate}
                  getEventIcon={getEventIcon}
                  t={t}
                  onUndoAction={(historyEntryId, eventType) => {
                    onUndoAction(cylinder.cylinderId, historyEntryId, eventType);
                  }}
                />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function OrderMeta({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="font-medium">{value}</div>
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
  onUndoAction,
}: {
  history: Array<{ id: string; eventType: string; details?: string; timestamp: string }>;
  formatDate: (d: string) => string;
  getEventIcon: (e: string) => string;
  t: (key: string) => string;
  inline?: boolean;
  onUndoAction?: (historyEntryId: string, eventType: string) => void;
}) {
  if (history.length === 0) {
    return (
      <div className={`text-center text-muted-foreground py-4 ${!inline ? 'px-4 pb-4' : ''}`}>
        {t('dashboard.noHistory')}
      </div>
    );
  }

  const undoneIds = new Set(
    history
      .filter((item) => item.eventType === 'ActionUndone')
      .map((item) => extractUndoneHistoryEntryId(item.details))
      .filter((value): value is string => Boolean(value))
  );

  const latestUndoableActiveId = history.find((item) =>
    item.eventType !== 'ActionUndone' &&
    !undoneIds.has(item.id) &&
    ['Delivered', 'MarkedReady', 'ProblemReported'].includes(item.eventType)
  )?.id;

  return (
    <div className={`space-y-3 ${!inline ? 'px-4 pb-4 border-t bg-muted/20' : ''}`}>
      {!inline ? null : null}
      {history.map((item, index) => {
        const canUndo =
          item.id === latestUndoableActiveId &&
          !!onUndoAction;

        return (
        <div key={item.id} className={`flex gap-3 ${!inline && index === 0 ? 'pt-4' : ''}`}>
          <div className="text-xl">{getEventIcon(item.eventType)}</div>
          <div className="flex-1">
            <div className="flex items-start justify-between gap-3">
              <div className="font-medium">
                {t(`dashboard.events.${item.eventType}`)}
              </div>
              {canUndo && (
                <button
                  onClick={() => onUndoAction?.(item.id, item.eventType)}
                  className="px-2 py-1 border rounded-lg text-xs font-medium hover:bg-accent whitespace-nowrap"
                >
                  {t('dashboard.deleteAction')}
                </button>
              )}
            </div>
            {item.details && (
              <div className="text-sm text-muted-foreground">
                {formatHistoryDetails(item, t)}
              </div>
            )}
            <div className="text-xs text-muted-foreground">
              {formatDate(item.timestamp)}
            </div>
          </div>
        </div>
      )})}
    </div>
  );
}

function extractUndoneHistoryEntryId(details?: string): string | null {
  if (!details) return null;

  const match = details.match(/\|\|UNDO:([0-9a-fA-F-]{36})\|\|/);
  return match?.[1] ?? null;
}

function cleanHistoryDetails(details?: string): string {
  if (!details) return '';
  return details.replace(/\s*\|\|UNDO:[0-9a-fA-F-]{36}\|\|/, '').trim();
}

function formatHistoryDetails(
  item: { eventType: string; details?: string },
  t: (key: string) => string
): string {
  const details = cleanHistoryDetails(item.details);

  if (item.eventType !== 'ActionUndone' || !details) {
    return details;
  }

  return details
    .replace('Delivered', t('dashboard.events.Delivered'))
    .replace('MarkedReady', t('dashboard.events.MarkedReady'))
    .replace('ProblemReported', t('dashboard.events.ProblemReported'))
    .replace('OrderCancelled', t('dashboard.events.OrderCancelled'))
    .replace('LabelAssigned', t('dashboard.events.LabelAssigned'))
    .replace('Received', t('dashboard.events.Received'));
}

function DailyActivityChart({
  points,
  range,
  t
}: {
  points: DashboardStats['dailySeries'];
  range: ChartRange;
  t: ReturnType<typeof useTranslations>;
}) {
  const width = 640;
  const height = 260;
  const isLongRange = range > 7;
  const padding = {
    top: 20,
    right: 12,
    bottom: 32,
    left: 26
  };
  const innerWidth = width - padding.left - padding.right;
  const innerHeight = height - padding.top - padding.bottom;
  const maxValue = Math.max(1, ...points.flatMap((point) => [point.received, point.ready, point.delivered]));

  const toX = (index: number) => (
    padding.left + (points.length === 1 ? innerWidth / 2 : (index * innerWidth) / (points.length - 1))
  );

  const toY = (value: number) => (
    padding.top + innerHeight - (value / maxValue) * innerHeight
  );

  const buildPath = (values: number[]) => values
    .map((value, index) => `${index === 0 ? 'M' : 'L'} ${toX(index)} ${toY(value)}`)
    .join(' ');

  const series = [
    {
      key: 'received',
      label: t('dashboard.chart.received'),
      color: '#f59e0b',
      values: points.map((point) => point.received)
    },
    {
      key: 'ready',
      label: t('dashboard.chart.ready'),
      color: '#22c55e',
      values: points.map((point) => point.ready)
    },
    {
      key: 'delivered',
      label: t('dashboard.chart.delivered'),
      color: '#64748b',
      values: points.map((point) => point.delivered)
    }
  ] as const;

  return (
    <div className="border rounded-lg bg-muted/20 p-4 space-y-4">
      <div className="flex flex-wrap gap-3 text-sm">
        {series.map((item) => (
          <div key={item.key} className="flex items-center gap-2 text-muted-foreground">
            <span
              className="inline-block h-2.5 w-2.5 rounded-full"
              style={{ backgroundColor: item.color }}
            />
            <span>{item.label}</span>
          </div>
        ))}
      </div>

      <div>
        <svg viewBox={`0 0 ${width} ${height}`} className="w-full h-auto">
          {[0, 0.25, 0.5, 0.75, 1].map((ratio) => {
            const value = Math.round(maxValue * ratio);
            const y = toY(value);

            return (
              <g key={ratio}>
                <line
                  x1={padding.left}
                  x2={width - padding.right}
                  y1={y}
                  y2={y}
                  stroke="currentColor"
                  strokeOpacity="0.12"
                />
                <text
                  x={padding.left - 8}
                  y={y + 4}
                  textAnchor="end"
                  className="fill-muted-foreground text-[10px]"
                >
                  {value}
                </text>
              </g>
            );
          })}

          {series.map((item) => (
            <path
              key={item.key}
              d={buildPath(item.values)}
              fill="none"
              stroke={item.color}
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          ))}

          {series.map((item) =>
            item.values.map((value, index) => (
              <circle
                key={`${item.key}-${points[index].date}`}
                cx={toX(index)}
                cy={toY(value)}
                r="4"
                fill={item.color}
              />
            ))
          )}

          {points.map((point, index) => {
            const shouldRenderLabel = !isLongRange || index === 0 || index === points.length - 1 || index % (range === 30 ? 5 : 10) === 0;

            if (!shouldRenderLabel) {
              return null;
            }

            return (
              <text
                key={point.date}
                x={toX(index)}
                y={height - 10}
                textAnchor="middle"
                className="fill-muted-foreground text-[10px]"
              >
                {formatShortDate(point.date)}
              </text>
            );
          })}
        </svg>
      </div>
    </div>
  );
}

function formatShortDate(date: string) {
  const parsed = new Date(`${date}T00:00:00`);

  return parsed.toLocaleDateString('pt-PT', {
    day: '2-digit',
    month: '2-digit'
  });
}

function formatDurationBetween(startDate: string, endDate?: string): string | null {
  if (!endDate) return null;

  const start = new Date(startDate).getTime();
  const end = new Date(endDate).getTime();
  const totalMinutes = Math.max(0, Math.floor((end - start) / 60000));
  const days = Math.floor(totalMinutes / 1440);
  const hours = Math.floor((totalMinutes % 1440) / 60);
  const minutes = totalMinutes % 60;

  if (days > 0) {
    return `${days}d ${hours}h`;
  }

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }

  return `${minutes}m`;
}

function formatOrderDeadline(
  createdAt: string,
  status: string,
  t: ReturnType<typeof useTranslations>
): string {
  if (status === 'Completed' || status === 'Cancelled') {
    return t('dashboard.closed');
  }

  const elapsedMinutes = Math.max(0, Math.floor((Date.now() - new Date(createdAt).getTime()) / 60000));
  const days = Math.floor(elapsedMinutes / 1440);
  const hours = Math.floor((elapsedMinutes % 1440) / 60);

  if (days > 0) {
    return `${days}d ${hours}h`;
  }

  return `${hours}h`;
}

function cleanProblemNotes(notes?: string): string {
  if (!notes) return '';

  return notes.replace(/^\[[^\]]+\]\s*/, '').trim();
}

function ProblemCylinderCard({
  cylinder,
  t,
  formatDate,
  onViewHistory,
}: {
  cylinder: ProblemCylinder;
  t: ReturnType<typeof useTranslations>;
  formatDate: (value: string) => string;
  onViewHistory: (cylinderId: string) => Promise<void>;
}) {
  return (
    <div className="border rounded-lg bg-background p-4 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2">
          <div className="w-10 h-10 bg-red-100 dark:bg-red-900/30 rounded-full flex items-center justify-center text-red-600 dark:text-red-300 font-mono text-sm font-bold">
            #{cylinder.sequentialNumber}
          </div>
        </div>
        <span className="px-2 py-1 rounded-full text-xs font-medium bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300">
          {t('cylinder.status.problem')}
        </span>
      </div>

      <div className="grid gap-2 text-sm md:grid-cols-2">
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            {t('customer.title')}
          </div>
          <div className="font-medium">
            {cylinder.customerName || t('dashboard.problems.customerUnknown')}
          </div>
          <div className="text-xs text-muted-foreground">
            {cylinder.customerPhone || '-'}
          </div>
        </div>
        <div>
          <div className="text-xs uppercase tracking-wide text-muted-foreground">
            {t('order.title')}
          </div>
          <div className="font-medium">
            {cylinder.orderStatus
              ? t(`order.status.${cylinder.orderStatus.toLowerCase()}`)
              : t('dashboard.problems.orderUnknown')}
          </div>
          <div className="text-xs text-muted-foreground">
            {formatDate(cylinder.createdAt)}
          </div>
        </div>
      </div>

      {cleanProblemNotes(cylinder.occurrenceNotes) && (
        <div className="rounded-lg bg-muted/40 p-3 text-sm text-muted-foreground">
          {cleanProblemNotes(cylinder.occurrenceNotes)}
        </div>
      )}

      <div className="flex justify-end">
        <button
          onClick={() => void onViewHistory(cylinder.cylinderId)}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90 text-sm font-medium"
        >
          {t('dashboard.problems.viewHistory')}
        </button>
      </div>
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
