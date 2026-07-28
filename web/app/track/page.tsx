'use client';

import { FormEvent, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { LanguageSelector } from '@/components/LanguageSelector';
import { ThemeToggle } from '@/components/ThemeToggle';
import { trackingApi, TrackingOrder, TrackingResult } from '@/lib/api';

function statusColor(status: string): string {
  switch (status) {
    case 'Open':
      return 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300';
    case 'ReadyForPickup':
      return 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-300';
    case 'Completed':
      return 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300';
    case 'Cancelled':
      return 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300';
    default:
      return 'bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300';
  }
}

function formatDate(value?: string, locale = 'pt-PT'): string {
  if (!value) return '';
  try {
    return new Date(value).toLocaleString(locale, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return value;
  }
}

function OrderCard({ order, locale }: { order: TrackingOrder; locale: string }) {
  const t = useTranslations('tracking');
  const tOrder = useTranslations('order');

  return (
    <div className="border rounded-lg p-4 space-y-3 bg-background">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm text-muted-foreground">
          {t('orderFrom', { date: formatDate(order.createdAt, locale) })}
        </span>
        <span className={`px-3 py-1 rounded-full text-xs font-medium ${statusColor(order.status)}`}>
          {tOrder(`status.${order.status.toLowerCase()}`)}
        </span>
      </div>

      <div className="text-sm text-muted-foreground">
        {order.fulfillmentMethod === 'Shipping'
          ? t('fulfillment.shipping')
          : t('fulfillment.pickup')}
      </div>

      {order.status === 'Completed' ? (
        <div className="text-sm font-medium">
          {t('deliveredProgress', { delivered: order.deliveredCylinders, total: order.totalCylinders })}
        </div>
      ) : (
        <div className="text-sm font-medium">
          {t('cylinderProgress', { ready: order.readyCylinders + order.deliveredCylinders, total: order.totalCylinders })}
        </div>
      )}

      <div className="w-full h-2 rounded-full bg-muted overflow-hidden">
        <div
          className="h-full bg-primary transition-all"
          style={{
            width: `${
              order.totalCylinders > 0
                ? Math.round(((order.readyCylinders + order.deliveredCylinders) / order.totalCylinders) * 100)
                : 0
            }%`,
          }}
        />
      </div>

      <div className="flex flex-wrap gap-2 text-xs">
        {order.receivedCylinders > 0 ? (
          <span className="px-2 py-1 rounded-full bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300">
            {t('counts.received')}: {order.receivedCylinders}
          </span>
        ) : null}
        {order.readyCylinders > 0 ? (
          <span className="px-2 py-1 rounded-full bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-300">
            {t('counts.ready')}: {order.readyCylinders}
          </span>
        ) : null}
        {order.problemCylinders > 0 ? (
          <span className="px-2 py-1 rounded-full bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-300">
            {t('counts.problem')}: {order.problemCylinders}
          </span>
        ) : null}
        {order.deliveredCylinders > 0 ? (
          <span className="px-2 py-1 rounded-full bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300">
            {t('counts.delivered')}: {order.deliveredCylinders}
          </span>
        ) : null}
      </div>

      {order.status === 'ReadyForPickup' && order.readyAt ? (
        <p className="text-sm text-muted-foreground">{t('readySince', { date: formatDate(order.readyAt, locale) })}</p>
      ) : null}
      {order.status === 'Completed' && order.completedAt ? (
        <p className="text-sm text-muted-foreground">{t('completedAt', { date: formatDate(order.completedAt, locale) })}</p>
      ) : null}
      {order.shippedAt ? (
        <p className="text-sm text-muted-foreground">{t('shippedAt', { date: formatDate(order.shippedAt, locale) })}</p>
      ) : null}
      {order.status === 'Cancelled' && order.cancelledAt ? (
        <p className="text-sm text-muted-foreground">{t('cancelledAt', { date: formatDate(order.cancelledAt, locale) })}</p>
      ) : null}
    </div>
  );
}

export default function TrackPage() {
  const t = useTranslations('tracking');
  const tCommon = useTranslations('common');
  const [phone, setPhone] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<TrackingResult | null>(null);

  const locale = useMemo(() => {
    if (typeof window === 'undefined') return 'pt-PT';
    return localStorage.getItem('locale') || 'pt-PT';
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    const digits = phone.replace(/\D/g, '');
    if (digits.length < 9) {
      setError(t('invalidPhone'));
      return;
    }

    setLoading(true);
    setError(null);
    setResult(null);

    try {
      const data = await trackingApi.getStatus(digits);
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('invalidPhone'));
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setResult(null);
    setError(null);
    setPhone('');
  };

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="flex justify-end gap-2 p-4">
        <LanguageSelector />
        <ThemeToggle />
      </div>

      <div className="container mx-auto px-4 pb-12 max-w-lg">
        <div className="text-center space-y-2 mb-8">
          <h1 className="text-2xl font-bold">{t('title')}</h1>
          <p className="text-muted-foreground">{t('subtitle')}</p>
        </div>

        {!result && (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <label htmlFor="phone" className="text-sm font-medium">
                {t('phoneLabel')}
              </label>
              <input
                id="phone"
                type="tel"
                inputMode="numeric"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder={t('phonePlaceholder')}
                className="w-full px-4 py-3 border rounded-lg bg-background text-lg"
                autoFocus
                required
              />
            </div>

            {error && <p className="text-sm text-destructive">{error}</p>}

            <button
              type="submit"
              disabled={loading}
              className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg font-medium hover:opacity-90 disabled:opacity-50 transition-opacity"
            >
              {loading ? t('searching') : t('submit')}
            </button>
          </form>
        )}

        {result && (
          <div className="space-y-4">
            {result.orders.length === 0 || !result.customerName ? (
              <p className="text-center text-muted-foreground py-8">{t('notFound')}</p>
            ) : (
              <>
                <p className="text-lg font-semibold">{t('greeting', { name: result.customerName })}</p>
                <p className="text-sm text-muted-foreground">
                  {t('ordersFound', { count: result.orders.length })}
                </p>
                <div className="space-y-3">
                  {result.orders.map((order, index) => (
                    <OrderCard key={index} order={order} locale={locale} />
                  ))}
                </div>
              </>
            )}

            <button
              onClick={handleReset}
              className="w-full px-4 py-3 border rounded-lg font-medium hover:bg-accent transition-colors"
            >
              {t('backHome')}
            </button>
          </div>
        )}

        <p className="text-center text-xs text-muted-foreground mt-10">{tCommon('appName')}</p>
      </div>
    </div>
  );
}
