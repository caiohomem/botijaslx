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

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    loadOrders();
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('pickup.title')}</h1>

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
          {orders.map((order) => (
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
