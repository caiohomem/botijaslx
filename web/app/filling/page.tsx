'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { cylindersApi, pickupApi, historyApi, generateWhatsAppLink, FillingQueueItem } from '@/lib/api';
import { playSound } from '@/lib/sounds';
import { QrScanner } from '@/components/QrScanner';
import { DEFAULT_APP_SETTINGS, loadAppSettings } from '@/lib/settings';

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
    sequentialNumber: number;
  } | null>(null);
  const [problemType, setProblemType] = useState('');
  const [problemNotes, setProblemNotes] = useState('');
  const [showProblems, setShowProblems] = useState(false);
  const [reportedProblems, setReportedProblems] = useState<Array<{
    cylinderId: string;
    sequentialNumber: number;
    type: string;
    notes: string;
    timestamp: string;
  }>>([]);
  const [messageTemplate, setMessageTemplate] = useState(DEFAULT_APP_SETTINGS.whatsAppMessageTemplate);
  const [storeLink, setStoreLink] = useState(DEFAULT_APP_SETTINGS.storeLink);
  const [completedOrders, setCompletedOrders] = useState<Array<{
    orderId: string;
    customerName: string;
    customerPhone: string;
    customerPhoneType: string;
    cylinderCount: number;
  }>>([]);

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
    loadAppSettings().then((settings) => {
      setMessageTemplate(settings.whatsAppMessageTemplate);
      setStoreLink(settings.storeLink);
    });
  }, [loadQueue]);

  const openWhatsApp = (customerName: string, customerPhone: string, customerPhoneType: string, cylinderCount: number, orderId: string) => {
    const message = messageTemplate
      .replace('{name}', customerName)
      .replace('{count}', String(cylinderCount))
      .replace('{link}', storeLink);
    const link = generateWhatsAppLink(
      customerPhone,
      message,
      customerPhoneType === 'International' ? 'international' : 'pt'
    );
    window.open(link, '_blank');

    // Record notification in history
    pickupApi.markNotified(orderId).catch(() => {});

    // Remove from completed orders list
    setCompletedOrders(prev => prev.filter(o => o.orderId !== orderId));
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
        // All cylinders filled - track for manual WhatsApp notification
        setCompletedOrders(prev => [...prev, {
          orderId: result.orderId,
          customerName: cylinder.customerName,
          customerPhone: cylinder.customerPhone,
          customerPhoneType: cylinder.customerPhoneType,
          cylinderCount: cylinder.totalCylindersInOrder,
        }]);
        // M10: Play completion sound
        playSound('complete');
        setSuccessMessage(t('filling.orderComplete', { name: cylinder.customerName }));
      } else {
        // M10: Play success sound for individual cylinder
        playSound('success');
        setSuccessMessage(t('filling.marked'));
      }

      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao marcar botija');
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
      // M10: Play warning sound for problem report
      playSound('warning');

      // M12: Track reported problem for workflow
      setReportedProblems(prev => [...prev, {
        cylinderId: problemModal.cylinderId,
        sequentialNumber: problemModal.sequentialNumber,
        type: problemType,
        notes: problemNotes,
        timestamp: new Date().toISOString()
      }]);

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

  const formatCylinderStatus = (state: string) => {
    return t(`cylinder.status.${state.toLowerCase()}`);
  };

  const formatOrderStatus = (status?: string) => {
    if (!status) return '';
    return t(`order.status.${status.toLowerCase()}`);
  };

  const handleScanValue = async (value: string) => {
    const trimmed = value.trim();
    if (!trimmed) return;

    const normalizedToken = trimmed.toUpperCase();
    const cleanNum = trimmed.replace(/^#?0*/, '');
    const seqNum = parseInt(cleanNum, 10);
    const cylinder = cylinders.find(
      c => c.labelToken === normalizedToken ||
           (!isNaN(seqNum) && c.sequentialNumber === seqNum)
    );

    if (cylinder) {
      await handleMarkReady(cylinder.cylinderId);
      setScanInput('');
    } else {
      try {
        const cylinderData = await historyApi.scanCylinder(trimmed);
        const cylinderLabel = `#${String(cylinderData.sequentialNumber).padStart(4, '0')}`;
        const stateLabel = formatCylinderStatus(cylinderData.state);
        const orderStatusLabel = formatOrderStatus(cylinderData.currentOrderStatus);

        const message = cylinderData.currentOrderStatus
          ? t('filling.notInQueueWithStatusAndOrder', {
              cylinder: cylinderLabel,
              state: stateLabel,
              orderStatus: orderStatusLabel,
            })
          : t('filling.notInQueueWithStatus', {
              cylinder: cylinderLabel,
              state: stateLabel,
            });

        setError(message);
      } catch {
        setError(t('filling.notFound'));
      }
      setTimeout(() => setError(null), 3000);
    }
  };

  const handleScan = async () => handleScanValue(scanInput);

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
        <button
          onClick={() => { setLoading(true); loadQueue(); }}
          disabled={loading}
          className="px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
        >
          {loading ? t('common.loading') : t('filling.refresh')}
        </button>
      </div>

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

      {/* Completed orders - manual WhatsApp notification */}
      {completedOrders.length > 0 && (
        <div className="space-y-2">
          {completedOrders.map((order) => (
            <div key={order.orderId} className="p-4 border-2 border-green-300 dark:border-green-700 bg-green-50 dark:bg-green-900/20 rounded-lg flex items-center justify-between gap-4">
              <div>
                <div className="font-semibold text-green-800 dark:text-green-200">{order.customerName}</div>
                <div className="text-sm text-green-600 dark:text-green-400">
                  {order.cylinderCount} {t('order.cylinders')} — {t('filling.orderComplete', { name: '' }).replace(/ $/, '').trim()}
                </div>
              </div>
              <button
                onClick={() => openWhatsApp(order.customerName, order.customerPhone, order.customerPhoneType, order.cylinderCount, order.orderId)}
                className="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium whitespace-nowrap transition-colors"
              >
                {t('pickup.sendWhatsApp')}
              </button>
            </div>
          ))}
        </div>
      )}

      {/* M11: Inline loading indicator instead of full-screen block */}
      {loading && cylinders.length === 0 && (
        <div className="flex items-center justify-center gap-2 py-8 text-muted-foreground">
          <span className="inline-block animate-spin">⟳</span>
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
                        <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm font-bold">
                          #{cylinder.sequentialNumber}
                        </div>
                        <div className="flex-1">
                          <div className="font-mono text-sm font-bold">
                            #{String(cylinder.sequentialNumber).padStart(4, '0')}
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
                          sequentialNumber: cylinder.sequentialNumber
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

      {/* M12: Problem tracking section */}
      {reportedProblems.length > 0 && (
        <div className="border rounded-lg overflow-hidden">
          <button
            onClick={() => setShowProblems(!showProblems)}
            className="w-full bg-red-50 dark:bg-red-900/20 p-4 text-left flex justify-between items-center border-b hover:bg-red-100 dark:hover:bg-red-900/30 transition-colors"
          >
            <div className="flex items-center gap-2">
              <span className="text-xl">⚠️</span>
              <span className="font-semibold text-red-700 dark:text-red-300">
                {reportedProblems.length} {t('filling.problem')}(s) Reported
              </span>
            </div>
            <span>{showProblems ? '▼' : '▶'}</span>
          </button>

          {showProblems && (
            <div className="divide-y bg-red-50/50 dark:bg-red-900/10">
              {reportedProblems.map((problem, idx) => (
                <div key={idx} className="p-4">
                  <div className="flex justify-between items-start mb-2">
                    <div>
                      <div className="font-mono text-sm font-medium font-bold">
                        #{String(problem.sequentialNumber).padStart(4, '0')}
                      </div>
                      <div className="text-xs text-muted-foreground mt-1">
                        {new Date(problem.timestamp).toLocaleString('pt-PT')}
                      </div>
                    </div>
                    <div className="text-sm font-medium text-red-700 dark:text-red-300">
                      {t(`filling.problemTypes.${problem.type}`)}
                    </div>
                  </div>
                  {problem.notes && (
                    <div className="text-sm text-muted-foreground mt-2 p-2 bg-background rounded border-l-2 border-red-400">
                      {problem.notes}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
      {/* Problem Modal */}
      {problemModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-background rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('filling.reportProblem')}</h3>
            
            <div className="text-sm text-muted-foreground">
              {t('filling.cylinder')}: #{String(problemModal.sequentialNumber).padStart(4, '0')}
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
