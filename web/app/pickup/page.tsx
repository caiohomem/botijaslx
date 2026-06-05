'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { pickupApi, generateWhatsAppLink, PickupOrder } from '@/lib/api';
import { playSound } from '@/lib/sounds';
import { DEFAULT_APP_SETTINGS, loadAppSettings } from '@/lib/settings';
import { getWaitTimeMinutes, formatWaitTime } from '@/lib/time';

export default function PickupPage() {
  const t = useTranslations();
  const [orders, setOrders] = useState<PickupOrder[]>([]);
  const [fulfillmentFilter, setFulfillmentFilter] = useState<'all' | 'pickup' | 'shipping'>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedOrder, setExpandedOrder] = useState<string | null>(null);
  const [confirmDeliver, setConfirmDeliver] = useState<{ orderId: string; count: number } | null>(null);
  const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);
  const [pickupTemplate, setPickupTemplate] = useState(DEFAULT_APP_SETTINGS.whatsAppMessageTemplate);
  const [shippingTemplate, setShippingTemplate] = useState(DEFAULT_APP_SETTINGS.shippingReadyMessageTemplate);
  const [thankYouTemplate, setThankYouTemplate] = useState(DEFAULT_APP_SETTINGS.thankYouMessageTemplate);
  const [storeLink, setStoreLink] = useState(DEFAULT_APP_SETTINGS.storeLink);

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
    loadAppSettings().then((settings) => {
      setPickupTemplate(settings.whatsAppMessageTemplate);
      setShippingTemplate(settings.shippingReadyMessageTemplate);
      setThankYouTemplate(settings.thankYouMessageTemplate);
      setStoreLink(settings.storeLink);
    });

    // M5: Auto-refresh with polling every 10 seconds
    const pollInterval = setInterval(() => {
      loadOrders();
    }, 10000);

    return () => {
      clearInterval(pollInterval);
      // M9: Cleanup debounce timer on unmount
      if (debounceTimerRef.current) {
        clearTimeout(debounceTimerRef.current);
      }
    };
  }, [loadOrders]);

  const openReadyWhatsApp = async (order: PickupOrder) => {
    const template = order.fulfillmentMethod === 'Shipping'
      ? shippingTemplate
      : pickupTemplate;
    const message = template
      .replace('{name}', order.customerName)
      .replace('{count}', String(order.totalCylinders))
      .replace('{link}', storeLink);
    const link = generateWhatsAppLink(
      order.customerPhone,
      message,
      order.customerPhoneType === 'International' ? 'international' : 'pt'
    );
    window.open(link, '_blank');

    // Marcar como notificado se ainda não foi
    if (order.needsNotification) {
      try {
        await pickupApi.markNotified(order.orderId);
        setOrders(prev => prev.map(o =>
          o.orderId === order.orderId
            ? { ...o, needsNotification: false, notifiedAt: new Date().toISOString() }
            : o
        ));
      } catch {
        // Não bloquear o fluxo se falhar
      }
    }
  };

  const openThankYouWhatsApp = (order: PickupOrder) => {
    const message = thankYouTemplate
      .replace('{name}', order.customerName)
      .replace('{count}', String(order.totalCylinders))
      .replace('{link}', storeLink);

    const link = generateWhatsAppLink(
      order.customerPhone,
      message,
      order.customerPhoneType === 'International' ? 'international' : 'pt'
    );

    window.open(link, '_blank');
  };

  const handleShipOrder = async (order: PickupOrder) => {
    setActionLoading(order.orderId);
    setError(null);
    setSuccessMessage(null);

    try {
      await pickupApi.markShipped(order.orderId);
      openThankYouWhatsApp(order);
      playSound('complete');
      setOrders(prev => prev.filter(o => o.orderId !== order.orderId));
      setExpandedOrder(null);
      setSuccessMessage(t('pickup.orderCompleteThankYou', { name: order.customerName }));
      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao marcar pedido como enviado');
      setLoading(true);
      loadOrders();
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeliverAll = async (order: PickupOrder, sendThankYou: boolean) => {
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

      if (sendThankYou) {
        openThankYouWhatsApp(order);
      }

      // M10: Play success sound for order completion
      playSound('complete');

      setOrders(prev => prev.filter(o => o.orderId !== order.orderId));
      setExpandedOrder(null);
      setConfirmDeliver(null);
      setSuccessMessage(
        sendThankYou
          ? t('pickup.orderCompleteThankYou', { name: order.customerName })
          : t('pickup.orderComplete', { name: order.customerName })
      );
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

  // M8: Wait time urgency badge
  const getWaitTimeBadgeClass = (waitMinutes: number): string => {
    if (waitMinutes >= 120) return 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'; // 2+ hours
    if (waitMinutes >= 60) return 'bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-400'; // 1+ hour
    if (waitMinutes >= 30) return 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400'; // 30+ minutes
    return 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'; // < 30 minutes
  };

  const filteredOrders = orders.filter((order) => {
    if (fulfillmentFilter === 'pickup') {
      return order.fulfillmentMethod === 'Pickup';
    }

    if (fulfillmentFilter === 'shipping') {
      return order.fulfillmentMethod === 'Shipping';
    }

    return true;
  });

  const emptyMessage = fulfillmentFilter === 'all'
    ? t('pickup.empty')
    : t('pickup.emptyFiltered', {
        filter: fulfillmentFilter === 'pickup'
          ? t('pickup.filters.pickup')
          : t('pickup.filters.shipping')
      });

  // M9: Debounced live search
  const handleSearchChange = (value: string) => {
    setSearchQuery(value);

    // Clear existing timer
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
    }

    // Set new timer for debounced search (500ms)
    debounceTimerRef.current = setTimeout(() => {
      setLoading(true);
      // loadOrders will be called via useEffect when searchQuery changes
    }, 500);
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

      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-muted-foreground">{t('pickup.filterBy')}</span>
        <button
          onClick={() => setFulfillmentFilter('all')}
          className={`px-3 py-2 rounded-lg text-sm font-medium border ${
            fulfillmentFilter === 'all'
              ? 'bg-primary text-primary-foreground border-primary'
              : 'hover:bg-accent'
          }`}
        >
          {t('pickup.filters.all')}
        </button>
        <button
          onClick={() => setFulfillmentFilter('pickup')}
          className={`px-3 py-2 rounded-lg text-sm font-medium border ${
            fulfillmentFilter === 'pickup'
              ? 'bg-primary text-primary-foreground border-primary'
              : 'hover:bg-accent'
          }`}
        >
          {t('pickup.filters.pickup')}
        </button>
        <button
          onClick={() => setFulfillmentFilter('shipping')}
          className={`px-3 py-2 rounded-lg text-sm font-medium border ${
            fulfillmentFilter === 'shipping'
              ? 'bg-primary text-primary-foreground border-primary'
              : 'hover:bg-accent'
          }`}
        >
          {t('pickup.filters.shipping')}
        </button>
      </div>

      {/* M7: KPI Counters */}
      {!loading && (
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-muted/30 p-4 rounded-lg border">
            <div className="text-xs text-muted-foreground uppercase tracking-wide">{t('common.orders') || 'Orders'}</div>
            <div className="text-2xl font-bold mt-2">{filteredOrders.length}</div>
            <div className="text-xs text-muted-foreground mt-1">{t('pickup.title')}</div>
          </div>
          <div className="bg-purple-50 dark:bg-purple-900/20 p-4 rounded-lg border border-purple-200 dark:border-purple-800">
            <div className="text-xs text-purple-700 dark:text-purple-300 uppercase tracking-wide font-medium">{t('common.cylinders') || 'Cylinders'}</div>
            <div className="text-2xl font-bold mt-2 text-purple-700 dark:text-purple-300">
              {filteredOrders.reduce((sum, o) => sum + o.totalCylinders, 0)}
            </div>
            <div className="text-xs text-purple-600 dark:text-purple-400 mt-1">{t('common.total') || 'Total'}</div>
          </div>
          <div className="bg-orange-50 dark:bg-orange-900/20 p-4 rounded-lg border border-orange-200 dark:border-orange-800">
            <div className="text-xs text-orange-700 dark:text-orange-300 uppercase tracking-wide font-medium">{t('common.pending') || 'Pending'}</div>
            <div className="text-2xl font-bold mt-2 text-orange-700 dark:text-orange-300">
              {filteredOrders.reduce((sum, o) => sum + (o.totalCylinders - o.deliveredCylinders), 0)}
            </div>
            <div className="text-xs text-orange-600 dark:text-orange-400 mt-1">{t('common.delivery') || 'Delivery'}</div>
          </div>
        </div>
      )}

      {/* M9: Live search with debounce */}
      <div className="relative">
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => handleSearchChange(e.target.value)}
          placeholder={t('pickup.searchPlaceholder')}
          className="w-full px-4 py-2 border rounded-lg bg-background text-foreground"
          autoComplete="off"
        />
        {searchQuery && (
          <button
            onClick={() => handleSearchChange('')}
            className="absolute right-3 top-1/2 transform -translate-y-1/2 text-muted-foreground hover:text-foreground"
            title={t('common.close')}
          >
            ✕
          </button>
        )}
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

      {/* M11: Inline loading indicator instead of full-screen block */}
      {loading && orders.length === 0 && (
        <div className="flex items-center justify-center gap-2 py-8 text-muted-foreground">
          <span className="inline-block animate-spin">⟳</span>
          {t('common.loading')}
        </div>
      )}

      {/* Empty State */}
      {!loading && filteredOrders.length === 0 && (
        <div className="text-center text-muted-foreground py-12 border-2 border-dashed rounded-lg">
          <div className="text-4xl mb-2">📦</div>
          <div>{emptyMessage}</div>
        </div>
      )}

      {/* Orders List */}
      {!loading && filteredOrders.length > 0 && (
        <div className="space-y-4">
          {/* M8: Sort orders by wait time (longest waiting first), desde que ficou pronta */}
          {[...filteredOrders].sort((a, b) =>
            getWaitTimeMinutes(b.readyAt ?? b.createdAt) - getWaitTimeMinutes(a.readyAt ?? a.createdAt)
          ).map((order) => (
            (() => {
              const isShipping = order.fulfillmentMethod === 'Shipping';
              const undeliveredCount = order.cylinders.filter(c => !c.isDelivered).length;
              const deliveredCount = order.totalCylinders - undeliveredCount;

              return (
            <div key={order.orderId} className={`border rounded-lg overflow-hidden ${order.needsNotification ? 'border-amber-400 dark:border-amber-600' : ''}`}>
              {/* Order Header */}
              <div className="bg-muted/50 p-4 border-b">
                {/* Linha 1: Nome + badges */}
                <div className="flex justify-between items-start gap-2">
                  <button
                    onClick={() => setExpandedOrder(
                      expandedOrder === order.orderId ? null : order.orderId
                    )}
                    className="text-left flex-1 min-w-0"
                  >
                    <div className="font-semibold text-lg truncate">{order.customerName}</div>
                    <div className="text-sm text-muted-foreground">{order.customerPhone}</div>
                    <div className="text-xs text-muted-foreground mt-1">
                      {order.readyAt
                        ? `${t('pickup.readyAt')}: ${formatDate(order.readyAt)}`
                        : `${t('pickup.createdAt')}: ${formatDate(order.createdAt)}`}
                    </div>
                    <div className="flex flex-wrap gap-2 mt-2">
                      <div className={`text-xs px-2 py-1 rounded-full font-medium ${
                        isShipping
                          ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
                          : 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-300'
                      }`}>
                        {isShipping ? 'Envio' : 'Recolha'}
                      </div>
                      {order.refillPaid && (
                        <div className="text-xs px-2 py-1 rounded-full font-medium bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300">
                          Enchimento pago
                        </div>
                      )}
                      {isShipping && order.shippingPaid && (
                        <div className="text-xs px-2 py-1 rounded-full font-medium bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300">
                          Portes pagos
                        </div>
                      )}
                    </div>
                  </button>

                  <div className="flex items-center gap-1.5 shrink-0">
                    {/* M8: Wait time badge */}
                    <div className={`text-xs px-2 py-1 rounded-full whitespace-nowrap font-medium ${getWaitTimeBadgeClass(getWaitTimeMinutes(order.readyAt ?? order.createdAt))}`}>
                      {formatWaitTime(getWaitTimeMinutes(order.readyAt ?? order.createdAt))}
                    </div>
                    <div className="text-xs bg-background px-2 py-1 rounded-full whitespace-nowrap">
                      {t('pickup.progress', { delivered: deliveredCount, total: order.totalCylinders })}
                    </div>
                    {order.needsNotification && (
                      <div className="text-xs px-2 py-1 rounded-full whitespace-nowrap font-medium bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400">
                        !
                      </div>
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

                {/* Linha 2: Botões de acção */}
                <div className="flex items-center gap-2 mt-3">
                  <button
                    onClick={() => openReadyWhatsApp(order)}
                    className={`flex-1 px-3 py-2 text-sm rounded-lg transition-colors whitespace-nowrap font-medium ${
                      order.needsNotification
                        ? 'bg-green-600 hover:bg-green-700 text-white'
                        : 'bg-muted hover:bg-accent text-muted-foreground border'
                    }`}
                    title={t('pickup.sendReadyWhatsApp')}
                  >
                    {order.needsNotification
                      ? `📱 ${t('pickup.sendReadyWhatsApp')}`
                      : `✓ ${t('pickup.readyNotified')}`
                    }
                  </button>

                  {/* M4: Direct deliver button */}
                  {undeliveredCount > 0 && (
                    <button
                      onClick={() => isShipping
                        ? handleShipOrder(order)
                        : setConfirmDeliver({ orderId: order.orderId, count: undeliveredCount })}
                      disabled={actionLoading === order.orderId}
                      className="flex-1 px-3 py-2 text-sm bg-primary hover:opacity-90 text-primary-foreground rounded-lg disabled:opacity-50 transition-colors whitespace-nowrap font-medium"
                      title={t('pickup.completeAndThank')}
                    >
                      {actionLoading === order.orderId ? (
                        <span className="inline-block animate-spin">⟳</span>
                      ) : (
                        `✓ ${t('pickup.completeAndThank')}`
                      )}
                    </button>
                  )}
                </div>
              </div>

              {/* Expanded: Cylinders List + Deliver All Button */}
              {expandedOrder === order.orderId && (
                <div>
                  {/* Cylinders List (read-only) */}
                  <div className="divide-y">
                    {order.cylinders.map((cylinder) => {
                      const hasProblem = cylinder.state === 'Problem';

                      return (
                        <div
                          key={cylinder.cylinderId}
                          className={`p-4 flex items-start gap-3 ${
                            hasProblem
                              ? 'bg-red-50/80 dark:bg-red-950/30 border-l-4 border-red-600'
                              : ''
                          }`}
                        >
                          <div className={`w-10 h-10 rounded-full flex items-center justify-center font-mono text-sm font-bold shrink-0 ${
                            hasProblem
                              ? 'bg-red-600 text-white ring-2 ring-red-200 dark:ring-red-900'
                              : 'bg-primary/10 text-primary'
                          }`}>
                            #{cylinder.sequentialNumber}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className={`text-xs font-medium ${hasProblem ? 'text-red-700 dark:text-red-300' : 'text-muted-foreground'}`}>
                              {t(`cylinder.status.${cylinder.state.toLowerCase()}`)}
                            </div>
                            {hasProblem && cylinder.occurrenceNotes && (
                              <div className="mt-2 rounded-md border border-red-200 bg-red-100 px-3 py-2 text-sm font-medium text-red-800 dark:border-red-900 dark:bg-red-950/70 dark:text-red-200">
                                {cylinder.occurrenceNotes}
                              </div>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>

                  {/* Single Deliver All Button */}
                  {undeliveredCount > 0 && (
                    <div className="p-4 border-t">
                      <button
                        onClick={() => isShipping ? handleShipOrder(order) : handleDeliverAll(order, true)}
                        disabled={actionLoading === order.orderId}
                        className="w-full px-4 py-3 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 transition-colors font-medium text-lg"
                      >
                        {actionLoading === order.orderId ? (
                        <span className="inline-flex items-center gap-2">
                            <span className="inline-block animate-spin">⟳</span>
                            {t('common.loading')}
                          </span>
                        ) : (
                          t('pickup.completeAndThank')
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

            <div className="grid gap-3 pt-2 md:grid-cols-3">
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
                    handleDeliverAll(order, false);
                    setConfirmDeliver(null);
                  }
                }}
                disabled={actionLoading === confirmDeliver.orderId}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
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
              <button
                onClick={() => {
                  const order = orders.find(o => o.orderId === confirmDeliver.orderId);
                  if (order) {
                    handleDeliverAll(order, true);
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
                  t('pickup.completeAndThank')
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
