'use client';

import { useEffect, useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { CustomerSearch } from '@/components/CustomerSearch';
import { DebugCustomerSnapshot, debugApi } from '@/lib/api';
import { loadAppSettings } from '@/lib/settings';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
}

export default function DebugPage() {
  const t = useTranslations();
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const [mode, setMode] = useState<'customer' | 'all'>('customer');
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [snapshot, setSnapshot] = useState<DebugCustomerSnapshot | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [debugEnabled, setDebugEnabled] = useState(false);
  const [settingsLoaded, setSettingsLoaded] = useState(false);

  useEffect(() => {
    loadAppSettings().then((settings) => {
      setDebugEnabled(settings.debugEnabled);
      setSettingsLoaded(true);
    });
  }, []);

  const loadSnapshot = async (customer: Customer) => {
    setMode('customer');
    setSelectedCustomer(customer);
    setLoading(true);
    setError(null);
    setSuccessMessage(null);

    try {
      const result = await debugApi.getCustomerSnapshot(customer.customerId);
      setSnapshot(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar debug');
    } finally {
      setLoading(false);
    }
  };

  const loadFullSnapshot = async () => {
    setMode('all');
    setSelectedCustomer(null);
    setLoading(true);
    setError(null);
    setSuccessMessage(null);

    try {
      const result = await debugApi.getFullSnapshot();
      setSnapshot(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar debug');
    } finally {
      setLoading(false);
    }
  };

  const refresh = async () => {
    if (mode === 'all') {
      await loadFullSnapshot();
      return;
    }

    if (!selectedCustomer) return;
    await loadSnapshot(selectedCustomer);
  };

  const handleDelete = async (action: () => Promise<void>, options?: { clearAfterDelete?: boolean }) => {
    setLoading(true);
    setError(null);
    setSuccessMessage(null);

    try {
      await action();
      if (options?.clearAfterDelete) {
        setSelectedCustomer(null);
        setSnapshot(null);
      } else {
        await refresh();
      }
      setSuccessMessage(t('debug.deleteSuccess'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao eliminar linha');
    } finally {
      setLoading(false);
    }
  };

  const handleExport = async () => {
    setLoading(true);
    setError(null);
    setSuccessMessage(null);

    try {
      const exported = await debugApi.exportDatabase();
      const blob = new Blob([JSON.stringify(exported, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      link.href = url;
      link.download = `botijas-debug-export-${timestamp}.json`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      setSuccessMessage(t('debug.exportSuccess'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao exportar base');
    } finally {
      setLoading(false);
    }
  };

  const handleImportClick = () => {
    importInputRef.current?.click();
  };

  const handleImportFile = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;

    if (!window.confirm(t('debug.importConfirm'))) {
      return;
    }

    setLoading(true);
    setError(null);
    setSuccessMessage(null);

    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as DebugCustomerSnapshot;
      await debugApi.importDatabase(parsed);
      setMode('all');
      setSelectedCustomer(null);
      const refreshed = await debugApi.getFullSnapshot();
      setSnapshot(refreshed);
      setSuccessMessage(t('debug.importSuccess'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao importar snapshot');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      {!settingsLoaded ? (
        <div className="p-4 border rounded-lg text-muted-foreground">
          {t('common.loading')}
        </div>
      ) : null}

      {settingsLoaded && !debugEnabled ? (
        <div className="p-4 border rounded-lg text-muted-foreground">
          {t('debug.disabled')}
        </div>
      ) : null}

      {settingsLoaded && debugEnabled ? (
        <>
      <div>
        <h1 className="text-2xl font-bold">{t('debug.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('debug.subtitle')}</p>
      </div>

      <div className="flex gap-2">
        <button
          onClick={() => setMode('customer')}
          className={`px-4 py-2 rounded-lg font-medium ${mode === 'customer' ? 'bg-primary text-primary-foreground' : 'border hover:bg-accent'}`}
        >
          {t('dashboard.searchByCustomer')}
        </button>
        <button
          onClick={loadFullSnapshot}
          disabled={loading}
          className={`px-4 py-2 rounded-lg font-medium ${mode === 'all' ? 'bg-primary text-primary-foreground' : 'border hover:bg-accent'} disabled:opacity-50`}
        >
          {t('debug.fullDatabase')}
        </button>
        <button
          onClick={handleExport}
          disabled={loading}
          className="px-4 py-2 rounded-lg font-medium border hover:bg-accent disabled:opacity-50"
        >
          {t('debug.exportDatabase')}
        </button>
        <button
          onClick={handleImportClick}
          disabled={loading}
          className="px-4 py-2 rounded-lg font-medium border hover:bg-accent disabled:opacity-50"
        >
          {t('debug.importDatabase')}
        </button>
        <input
          ref={importInputRef}
          type="file"
          accept="application/json"
          className="hidden"
          onChange={handleImportFile}
        />
      </div>

      {mode === 'customer' && (
        <CustomerSearch
          onSelect={loadSnapshot}
          disabled={loading}
        />
      )}

      {mode === 'customer' && selectedCustomer && !snapshot && !loading && (
        <div className="p-4 border rounded-lg text-muted-foreground">
          {t('debug.selectCustomer')}
        </div>
      )}

      {loading && (
        <div className="p-4 border rounded-lg text-muted-foreground">
          {t('debug.loading')}
        </div>
      )}

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

      {snapshot && (
        <div className="space-y-6">
          <div className="flex items-center justify-between gap-3">
            <div className="p-4 border rounded-lg bg-muted/20 flex-1">
              {snapshot.customer ? (
                <>
                  <div className="font-semibold">{snapshot.customer.name}</div>
                  <div className="text-sm text-muted-foreground">{snapshot.customer.phone}</div>
                  <div className="text-xs text-muted-foreground mt-1">{snapshot.customer.customerId}</div>
                </>
              ) : (
                <>
                  <div className="font-semibold">{t('debug.fullDatabase')}</div>
                  <div className="text-sm text-muted-foreground">
                    {snapshot.customers?.length ?? 0} {t('navigation.customers')}
                  </div>
                </>
              )}
            </div>
            <button
              onClick={refresh}
              disabled={loading}
              className="px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
            >
              {t('debug.refresh')}
            </button>
          </div>

          <DebugSection
            title={t('debug.customer')}
            rows={snapshot.customer ? [snapshot.customer] : (snapshot.customers ?? [])}
            getKey={(row) => row.customerId}
            onDelete={(row) => handleDelete(() => debugApi.deleteCustomer(row.customerId), { clearAfterDelete: true })}
            deleteLabel={t('debug.deleteRow')}
          />

          <DebugSection
            title={t('debug.orders')}
            rows={snapshot.orders}
            getKey={(row) => row.orderId}
            onDelete={(row) => handleDelete(() => debugApi.deleteOrder(row.orderId))}
            deleteLabel={t('debug.deleteRow')}
          />

          <DebugSection
            title={t('debug.cylinderRefs')}
            rows={snapshot.cylinderRefs}
            getKey={(row) => `${row.orderId}-${row.cylinderId}`}
            onDelete={(row) => handleDelete(() => debugApi.deleteCylinderRef(row.orderId, row.cylinderId))}
            deleteLabel={t('debug.deleteRow')}
          />

          <DebugSection
            title={t('debug.cylinders')}
            rows={snapshot.cylinders}
            getKey={(row) => row.cylinderId}
            onDelete={(row) => handleDelete(() => debugApi.deleteCylinder(row.cylinderId))}
            deleteLabel={t('debug.deleteRow')}
          />

          <DebugSection
            title={t('debug.cylinderHistory')}
            rows={snapshot.cylinderHistory}
            getKey={(row) => row.id}
            onDelete={(row) => handleDelete(() => debugApi.deleteCylinderHistory(row.id))}
            deleteLabel={t('debug.deleteRow')}
          />

          <DebugSection
            title={t('debug.appSettings')}
            rows={snapshot.appSettings ?? []}
            getKey={(row) => row.appSettingsId}
            deleteLabel={t('debug.deleteRow')}
          />

          <div className="p-4 border rounded-lg bg-muted/20">
            <h2 className="font-semibold mb-2">{t('debug.omittedTables')}</h2>
            <ul className="text-sm text-muted-foreground space-y-1">
              {snapshot.omittedTables.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </div>
        </div>
      )}
        </>
      ) : null}
    </div>
  );
}

function DebugSection<T extends Record<string, unknown>>({
  title,
  rows,
  getKey,
  onDelete,
  deleteLabel,
}: {
  title: string;
  rows: T[];
  getKey: (row: T) => string;
  onDelete?: (row: T) => void;
  deleteLabel: string;
}) {
  return (
    <div className="space-y-3">
      <h2 className="font-semibold">{title} ({rows.length})</h2>
      {rows.length === 0 ? (
        <div className="p-4 border rounded-lg text-muted-foreground">0 rows</div>
      ) : (
        <div className="space-y-2">
          {rows.map((row) => (
            <div key={getKey(row)} className="p-4 border rounded-lg">
              <div className="flex justify-between items-start gap-4">
                <pre className="text-xs whitespace-pre-wrap break-all overflow-x-auto flex-1">
                  {JSON.stringify(row, null, 2)}
                </pre>
                {onDelete ? (
                  <button
                    onClick={() => onDelete(row)}
                    className="px-3 py-2 border border-red-300 text-red-600 rounded-lg hover:bg-red-50"
                  >
                    {deleteLabel}
                  </button>
                ) : null}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
