'use client';

import { useState, useRef, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import { CustomerSearch } from '@/components/CustomerSearch';
import { CreateCustomerForm } from '@/components/CreateCustomerForm';
import { AppSettings, ordersApi, cylindersApi, customersApi, historyApi, generateWhatsAppLink, CustomerCylinder } from '@/lib/api';
import { QrScanner } from '@/components/QrScanner';
import { LabelPreview, printLabels } from '@/components/LabelPreview';
import { DEFAULT_APP_SETTINGS, loadAppSettings } from '@/lib/settings';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
  phoneType: string;
}

interface Order {
  orderId: string;
  customerId: string;
  status: string;
  createdAt: string;
  cylinderCount: number;
}

interface Cylinder {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  isDraftNew?: boolean;
}

interface CustomerCylinderListItem {
  cylinderId: string;
  sequentialNumber: number;
  labelToken?: string;
  state: string;
  orderStatus: string;
  orderId: string;
  lastEventAt: string;
  availableForEntry: boolean;
}

interface CustomerOrderHistoryItem {
  orderId: string;
  orderStatus: string;
  createdAt: string;
  cylinderCount: number;
  deliveredCount: number;
}

interface PrintPreviewState {
  labels: Array<{
    qrContent: string;
    sequentialNumber: number;
  }>;
}

function getCustomerCylinderSortKey(cylinder: CustomerCylinder): number {
  const latestHistoryTimestamp = cylinder.history
    .map((item) => new Date(item.timestamp).getTime())
    .filter((value) => !Number.isNaN(value))
    .sort((a, b) => b - a)[0];

  return latestHistoryTimestamp ?? new Date(cylinder.createdAt).getTime();
}

function getUniqueCustomerCylinders(allCylinders: CustomerCylinder[]): CustomerCylinder[] {
  const unique = new Map<string, CustomerCylinder>();

  for (const cylinder of allCylinders) {
    const existing = unique.get(cylinder.cylinderId);
    if (!existing || getCustomerCylinderSortKey(cylinder) > getCustomerCylinderSortKey(existing)) {
      unique.set(cylinder.cylinderId, cylinder);
    }
  }

  return Array.from(unique.values());
}

