'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { CustomerSearch } from '@/components/CustomerSearch';
import { customersApi, cylindersApi, CustomerCylinder } from '@/lib/api';

interface SelectedCustomer {
  customerId: string;
  name: string;
  phone: string;
}

export default function ClientesPage() {
  const t = useTranslations();
  const [selectedCustomer, setSelectedCustomer] = useState<SelectedCustomer | null>(null);
  const [cylinders, setCylinders] = useState<CustomerCylinder[]>([]);
  const [loading, setLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Edit phone state
  const [editingPhone, setEditingPhone] = useState(false);
  const [newPhone, setNewPhone] = useState('');

  // Delete confirmation states
  const [confirmDeleteCylinder, setConfirmDeleteCylinder] = useState<string | null>(null);
  const [confirmDeleteCustomer, setConfirmDeleteCustomer] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const showSuccess = (msg: string) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), 3000);
  };

  const handleSelectCustomer = async (customer: SelectedCustomer) => {
    setSelectedCustomer(customer);
    setLoading(true);
    setError(null);
    setCylinders([]);
    setEditingPhone(false);
    setConfirmDeleteCustomer(false);
    setConfirmDeleteCylinder(null);
    setDeleteError(null);

    try {
      const data = await customersApi.getCylinders(customer.customerId);
      setCylinders(data.cylinders);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar botijas');
    } finally {
      setLoading(false);
    }
  };

  const handleUpdatePhone = async () => {
    if (!selectedCustomer || !newPhone.trim()) return;
    setActionLoading('phone');
    setError(null);

    try {
      const updated = await customersApi.updatePhone(selectedCustomer.customerId, newPhone.trim());
      setSelectedCustomer(prev => (prev ? { ...prev, phone: updated.phone } : null));
      setEditingPhone(false);
      setNewPhone('');
      showSuccess(t('clientes.phoneUpdated'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao atualizar telefone');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeleteCylinder = async (cylinderId: string) => {
    setActionLoading('cylinder');
    setError(null);
    setConfirmDeleteCylinder(null);

    try {
      await cylindersApi.delete(cylinderId);
      setCylinders(prev => prev.filter(c => c.cylinderId !== cylinderId));
      showSuccess(t('clientes.cylinderDeleted'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao eliminar botija');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeleteCustomer = async () => {
    if (!selectedCustomer) return;
    setActionLoading('customer');
    setDeleteError(null);

    try {
      await customersApi.delete(selectedCustomer.customerId);
      setSelectedCustomer(null);
      setCylinders([]);
      setConfirmDeleteCustomer(false);
      showSuccess(t('clientes.customerDeleted'));
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Erro ao eliminar cliente';
      setDeleteError(errorMsg);
      setActionLoading(null);
    }
  };

  const handleClear = () => {
    setSelectedCustomer(null);
    setCylinders([]);
    setError(null);
    setEditingPhone(false);
    setConfirmDeleteCustomer(false);
    setConfirmDeleteCylinder(null);
    setDeleteError(null);
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('clientes.title')}</h1>

      {/* Search Area */}
      {!selectedCustomer && (
        <div className="space-y-4">
          <CustomerSearch
            onSelect={handleSelectCustomer}
            disabled={loading}
          />
          {error && (
            <div className="p-3 bg-destructive/10 text-destructive rounded-lg">{error}</div>
          )}
        </div>
      )}

      {/* Success message */}
      {successMessage && (
        <div className="p-3 bg-green-500/10 text-green-600 dark:text-green-400 rounded-lg">
          {successMessage}
        </div>
      )}

      {/* Customer Detail */}
      {selectedCustomer && (
        <div className="space-y-4">
          {/* Customer Header Card */}
          <div className="p-4 border rounded-lg bg-muted/30 space-y-3">
            <div className="flex justify-between items-start">
              <div className="flex-1">
                <div className="font-semibold text-lg">{selectedCustomer.name}</div>
                {!editingPhone ? (
                  <div className="flex items-center gap-2 mt-1">
                    <span className="text-sm text-muted-foreground">
                      {selectedCustomer.phone}
                    </span>
                    <button
                      onClick={() => {
                        setNewPhone(selectedCustomer.phone);
                        setEditingPhone(true);
                      }}
                      className="text-xs text-primary hover:underline"
                    >
                      {t('clientes.editPhone')}
                    </button>
                  </div>
                ) : (
                  <div className="flex items-center gap-2 mt-2">
                    <input
                      type="tel"
                      value={newPhone}
                      onChange={(e) => setNewPhone(e.target.value)}
                      maxLength={9}
                      className="px-2 py-1 border rounded text-sm bg-background w-32"
                      autoFocus
                    />
                    <button
                      onClick={handleUpdatePhone}
                      disabled={actionLoading === 'phone' || !newPhone.trim()}
                      className="px-3 py-1 bg-primary text-primary-foreground rounded text-sm disabled:opacity-50"
                    >
                      {actionLoading === 'phone' ? '...' : t('clientes.savePhone')}
                    </button>
                    <button
                      onClick={() => {
                        setEditingPhone(false);
                        setNewPhone('');
                      }}
                      className="px-3 py-1 border rounded text-sm hover:bg-accent"
                    >
                      {t('clientes.cancelEdit')}
                    </button>
                  </div>
                )}
              </div>
              <button
                onClick={handleClear}
                className="text-sm text-muted-foreground hover:text-foreground"
              >
                {t('common.back')}
              </button>
            </div>

            {/* Delete customer section */}
            {!confirmDeleteCustomer ? (
              <button
                onClick={() => {
                  setConfirmDeleteCustomer(true);
                  setDeleteError(null);
                }}
                className="text-sm text-destructive hover:underline"
              >
                {t('clientes.deleteCustomer')}
              </button>
            ) : (
              <div className="p-3 border border-destructive rounded-lg bg-destructive/10 space-y-2">
                <p className="text-sm font-medium text-destructive">
                  {t('clientes.confirmDeleteCustomer', { name: selectedCustomer.name })}
                </p>
                {deleteError && (
                  <div className="p-2 bg-destructive/20 text-destructive text-xs rounded">
                    {deleteError}
                  </div>
                )}
                <div className="flex gap-2">
                  <button
                    onClick={handleDeleteCustomer}
                    disabled={actionLoading === 'customer'}
                    className="px-4 py-2 bg-destructive text-destructive-foreground rounded-lg text-sm font-medium disabled:opacity-50"
                  >
                    {actionLoading === 'customer' ? '...' : t('common.delete')}
                  </button>
                  <button
                    onClick={() => {
                      setConfirmDeleteCustomer(false);
                      setDeleteError(null);
                    }}
                    className="px-4 py-2 border rounded-lg text-sm hover:bg-accent"
                  >
                    {t('common.cancel')}
                  </button>
                </div>
              </div>
            )}
          </div>

          {/* Error */}
          {error && (
            <div className="p-3 bg-destructive/10 text-destructive rounded-lg">{error}</div>
          )}

          {/* Cylinders List */}
          {loading && !cylinders.length && (
            <div className="text-center text-muted-foreground py-8">{t('common.loading')}</div>
          )}

          {!loading && (
            <div className="space-y-3">
              <h2 className="font-semibold">
                {t('clientes.cylinders')} ({cylinders.length})
              </h2>

              {cylinders.length === 0 ? (
                <div className="text-center text-muted-foreground py-8 border-2 border-dashed rounded-lg">
                  {t('clientes.noCylinders')}
                </div>
              ) : (
                <div className="space-y-2">
                  {cylinders.map((cylinder) => (
                    <div
                      key={cylinder.cylinderId}
                      className="border rounded-lg p-3 flex items-center justify-between hover:bg-muted/50"
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center text-primary font-mono text-sm font-bold">
                          #{cylinder.sequentialNumber}
                        </div>
                        <div>
                          <div className="font-mono text-sm font-bold">
                            #{String(cylinder.sequentialNumber).padStart(4, '0')}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {cylinder.state} · {cylinder.orderStatus}
                          </div>
                        </div>
                      </div>

                      {/* Delete cylinder */}
                      {confirmDeleteCylinder === cylinder.cylinderId ? (
                        <div className="flex items-center gap-2">
                          <span className="text-xs text-destructive">
                            {t('common.confirm')}?
                          </span>
                          <button
                            onClick={() => handleDeleteCylinder(cylinder.cylinderId)}
                            disabled={actionLoading === 'cylinder'}
                            className="px-2 py-1 bg-destructive text-destructive-foreground rounded text-xs disabled:opacity-50"
                          >
                            {actionLoading === 'cylinder' ? '...' : t('common.delete')}
                          </button>
                          <button
                            onClick={() => setConfirmDeleteCylinder(null)}
                            className="px-2 py-1 border rounded text-xs hover:bg-accent"
                          >
                            {t('common.cancel')}
                          </button>
                        </div>
                      ) : (
                        <button
                          onClick={() => setConfirmDeleteCylinder(cylinder.cylinderId)}
                          className="text-destructive text-sm hover:underline"
                        >
                          {t('clientes.deleteCylinder')}
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
