'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { pickupApi, PickupOrder } from '@/lib/api';
import { sendWhatsApp } from '@/lib/whatsapp';

export default function PickupPage() {
  const t = useTranslations();
  const [orders, setOrders] = useState<PickupOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedOrder, setExpandedOrder] = useState<string | null>(null);
  const [confirmDeliver, setConfirmDeliver] = useState<{ orderId: string; count: number } | null>(null);

  const loadOrders = useCallback(async () => {
    try {
      setError(null);
      const response = await pickupApi.getReadyForPickup(searchQuery || undefined);
      setOrders(response.orders);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar pedidos');
    } finally {
      setLoading(false);
    }
  }, [searchQuery]);

  useEffect(() => {
    loadOrders();

    // M5: Auto-refresh with polling every 10 seconds
    const pollInterval = setInterval(() => {
      loadOrders();
    }, 10000);

    return () => clearInterval(pollInterval);
  }, [loadOrders]);

  const sendThankYouWhatsApp = async (order: PickupOrder) => {
    try {
      const savedSettings = localStorage.getItem('botijas_settings');
      const settings = savedSettings ? JSON.parse(savedSettings) : {};
      const template = settings.thankYouMessageTemplate || 'Obrigado por utilizar o nosso serviço de enchimento. Obrigado, equipa da Oficina da Cerveja!';
      const message = template.replace('{name}', order.customerName).replace('{count}', String(order.totalCylinders));
      await sendWhatsApp(order.customerPhone, message);
    } catch {
      // Silently fail
    }
  };

  const handleDeliverAll = async (order: PickupOrder) => {
    const undelivered = order.cylinders.filter(c => !c.isDelivered);
    if (undelivered.length === 0) return;

    setActionLoading(order.orderId);
    setError(null);
    setSuccessMessage(null);

    try {
      // Deliver all undelivered cylinders one by one
      for (const cylinder of undelivered) {
        await pickupApi.deliverCylinder(order.orderId, cylinder.cylinderId);
      }

      // All delivered - send thank you WhatsApp
      sendThankYouWhatsApp(order);

      setOrders(prev => prev.filter(o => o.orderId !== order.orderId));
      setExpandedOrder(null);
      setConfirmDeliver(null);
      setSuccessMessage(t('pickup.orderCompleteThankYou', { name: order.customerName }));
      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao entregar botijas');
      // Reload to get current state
      setLoading(true);
      loadOrders();
    } finally {
      setActionLoading(null);
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // M8: Calculate wait time and urgency
  const getWaitTimeMinutes = (createdAt: string): number => {
    const created = new Date(createdAt);
    const now = new Date();
    return Math.floor((now.getTime() - created.getTime()) / 60000);
  };

  const getWaitTimeBadgeClass = (waitMinutes: number): string => {
    if (waitMinutes >= 120) return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'; // 2+ hours
    if (waitMinutes >= 60) return 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-400'; // 1+ hour
    if (waitMinutes >= 30) return 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400'; // 30+ minutes
    return 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'; // < 30 minutes
  };

  const formatWaitTime = (waitMinutes: number): string => {
    if (waitMinutes >= 60) {
      const hours = Math.floor(waitMinutes / 60);
      const mins = waitMinutes % 60;
      return `${hours}h ${mins}m`;
    }
    return `${waitMinutes}m`;
  };

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    loadOrders();
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">{t('pickup.title')}</h1>
        {/* M5: Auto-refresh indicator */}
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="inline-block w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
          {t('common.autoRefresh') || 'Auto-refresh: every 10s'}
        </div>
      </div>

      {/* M7: KPI Counters */}
      {!loading && orders.length > 0 && (
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-muted/30 p-4 rounded-lg border">
            <div className="text-xs text-muted-foreground uppercase tracking-wide">{t('common.orders') || 'Orders'}</div>
            <div className="text-2xl font-bold mt-2">{orders.length}</div>
            <div className="text-xs text-muted-foreground mt-1">{t('pickup.title')}</div>
          </div>
          <div className="bg-purple-50 dark:bg-purple-900/20 p-4 rounded-lg border border-purple-200 dark:border-purple-800">
            <div className="text-xs text-purple-700 dark:text-purple-300 uppercase tracking-wide font-medium">{t('common.cylinders') || 'Cylinders'}</div>
            <div className="text-2xl font-bold mt-2 text-purple-700 dark:text-purple-300">
              {orders.reduce((sum, o) => sum + o.totalCylinders, 0)}
            </div>
            <div className="text-xs text-purple-600 dark:text-purple-400 mt-1">{t('common.total') || 'Total'}</div>
          </div>
          <div className="bg-orange-50 dark:bg-orange-900/20 p-4 rounded-lg border border-orange-200 dark:border-orange-800">
            <div className="text-xs text-orange-700 dark:text-orange-300 uppercase tracking-wide font-medium">{t('common.pending') || 'Pending'}</div>
            <div className="text-2xl font-bold mt-2 text-orange-700 dark:text-orange-300">
              {orders.reduce((sum, o) => sum + (o.totalCylinders - o.deliveredCylinders), 0)}
            </div>
            <div className="text-xs text-orange-600 dark:text-orange-400 mt-1">{t('common.delivery') || 'Delivery'}</div>
          </div>
        </div>
      )}

      {/* Search */}
      <form onSubmit={handleSearch} className="flex gap-2">
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder={t('pickup.searchPlaceholder')}
          className="flex-1 px-4 py-2 border rounded-lg bg-background text-foreground"
        />
        <button
          type="submit"
          className="px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90"
        >
          {t('common.search')}
        </button>
      </form>

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
      {!loading && orders.length === 0 && (
        <div className="text-center text-muted-foreground py-12 border-2 border-dashed rounded-lg">
          <div className="text-4xl mb-2">📦</div>
          <div>{t('pickup.empty')}</div>
        </div>
      )}

      {/* Orders List */}
      {!loading && orders.length > 0 && (
        <div className="space-y-4">
          {/* M8: Sort orders by wait time (longest waiting first) */}
          {[...orders].sort((a, b) =>
            getWaitTimeMinutes(b.createdAt) - getWaitTimeMinutes(a.createdAt)
          ).map((order) => (
            (() => {
              const undeliveredCount = order.cylinders.filter(c => !c.isDelivered).length;
              const deliveredCount = order.totalCylinders - undeliveredCount;

              return (
            <div key={order.orderId} className="border rounded-lg overflow-hidden">
              {/* Order Header */}
              <div className="bg-muted/50 p-4 border-b">
                <div className="flex justify-between items-start gap-4">
                  <button
                    onClick={() => setExpandedOrder(
                      expandedOrder === order.orderId ? null : order.orderId
                    )}
                    className="text-left flex-1"
                  >
                    <div className="font-semibold text-lg">{order.customerName}</div>
                    <div className="text-sm text-muted-foreground">{order.customerPhone}</div>
                    <div className="text-xs text-muted-foreground mt-1">
                      {t('pickup.createdAt')}: {formatDate(order.createdAt)}
                    </div>
                  </button>

                  <div className="flex items-center gap-2">
                    {/* M8: Wait time badge */}
                    <div className={`text-sm px-3 py-1 rounded-full whitespace-nowrap font-medium ${getWaitTimeBadgeClass(getWaitTimeMinutes(order.createdAt))}`}>
                      {formatWaitTime(getWaitTimeMinutes(order.createdAt))}
                    </div>

                    <div className="text-sm bg-background px-3 py-1 rounded-full whitespace-nowrap">
                      {t('pickup.progress', { delivered: deliveredCount, total: order.totalCylinders })}
                    </div>

                    {/* M4: Direct deliver button without expand */}
                    {undeliveredCount > 0 && (
                      <button
                        onClick={() => setConfirmDeliver({ orderId: order.orderId, count: undeliveredCount })}
                        disabled={actionLoading === order.orderId}
                        className="px-3 py-1 text-sm bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors whitespace-nowrap font-medium"
                        title={t('pickup.deliverAll', { count: undeliveredCount })}
                      >
                        {actionLoading === order.orderId ? (
                          <span className="inline-block animate-spin">⟳</span>
                        ) : (
                          `✓ ${undeliveredCount}`
                        )}
                      </button>
                    )}

                    <button
                      onClick={() => setExpandedOrder(
                        expandedOrder === order.orderId ? null : order.orderId
                      )}
                      className="text-xl p-1"
                    >
                      {expandedOrder === order.orderId ? '▼' : '▶'}
                    </button>
                  </div>
                </div>
              </div>

              {/* Expanded: Cylinders List + Deliver All Button */}
              {expandedOrder === order.orderId && (
                <div>
                  {/* Cylinders List (read-only) */}
                  <div className="divide-y">
                    {order.cylinders.map((cylinder) => (
                      <div
                        key={cylinder.cylinderId}
                        className="p-4 flex items-center gap-3"
                      >
                        <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm">
                          {cylinder.labelToken?.slice(0, 4) || '?'}
                        </div>
                        <div className="flex-1">
                          <div className="font-mono text-sm">
                            {cylinder.labelToken || (
                              <span className="text-muted-foreground italic">
                                {t('pickup.noLabel')}
                              </span>
                            )}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {t(`cylinder.status.${cylinder.state.toLowerCase()}`)}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>

                  {/* Single Deliver All Button */}
                  {undeliveredCount > 0 && (
                    <div className="p-4 border-t">
                      <button
                        onClick={() => handleDeliverAll(order)}
                        disabled={actionLoading === order.orderId}
                        className="w-full px-4 py-3 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors font-medium text-lg"
                      >
                        {actionLoading === order.orderId ? (
                          <span className="inline-flex items-center gap-2">
                            <span className="inline-block animate-spin">⟳</span>
                            {t('common.loading')}
                          </span>
                        ) : (
                          t('pickup.deliverAll', { count: undeliveredCount })
                        )}
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
              );
            })()
          ))}
        </div>
      )}

      {/* Refresh Button */}
      <div className="flex justify-center">
        <button
          onClick={() => { setLoading(true); loadOrders(); }}
          disabled={loading}
          className="px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
        >
          {loading ? t('common.loading') : t('pickup.refresh')}
        </button>
      </div>

      {/* M4: Delivery Confirmation Modal */}
      {confirmDeliver && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center p-4 z-50">
          <div className="bg-background rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('common.confirm')}</h3>

            <div className="text-sm text-muted-foreground">
              {t('pickup.confirmDeliver', {
                count: confirmDeliver.count,
                name: orders.find(o => o.orderId === confirmDeliver.orderId)?.customerName || ''
              })}
            </div>

            <div className="flex gap-3 pt-2">
              <button
                onClick={() => setConfirmDeliver(null)}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={() => {
                  const order = orders.find(o => o.orderId === confirmDeliver.orderId);
                  if (order) {
                    handleDeliverAll(order);
                    setConfirmDeliver(null);
                  }
                }}
                disabled={actionLoading === confirmDeliver.orderId}
                className="flex-1 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50"
              >
                {actionLoading === confirmDeliver.orderId ? (
                  <span className="inline-flex items-center gap-2">
                    <span className="inline-block animate-spin">⟳</span>
                    {t('common.loading')}
                  </span>
                ) : (
                  t('pickup.deliver')
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