export default function DeliveryPage() {
  const t = useTranslations();
  const [step, setStep] = useState<'identify' | 'create' | 'order'>('identify');
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [order, setOrder] = useState<Order | null>(null);
  const [cylinders, setCylinders] = useState<Cylinder[]>([]);
  const [customerCylinders, setCustomerCylinders] = useState<CustomerCylinderListItem[]>([]);
  const [customerOrders, setCustomerOrders] = useState<CustomerOrderHistoryItem[]>([]);
  const [qrToken, setQrToken] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [labelInputs, setLabelInputs] = useState<Record<string, string>>({});
  const [showPrintDialog, setShowPrintDialog] = useState(false);
  const [printQuantity, setPrintQuantity] = useState(1);
  const [printReason, setPrintReason] = useState(''); // M13: Reprint reason
  const [printPreview, setPrintPreview] = useState<PrintPreviewState | null>(null);
  const [settings, setSettings] = useState<AppSettings>(DEFAULT_APP_SETTINGS);
  const labelsContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    loadAppSettings().then(setSettings);
  }, []);

  const tr = (key: string, fallback: string) => {
    const value = t(key);
    return value === key ? fallback : value;
  };

  const showSuccess = (msg: string, duration = 2000) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), duration);
  };

  const handlePrintLabels = async () => {
    if (!labelsContainerRef.current) return;
    const canvases = Array.from(labelsContainerRef.current.querySelectorAll('canvas')) as HTMLCanvasElement[];
    if (canvases.length > 0) {
      printLabels(canvases, settings);
    }

    try {
      showSuccess(t('delivery.printMarkedAsPrinted'));
    } catch {
      // Printing from browser cannot be fully observed; ignore ack failures on UI.
    }
  };

  const mapCustomerCylinders = (allCylinders: CustomerCylinder[]): CustomerCylinderListItem[] => {
    return getUniqueCustomerCylinders(allCylinders)
      .map((cylinder) => {
        const latestHistory = [...cylinder.history]
          .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())[0];

        return {
          cylinderId: cylinder.cylinderId,
          sequentialNumber: cylinder.sequentialNumber,
          labelToken: cylinder.labelToken,
          state: cylinder.state,
          orderStatus: cylinder.orderStatus,
          orderId: cylinder.orderId,
          lastEventAt: latestHistory?.timestamp || cylinder.createdAt,
          availableForEntry: cylinder.state === 'Delivered',
        };
      })
      .sort((a, b) => a.sequentialNumber - b.sequentialNumber);
  };

  const mapCustomerOrders = (allCylinders: CustomerCylinder[]): CustomerOrderHistoryItem[] => {
    const grouped = new Map<string, CustomerOrderHistoryItem>();

    for (const cylinder of getUniqueCustomerCylinders(allCylinders)) {
      const existing = grouped.get(cylinder.orderId);
      if (existing) {
        existing.cylinderCount += 1;
        if (cylinder.state === 'Delivered') {
          existing.deliveredCount += 1;
        }
        continue;
      }

      grouped.set(cylinder.orderId, {
        orderId: cylinder.orderId,
        orderStatus: cylinder.orderStatus,
        createdAt: cylinder.createdAt,
        cylinderCount: 1,
        deliveredCount: cylinder.state === 'Delivered' ? 1 : 0,
      });
    }

    return Array.from(grouped.values())
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  };

  const handleCustomerSelect = async (customer: Customer) => {
    setSelectedCustomer(customer);
    setLoading(true);
    setError(null);

    try {
      const customerData = await customersApi.getCylinders(customer.customerId);
      setOrder(null);
      setCylinders([]);
      setCustomerCylinders(mapCustomerCylinders(customerData.cylinders));
      setCustomerOrders(mapCustomerOrders(customerData.cylinders));
      setStep('order');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar botijas do cliente');
    } finally {
      setLoading(false);
    }
  };

  // Scan bottle to identify customer (M0b)
  const handleIdentifyScan = async (token: string) => {
    if (!token.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const cylinderData = await historyApi.scanCylinder(token.trim());

      if (cylinderData.customerName && cylinderData.customerPhone) {
        // We need the customerId - search for the customer by phone
        const searchResult = await customersApi.search(cylinderData.customerPhone);
        const matched = searchResult.customers.find(
          c => c.phone === cylinderData.customerPhone
        );

        if (matched) {
          showSuccess(t('customer.identifiedViaBottle'), 3000);
          await handleCustomerSelect(matched);
          return;
        }
      }

      setError(t('customer.notFound'));
    } catch {
      setError(t('customer.notFound'));
    } finally {
      setLoading(false);
    }
  };

  const openWelcomeWhatsApp = (customer: Customer) => {
    const template = settings.welcomeMessageTemplate || DEFAULT_APP_SETTINGS.welcomeMessageTemplate;
    const link = settings.storeLink || '';
    const message = template.replace('{name}', customer.name).replace('{link}', link);
    const waLink = generateWhatsAppLink(customer.phone, message, customer.phoneType === 'International' ? 'international' : 'pt');
    window.open(waLink, '_blank');
  };

  const handleCustomerCreated = async (customer: Customer) => {
    await handleCustomerSelect(customer);
  };

  const addCylinderToDraft = (cylinder: { cylinderId: string; sequentialNumber: number; labelToken?: string }) => {
    setError(null);

    setCylinders(prev => {
      if (prev.some(c => c.cylinderId === cylinder.cylinderId)) {
        return prev;
      }

      return [...prev, {
        cylinderId: cylinder.cylinderId,
        sequentialNumber: cylinder.sequentialNumber,
        labelToken: cylinder.labelToken,
        state: 'Delivered',
      }];
    });

    showSuccess(t('delivery.cylinderAdded'));
  };

  const handleScanToken = async (token: string) => {
    if (!selectedCustomer || !token.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const cylinderData = await historyApi.scanCylinder(token.trim());
      const customerCylinder = customerCylinders.find(c =>
        c.cylinderId === cylinderData.cylinderId ||
        c.labelToken === token.trim()
      );

      if (!customerCylinder || !customerCylinder.availableForEntry) {
        throw new Error('Botija não está disponível para entrada neste cliente');
      }

      addCylinderToDraft({
        cylinderId: customerCylinder.cylinderId,
        sequentialNumber: customerCylinder.sequentialNumber,
        labelToken: customerCylinder.labelToken,
      });
      setQrToken('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao escanear botija');
    } finally {
      setLoading(false);
    }
  };

  const handleScanQR = () => handleScanToken(qrToken);

  const handleAddNewDrafts = (quantity: number) => {
    if (quantity < 1) return;

    const drafts = Array.from({ length: quantity }, (_, index) => ({
      cylinderId: `draft-new-${Date.now()}-${index}`,
      sequentialNumber: 0,
      state: 'Received',
      isDraftNew: true,
    }));

    setCylinders(prev => [...prev, ...drafts]);
    setShowPrintDialog(false);
    setPrintQuantity(1);
    showSuccess(t('delivery.cylinderAddedNoLabel'), 3000);
  };

  const handleRemoveDraftCylinder = (cylinderId: string) => {
    setCylinders(prev => prev.filter(c => c.cylinderId !== cylinderId));
  };

  const handleAssignLabel = async (cylinderId: string) => {
    const qrToken = labelInputs[cylinderId]?.trim();
    if (!qrToken) return;

    setLoading(true);
    setError(null);

    try {
      const result = await cylindersApi.assignLabel(cylinderId, qrToken);
      setCylinders(prev => prev.map(c =>
        c.cylinderId === cylinderId
          ? { ...c, labelToken: result.labelToken }
          : c
      ));
      setLabelInputs(prev => {
        const newInputs = { ...prev };
        delete newInputs[cylinderId];
        return newInputs;
      });
      showSuccess(t('delivery.labelAssigned'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao atribuir etiqueta');
    } finally {
      setLoading(false);
    }
  };

  const handleReprintCylinder = (cylinder: Cylinder) => {
    if (!cylinder.labelToken || cylinder.isDraftNew) return;

    setPrintPreview({
      labels: [{
        qrContent: cylinder.labelToken,
        sequentialNumber: cylinder.sequentialNumber,
      }],
    });
  };

  const handleNewDelivery = () => {
    setStep('identify');
    setOrder(null);
    setSelectedCustomer(null);
    setCylinders([]);
    setCustomerCylinders([]);
    setCustomerOrders([]);
    setError(null);
    setSuccessMessage(null);
    setPrintPreview(null);
    setPrintReason(''); // M13: Reset reprint reason
  };

  const handleCloseOrder = async () => {
    if (!selectedCustomer) return;
    if (cylinders.length === 0) {
      setError(t('delivery.noCylinders'));
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const newOrder = await ordersApi.create(selectedCustomer.customerId);
      const finalizedCylinders: Cylinder[] = [];

      for (const cylinder of cylinders.filter(c => !c.isDraftNew)) {
        const added = await ordersApi.addCylinder(newOrder.orderId, cylinder.cylinderId);
        finalizedCylinders.push({ ...added });
      }

      const newDraftCount = cylinders.filter(c => c.isDraftNew).length;
      if (newDraftCount > 0) {
        const result = await ordersApi.addCylindersBatch(newOrder.orderId, newDraftCount);
        const newCylinders = result.cylinders.map((c: any) => ({
          cylinderId: c.cylinderId,
          sequentialNumber: c.sequentialNumber,
          labelToken: c.labelToken,
          state: c.state,
        }));

        for (let i = 0; i < newCylinders.length; i++) {
          const createdCylinder = newCylinders[i];
          const qrCode = `${newOrder.orderId}-${i + 1}`;
          const assigned = await cylindersApi.assignLabel(createdCylinder.cylinderId, qrCode);
          createdCylinder.labelToken = assigned.labelToken;
        }

        finalizedCylinders.push(...newCylinders);
        setPrintPreview({
          labels: newCylinders.map((c: Cylinder, index: number) => ({
            qrContent: `${newOrder.orderId}-${index + 1}`,
            sequentialNumber: c.sequentialNumber,
          })),
        });
      }

      setOrder(newOrder);
      setCylinders(finalizedCylinders);
      showSuccess(tr('delivery.orderClosed', 'Pedido fechado'), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao fechar pedido');
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('pt-PT', {
      day: '2-digit',
      month: '2-digit',
    });
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('navigation.delivery')}</h1>

      {/* Step 1: Identify Customer (unified: scan + search + create) */}
      {(step === 'identify' || step === 'create') && (
        <div className="space-y-4">
          {step === 'identify' && (
            <>
              {/* Scan bottle to identify customer */}
              <QrScanner
                onScan={handleIdentifyScan}
                label={t('customer.scanToIdentify')}
              />

              {/* Typeahead search */}
              <CustomerSearch
                onSelect={handleCustomerSelect}
                onCreateNew={() => setStep('create')}
                disabled={loading}
              />
            </>
          )}

          {step === 'create' && (
            <CreateCustomerForm
              onCreated={handleCustomerCreated}
              onCancel={() => setStep('identify')}
            />
          )}

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
        </div>
      )}

      {/* Step 2: Add Cylinders to Order */}
      {step === 'order' && (
        <div className="space-y-6">
          {/* Order Header */}
          <div className="p-4 border rounded-lg bg-muted/30">
            <div className="flex justify-between items-start">
              <div>
                <h2 className="font-semibold text-lg">{selectedCustomer?.name}</h2>
                <div className="text-sm text-muted-foreground">{selectedCustomer?.phone}</div>
              </div>
              <div className="flex items-center gap-3">
                {selectedCustomer && (
                  <button
                    onClick={() => openWelcomeWhatsApp(selectedCustomer)}
                    className="px-3 py-1 text-sm bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors whitespace-nowrap font-medium"
                  >
                    {t('pickup.sendWhatsApp')}
                  </button>
                )}
                <div className="text-right">
                  <div className="text-2xl font-bold">{cylinders.length}</div>
                  <div className="text-xs text-muted-foreground">{t('order.cylinders')}</div>
                </div>
              </div>
            </div>
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

          {!order && customerOrders.length > 0 && (
            <div className="space-y-3">
              <div>
                <h3 className="font-semibold">
                  {tr('delivery.customerOrderHistory', 'Histórico de pedidos')} ({customerOrders.length})
                </h3>
                <div className="text-sm text-muted-foreground">
                  {tr('delivery.customerOrderHistoryHelp', 'Resumo dos pedidos anteriores deste cliente')}
                </div>
              </div>
              <div className="space-y-2">
                {customerOrders.map((customerOrder) => (
                  <div key={customerOrder.orderId} className="p-3 border rounded-lg bg-muted/20">
                    <div className="flex items-center justify-between gap-3">
                      <div>
                        <div className="font-mono text-sm font-bold">
                          #{customerOrder.orderId.slice(0, 8)}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {formatDate(customerOrder.createdAt)}
                        </div>
                      </div>
                      <div className="text-right">
                        <div className="text-sm font-medium">
                          {t(`order.status.${customerOrder.orderStatus.toLowerCase()}`)}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {customerOrder.deliveredCount}/{customerOrder.cylinderCount} {t('order.cylinders')}
                        </div>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {!order && customerCylinders.length > 0 && (
            <div className="space-y-3">
              <div>
                <h3 className="font-semibold">
                  {tr('delivery.customerCylinders', 'Botijas do cliente')} ({customerCylinders.length})
                </h3>
                <div className="text-sm text-muted-foreground">
                  {tr('delivery.customerCylindersHelp', 'Selecione as botijas entregues que quer dar entrada neste pedido')}
                </div>
              </div>

              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                {customerCylinders.map((cylinder) => {
                  const selected = cylinders.some(c => c.cylinderId === cylinder.cylinderId);
                  const available = cylinder.availableForEntry;
                  const label = available
                    ? tr('delivery.availableForEntry', 'Disponível para entrada')
                    : tr('delivery.notAvailableForEntry', 'Indisponível');

                  return (
                    <button
                      key={cylinder.cylinderId}
                      type="button"
                      onClick={() => available && addCylinderToDraft({
                        cylinderId: cylinder.cylinderId,
                        sequentialNumber: cylinder.sequentialNumber,
                        labelToken: cylinder.labelToken,
                      })}
                      disabled={!available || selected || loading}
                      className={`p-3 border rounded-lg text-left transition-colors ${
                        selected
                          ? 'border-green-500 bg-green-50 dark:bg-green-900/20'
                          : available
                            ? 'hover:bg-accent border-blue-200 dark:border-blue-800'
                            : 'opacity-60 border-muted bg-muted/20 cursor-not-allowed'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <div className="font-mono font-bold">
                            #{String(cylinder.sequentialNumber).padStart(4, '0')}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            {t(`cylinder.status.${cylinder.state.toLowerCase()}`)}
                          </div>
                        </div>
                        <div className={`text-xs font-medium px-2 py-1 rounded-full ${
                          selected
                            ? 'bg-green-600 text-white'
                            : available
                              ? 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
                              : 'bg-muted text-muted-foreground'
                        }`}>
                          {selected ? tr('delivery.selectedForOrder', 'Selecionada') : label}
                        </div>
                      </div>
                      <div className="mt-2 text-xs text-muted-foreground">
                        {tr('delivery.lastMovement', 'Último movimento')}: {formatDate(cylinder.lastEventAt)}
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {!order && (
          <div className="space-y-3">
            <QrScanner
              onScan={(code) => handleScanToken(code)}
              label={t('delivery.scanCamera')}
            />
            <div className="flex gap-2">
              <input
                type="text"
                value={qrToken}
                onChange={(e) => setQrToken(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleScanQR()}
                placeholder={t('delivery.scanPlaceholder')}
                className="flex-1 px-4 py-3 border rounded-lg bg-background text-foreground text-lg"
                disabled={loading}
                autoFocus
              />
              <button
                onClick={handleScanQR}
                disabled={loading || !qrToken.trim()}
                className="px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50 font-medium"
              >
                OK
              </button>
            </div>

            {/* Quick print buttons (M2) */}
            <div className="space-y-2">
              <h4 className="text-sm font-semibold text-muted-foreground">{t('delivery.quickPrint')}</h4>
              <div className="grid grid-cols-4 gap-2">
                <button
                  onClick={() => handleAddNewDrafts(1)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  1 {t('order.cylinders')}
                </button>
                <button
                  onClick={() => handleAddNewDrafts(2)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  2 {t('order.cylinders')}
                </button>
                <button
                  onClick={() => handleAddNewDrafts(3)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  3 {t('order.cylinders')}
                </button>
                <button
                  onClick={() => handleAddNewDrafts(4)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  4 {t('order.cylinders')}
                </button>
              </div>
              <button
                onClick={() => setShowPrintDialog(true)}
                disabled={loading}
                className="w-full px-4 py-2 border border-amber-500 text-amber-600 hover:bg-amber-50 dark:hover:bg-amber-900/20 rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
              >
                {t('delivery.customizeQuantity')}
              </button>
            </div>
          </div>
          )}

          {/* Cylinders List */}
          {cylinders.length > 0 && (
            <div className="space-y-3">
              <h3 className="font-semibold">{t('order.cylinders')} ({cylinders.length})</h3>
              <div className="space-y-2">
                {cylinders.map((cylinder, index) => {
                  const uiStatus = cylinder.state.toLowerCase() === 'received' && cylinder.labelToken
                    ? 'printed'
                    : cylinder.state.toLowerCase();

                  return (
                  <div
                    key={cylinder.cylinderId}
                    className={`p-3 border rounded-lg flex items-center gap-3 ${
                      cylinder.labelToken
                        ? ''
                        : 'border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/20'
                    }`}
                  >
                    <div className={`w-10 h-10 rounded-full flex items-center justify-center font-mono text-sm font-bold ${
                      cylinder.labelToken
                        ? 'bg-primary/10 text-primary'
                        : 'bg-amber-100 dark:bg-amber-800 text-amber-600 dark:text-amber-400'
                    }`}>
                      #{cylinder.sequentialNumber}
                    </div>
                    <div className="flex-1">
                      <div className="text-sm font-mono font-bold">
                        {cylinder.isDraftNew
                          ? tr('delivery.newCylinderDraft', 'Nova botija')
                          : `#${String(cylinder.sequentialNumber).padStart(4, '0')}`}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {cylinder.isDraftNew
                          ? tr('delivery.pendingPrint', 'Aguardando impressão')
                          : t(`cylinder.status.${uiStatus}`)}
                      </div>
                    </div>
                    <div className={cylinder.labelToken ? 'text-green-600 dark:text-green-400' : 'text-amber-600 dark:text-amber-400'}>
                      {cylinder.labelToken ? '✓' : '🖨️'}
                    </div>
                    {!order && (
                      <button
                        onClick={() => handleRemoveDraftCylinder(cylinder.cylinderId)}
                        disabled={loading}
                        className="px-3 py-1.5 border border-red-500 text-red-500 rounded-lg hover:bg-red-500/10 disabled:opacity-50 text-sm font-medium transition-colors"
                      >
                        X
                      </button>
                    )}
                    {order && cylinder.labelToken && (
                      <button
                        onClick={() => handleReprintCylinder(cylinder)}
                        disabled={loading}
                        className="px-3 py-1.5 border rounded-lg hover:bg-accent disabled:opacity-50 text-sm font-medium transition-colors"
                      >
                        {t('delivery.reprintLabel')}
                      </button>
                    )}
                  </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex gap-3 pt-4 border-t">
            {!order && (
              <button
                onClick={handleCloseOrder}
                disabled={loading || cylinders.length === 0}
                className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50 font-medium"
              >
                {tr('delivery.closeOrder', 'Fechar Pedido')}
              </button>
            )}
            <button
              onClick={handleNewDelivery}
              className="px-4 py-2 border rounded-lg hover:bg-accent"
            >
              {t('common.back')}
            </button>
          </div>
        </div>
      )}

      {/* Print Dialog */}
      {showPrintDialog && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
          <div className="bg-background p-6 rounded-lg shadow-lg w-full max-w-sm space-y-4">
            <h3 className="text-lg font-semibold">{t('delivery.printNewLabels')}</h3>
            <p className="text-sm text-muted-foreground">{t('delivery.howManyLabels')}</p>
            <div className="flex items-center gap-4">
              <button
                onClick={() => setPrintQuantity(Math.max(1, printQuantity - 1))}
                className="w-10 h-10 border rounded-lg text-lg font-bold hover:bg-accent"
              >
                -
              </button>
              <input
                type="number"
                min="1"
                value={printQuantity}
                onChange={(e) => setPrintQuantity(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-20 text-center text-2xl font-bold border rounded-lg bg-background py-2"
              />
              <button
                onClick={() => setPrintQuantity(printQuantity + 1)}
                className="w-10 h-10 border rounded-lg text-lg font-bold hover:bg-accent"
              >
                +
              </button>
            </div>

            {/* M13: Reprint reason field */}
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('delivery.reprintReason') || 'Reason for reprint'}</label>
              <select
                value={printReason}
                onChange={(e) => setPrintReason(e.target.value)}
                className="w-full px-3 py-2 border rounded-lg bg-background"
              >
                <option value="">{t('delivery.selectReason') || 'Select reason...'}</option>
                <option value="damage">{t('delivery.reprintReasons.damage') || 'Labels damaged'}</option>
                <option value="quality">{t('delivery.reprintReasons.quality') || 'Print quality issues'}</option>
                <option value="lost">{t('delivery.reprintReasons.lost') || 'Labels lost'}</option>
                <option value="customer_request">{t('delivery.reprintReasons.customer_request') || 'Customer requested'}</option>
                <option value="other">{t('delivery.reprintReasons.other') || 'Other'}</option>
              </select>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                onClick={() => { setShowPrintDialog(false); setPrintQuantity(1); setPrintReason(''); }}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={() => handleAddNewDrafts(printQuantity)}
                disabled={loading}
                className="flex-1 px-4 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium"
              >
                {t('delivery.printLabels', { count: printQuantity })}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Label Preview Dialog */}
      {printPreview && selectedCustomer && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-background rounded-lg shadow-lg w-full max-w-lg max-h-[90vh] overflow-y-auto p-6 space-y-4">
            <h3 className="text-lg font-semibold">{t('delivery.labelPreview')}</h3>
            <div ref={labelsContainerRef} className="space-y-4 flex flex-col items-center">
              {Array.from({ length: printPreview.labels.length }, (_, i) => (
                <LabelPreview
                  key={i}
                  qrContent={printPreview.labels[i]?.qrContent ?? ''}
                  customerName={selectedCustomer.name}
                  customerPhone={selectedCustomer.phone}
                  sequentialNumber={printPreview.labels[i]?.sequentialNumber ?? i + 1}
                  storeName={settings.storeName}
                  storeLink={settings.storeLink}
                  settings={settings}
                />
              ))}
            </div>
            <div className="flex gap-3">
              <button
                onClick={() => setPrintPreview(null)}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.close')}
              </button>
              <button
                onClick={handlePrintLabels}
                className="flex-1 px-4 py-3 bg-amber-500 hover:bg-amber-600 text-white rounded-lg font-medium flex items-center justify-center gap-2"
              >
                🖨️ {t('delivery.printLabels', { count: printPreview.labels.length })}
              </button>
            </div>
          </div>
        </div>
      )}

      {loading && (
        <div className="fixed inset-0 bg-black/20 flex items-center justify-center z-40">
          <div className="bg-background p-4 rounded-lg shadow-lg">
            {t('common.loading')}
          </div>
        </div>
      )}
    </div>
  );
}
