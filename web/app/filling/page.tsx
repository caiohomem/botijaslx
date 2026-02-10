'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { cylindersApi, pickupApi, FillingQueueItem } from '@/lib/api';
import { sendWhatsApp } from '@/lib/whatsapp';
import { QrScanner } from '@/components/QrScanner';

const PROBLEM_TYPES = [
  'valve',
  'physical_damage',
  'leak',
  'expired',
  'other'
];

export default function FillingPage() {
  const t = useTranslations();
  const [cylinders, setCylinders] = useState<FillingQueueItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [scanInput, setScanInput] = useState('');
  
  // Problem modal state
  const [problemModal, setProblemModal] = useState<{
    cylinderId: string;
    labelToken?: string;
  } | null>(null);
  const [problemType, setProblemType] = useState('');
  const [problemNotes, setProblemNotes] = useState('');

  const loadQueue = useCallback(async () => {
    try {
      setError(null);
      const response = await cylindersApi.getFillingQueue();
      setCylinders(response.cylinders);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar fila');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadQueue();

    // M5: Auto-refresh with polling every 10 seconds
    const pollInterval = setInterval(() => {
      loadQueue();
    }, 10000);

    return () => clearInterval(pollInterval);
  }, [loadQueue]);

  const sendOrderCompleteWhatsApp = async (customerName: string, customerPhone: string, cylinderCount: number, orderId: string) => {
    try {
      const savedSettings = localStorage.getItem('botijas_settings');
      const settings = savedSettings ? JSON.parse(savedSettings) : {};
      const template = settings.whatsappMessageTemplate || 'Olá {name}! As suas {count} botija(s) de CO₂ estão prontas para recolha. Visite-nos quando puder!';
      const message = template.replace('{name}', customerName).replace('{count}', String(cylinderCount));
      await sendWhatsApp(customerPhone, message);

      // Record notification in history
      pickupApi.markNotified(orderId).catch(() => {});
    } catch {
      // Silently fail
    }
  };

  const handleMarkReady = async (cylinderId: string) => {
    setActionLoading(cylinderId);
    setError(null);
    setSuccessMessage(null);

    // Get customer info before removing cylinder from list
    const cylinder = cylinders.find(c => c.cylinderId === cylinderId);

    try {
      const result = await cylindersApi.markReady(cylinderId);

      setCylinders(prev => prev.filter(c => c.cylinderId !== cylinderId));

      if (result.isOrderComplete && cylinder) {
        // All cylinders filled - notify customer via WhatsApp
        sendOrderCompleteWhatsApp(
          cylinder.customerName,
          cylinder.customerPhone,
          cylinder.totalCylindersInOrder,
          result.orderId
        );
        setSuccessMessage(t('filling.orderCompleteNotified', { name: cylinder.customerName }));
      } else {
        setSuccessMessage(t('filling.marked'));
      }

      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao marcar botija');
    } finally {
      setActionLoading(null);
    }
  };

  // M3: Mark all cylinders in order as ready
  const handleMarkAllReady = async (orderId: string, count: number) => {
    const batchKey = `${orderId}_batch`;
    setActionLoading(batchKey);
    setError(null);

    try {
      const result = await cylindersApi.markReadyBatch(orderId);

      // Remove all cylinders from this order from the list
      setCylinders(prev => prev.filter(c => c.orderId !== orderId));

      // Get customer info from remaining cylinders (for notification)
      const cylindersInOrder = cylinders.filter(c => c.orderId === orderId);
      const customerInfo = cylindersInOrder[0];

      if (result.isOrderComplete && customerInfo) {
        // All cylinders filled - notify customer via WhatsApp
        sendOrderCompleteWhatsApp(
          customerInfo.customerName,
          customerInfo.customerPhone,
          customerInfo.totalCylindersInOrder,
          orderId
        );
        setSuccessMessage(t('filling.orderCompleteNotified', { name: customerInfo.customerName }));
      } else {
        setSuccessMessage(t('filling.marked'));
      }

      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao marcar botijas');
    } finally {
      setActionLoading(null);
    }
  };

  const handleReportProblem = async () => {
    if (!problemModal || !problemType) return;

    setActionLoading(problemModal.cylinderId);
    setError(null);

    try {
      await cylindersApi.reportProblem(
        problemModal.cylinderId,
        problemType,
        problemNotes
      );
      
      setCylinders(prev => prev.filter(c => c.cylinderId !== problemModal.cylinderId));
      setSuccessMessage(t('filling.problemReported'));
      setTimeout(() => setSuccessMessage(null), 3000);
      
      // Reset modal
      setProblemModal(null);
      setProblemType('');
      setProblemNotes('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao reportar problema');
    } finally {
      setActionLoading(null);
    }
  };

  const handleScanValue = (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return;

    const cylinder = cylinders.find(
      c => c.labelToken === trimmed ||
           c.cylinderId === trimmed ||
           c.cylinderId.startsWith(trimmed)
    );

    if (cylinder) {
      handleMarkReady(cylinder.cylinderId);
      setScanInput('');
    } else {
      setError(t('filling.notFound'));
      setTimeout(() => setError(null), 3000);
    }
  };

  const handleScan = () => handleScanValue(scanInput);

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // M6: Calculate wait time and priority
  const getWaitTimeMinutes = (receivedAt: string): number => {
    const received = new Date(receivedAt);
    const now = new Date();
    return Math.floor((now.getTime() - received.getTime()) / 60000);
  };

  const getWaitTimeBadgeClass = (waitMinutes: number): string => {
    if (waitMinutes >= 60) return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'; // 1+ hour
    if (waitMinutes >= 30) return 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-400'; // 30+ minutes
    if (waitMinutes >= 15) return 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400'; // 15+ minutes
    return 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'; // < 15 minutes
  };

  const formatWaitTime = (waitMinutes: number): string => {
    if (waitMinutes >= 60) {
      const hours = Math.floor(waitMinutes / 60);
      const mins = waitMinutes % 60;
      return `${hours}h ${mins}m`;
    }
    return `${waitMinutes}m`;
  };

  // Agrupar botijas por pedido
  const groupedByOrder = cylinders.reduce((acc, cylinder) => {
    if (!acc[cylinder.orderId]) {
      acc[cylinder.orderId] = {
        orderId: cylinder.orderId,
        customerName: cylinder.customerName,
        customerPhone: cylinder.customerPhone,
        totalCylindersInOrder: cylinder.totalCylindersInOrder,
        readyCylindersInOrder: cylinder.readyCylindersInOrder,
        cylinders: []
      };
    }
    acc[cylinder.orderId].cylinders.push(cylinder);
    return acc;
  }, {} as Record<string, {
    orderId: string;
    customerName: string;
    customerPhone: string;
    totalCylindersInOrder: number;
    readyCylindersInOrder: number;
    cylinders: FillingQueueItem[];
  }>);

  const orderGroups = Object.values(groupedByOrder);

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">{t('filling.title')}</h1>
        {/* M5: Auto-refresh indicator */}
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="inline-block w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
          {t('common.autoRefresh') || 'Auto-refresh: every 10s'}
        </div>
      </div>

      {/* M7: KPI Counters */}
      {!loading && cylinders.length > 0 && (
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-muted/30 p-4 rounded-lg border">
            <div className="text-xs text-muted-foreground uppercase tracking-wide">{t('common.total') || 'Total'}</div>
            <div className="text-2xl font-bold mt-2">{cylinders.length}</div>
            <div className="text-xs text-muted-foreground mt-1">{t('filling.title')}</div>
          </div>
          <div className="bg-blue-50 dark:bg-blue-900/20 p-4 rounded-lg border border-blue-200 dark:border-blue-800">
            <div className="text-xs text-blue-700 dark:text-blue-300 uppercase tracking-wide font-medium">{t('common.pending') || 'Pending'}</div>
            <div className="text-2xl font-bold mt-2 text-blue-700 dark:text-blue-300">
              {cylinders.filter(c => !c.labelToken?.endsWith('_ready')).length}
            </div>
            <div className="text-xs text-blue-600 dark:text-blue-400 mt-1">{t('common.awaiting') || 'Awaiting'}</div>
          </div>
          <div className="bg-green-50 dark:bg-green-900/20 p-4 rounded-lg border border-green-200 dark:border-green-800">
            <div className="text-xs text-green-700 dark:text-green-300 uppercase tracking-wide font-medium">{t('common.ready') || 'Ready'}</div>
            <div className="text-2xl font-bold mt-2 text-green-700 dark:text-green-300">
              {cylinders.filter(c => c.labelToken?.endsWith('_ready')).length}
            </div>
            <div className="text-xs text-green-600 dark:text-green-400 mt-1">{t('common.completed') || 'Completed'}</div>
          </div>
        </div>
      )}

      {/* Scan Input */}
      <QrScanner
        onScan={(code) => handleScanValue(code)}
        label={t('filling.scanCamera')}
      />
      <div className="flex gap-2">
        <input
          type="text"
          value={scanInput}
          onChange={(e) => setScanInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleScan()}
          placeholder={t('filling.scanPlaceholder')}
          className="flex-1 px-4 py-3 border rounded-lg bg-background text-foreground text-lg"
          autoFocus
        />
        <button
          onClick={handleScan}
          disabled={!scanInput.trim()}
          className="px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50 font-medium"
        >
          OK
        </button>
      </div>

      {/* Messages */}
      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg">
          {error}
        </div>
      )}

      {successMessage && (
        <div className="p-3 bg-green-500/10 text-green-600 dark:text-green-400 rounded-lg">
          {successMessage}
        </div>
      )}

      {/* Loading */}
      {loading && (
        <div className="text-center text-muted-foreground py-8">
          {t('common.loading')}
        </div>
      )}

      {/* Empty State */}
      {!loading && cylinders.length === 0 && (
        <div className="text-center text-muted-foreground py-12 border-2 border-dashed rounded-lg">
          <div className="text-4xl mb-2">✓</div>
          <div>{t('filling.empty')}</div>
        </div>
      )}

      {/* Queue grouped by order */}
      {!loading && orderGroups.length > 0 && (
        <div className="space-y-4">
          {orderGroups.map((group) => (
            <div key={group.orderId} className="border rounded-lg overflow-hidden">
              {/* Order Header */}
              <div className="bg-muted/50 p-4 border-b">
                <div className="flex justify-between items-start gap-4">
                  <div className="flex-1">
                    <div className="font-semibold">{group.customerName}</div>
                    <div className="text-sm text-muted-foreground">{group.customerPhone}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="text-sm bg-background px-3 py-1 rounded-full whitespace-nowrap">
                      {t('filling.progress', {
                        ready: group.readyCylindersInOrder,
                        total: group.totalCylindersInOrder
                      })}
                    </div>
                    {/* M3: Mark all ready button */}
                    {group.cylinders.some(c => c.cylinderId && !c.cylinderId.endsWith('_ready')) && (
                      <button
                        onClick={() => handleMarkAllReady(group.orderId, group.cylinders.filter(c => c.cylinderId && !c.cylinderId.endsWith('_ready')).length)}
                        disabled={actionLoading === `${group.orderId}_batch`}
                        className="px-3 py-1 text-sm bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors whitespace-nowrap font-medium"
                        title={t('filling.markAllReady', { count: group.cylinders.length - group.readyCylindersInOrder })}
                      >
                        {actionLoading === `${group.orderId}_batch` ? (
                          <span className="inline-block animate-spin">⟳</span>
                        ) : (
                          `✓ ${group.cylinders.length - group.readyCylindersInOrder}`
                        )}
                      </button>
                    )}
                  </div>
                </div>
              </div>

              {/* Cylinders */}
              <div className="divide-y">
                {/* M6: Sort cylinders by wait time (longest waiting first) */}
                {[...group.cylinders].sort((a, b) =>
                  getWaitTimeMinutes(b.receivedAt) - getWaitTimeMinutes(a.receivedAt)
                ).map((cylinder) => (
                  <div
                    key={cylinder.cylinderId}
                    className="p-4 flex items-center justify-between hover:bg-muted/30 transition-colors"
                  >
                    <div className="flex-1">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm">
                          {cylinder.labelToken ? cylinder.labelToken.slice(0, 4) : '?'}
                        </div>
                        <div className="flex-1">
                          <div className="font-mono text-sm">
                            {cylinder.labelToken || (
                              <span className="text-muted-foreground italic">
                                {t('filling.noLabel')}
                              </span>
                            )}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {t('filling.receivedAt')}: {formatDate(cylinder.receivedAt)}
                          </div>
                        </div>
                        {/* M6: Wait time badge */}
                        <div className={`px-2 py-1 text-xs font-medium rounded-full whitespace-nowrap ${getWaitTimeBadgeClass(getWaitTimeMinutes(cylinder.receivedAt))}`}>
                          {formatWaitTime(getWaitTimeMinutes(cylinder.receivedAt))}
                        </div>
                      </div>
                    </div>

                    <div className="flex gap-2">
                      <button
                        onClick={() => setProblemModal({
                          cylinderId: cylinder.cylinderId,
                          labelToken: cylinder.labelToken
                        })}
                        disabled={actionLoading === cylinder.cylinderId}
                        className="px-3 py-2 border border-red-300 dark:border-red-700 text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg disabled:opacity-50 transition-colors text-sm"
                      >
                        {t('filling.problem')}
                      </button>
                      <button
                        onClick={() => handleMarkReady(cylinder.cylinderId)}
                        disabled={actionLoading === cylinder.cylinderId}
                        className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors font-medium"
                      >
                        {actionLoading === cylinder.cylinderId ? (
                          <span className="inline-block animate-spin">⟳</span>
                        ) : (
                          t('cylinder.markReady')
                        )}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Refresh Button */}
      <div className="flex justify-center">
        <button
          onClick={() => { setLoading(true); loadQueue(); }}
          disabled={loading}
          className="px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
        >
          {loading ? t('common.loading') : t('filling.refresh')}
        </button>
      </div>

      {/* Problem Modal */}
      {problemModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-background rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('filling.reportProblem')}</h3>
            
            <div className="text-sm text-muted-foreground">
              {t('filling.cylinder')}: {problemModal.labelToken || problemModal.cylinderId.slice(0, 8)}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('filling.problemType')}</label>
              <select
                value={problemType}
                onChange={(e) => setProblemType(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg bg-background"
              >
                <option value="">{t('filling.selectProblemType')}</option>
                {PROBLEM_TYPES.map(type => (
                  <option key={type} value={type}>
                    {t(`filling.problemTypes.${type}`)}
                  </option>
                ))}
              </select>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">{t('filling.notes')}</label>
              <textarea
                value={problemNotes}
                onChange={(e) => setProblemNotes(e.target.value)}
                placeholder={t('filling.notesPlaceholder')}
                className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
              />
            </div>

            <div className="flex gap-3 pt-2">
              <button
                onClick={() => {
                  setProblemModal(null);
                  setProblemType('');
                  setProblemNotes('');
                }}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={handleReportProblem}
                disabled={!problemType || actionLoading === problemModal.cylinderId}
                className="flex-1 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg disabled:opacity-50"
              >
                {t('filling.confirmProblem')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
