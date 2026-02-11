'use client';

import { useState, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { CustomerSearch } from '@/components/CustomerSearch';
import { CreateCustomerForm } from '@/components/CreateCustomerForm';
import { ordersApi, printJobsApi, cylindersApi, customersApi, historyApi, CustomerCylinder } from '@/lib/api';
import { sendWhatsApp } from '@/lib/whatsapp';
import { QrScanner } from '@/components/QrScanner';
import { LabelPreview, printLabels } from '@/components/LabelPreview';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
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
}

interface KnownCylinder {
  cylinderId: string;
  sequentialNumber: number;
  labelToken: string;
  lastDeliveredAt: string;
}

interface PrintPreviewState {
  printJobId: string;
  quantity: number;
}

export default function DeliveryPage() {
  const t = useTranslations();
  const [step, setStep] = useState<'identify' | 'create' | 'order' | 'complete'>('identify');
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [order, setOrder] = useState<Order | null>(null);
  const [cylinders, setCylinders] = useState<Cylinder[]>([]);
  const [knownCylinders, setKnownCylinders] = useState<KnownCylinder[]>([]);
  const [qrToken, setQrToken] = useState('');
  const [loading, setLoading] = useState(false);
  const [addingKnown, setAddingKnown] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [labelInputs, setLabelInputs] = useState<Record<string, string>>({});
  const [showPrintDialog, setShowPrintDialog] = useState(false);
  const [printQuantity, setPrintQuantity] = useState(1);
  const [printReason, setPrintReason] = useState(''); // M13: Reprint reason
  const [printPreview, setPrintPreview] = useState<PrintPreviewState | null>(null);
  const [lastPrintPreview, setLastPrintPreview] = useState<PrintPreviewState | null>(null);
  const labelsContainerRef = useRef<HTMLDivElement>(null);
  const creatingOrderRef = useRef(false);

  const showSuccess = (msg: string, duration = 2000) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), duration);
  };

  const handlePrintLabels = async () => {
    if (!labelsContainerRef.current) return;
    const canvases = Array.from(labelsContainerRef.current.querySelectorAll('canvas')) as HTMLCanvasElement[];
    if (canvases.length > 0) {
      printLabels(canvases);
    }

    if (!printPreview) return;

    try {
      await printJobsApi.ackPrinted(printPreview.printJobId);

      // Fallback: bind printed labels to any still-unlabeled cylinders from this order.
      const unlabeled = cylinders.filter(c => !c.labelToken);
      for (let i = 0; i < Math.min(unlabeled.length, printPreview.quantity); i++) {
        const cylinder = unlabeled[i];
        const qrToken = `${printPreview.printJobId}-${i + 1}`;
        const assigned = await cylindersApi.assignLabel(cylinder.cylinderId, qrToken);
        setCylinders(prev =>
          prev.map(c =>
            c.cylinderId === cylinder.cylinderId
              ? { ...c, labelToken: assigned.labelToken }
              : c
          )
        );
      }

      showSuccess(t('delivery.printMarkedAsPrinted'));
    } catch {
      // Printing from browser cannot be fully observed; ignore ack failures on UI.
    }
  };

  // Extract known cylinders (delivered in completed orders, with label)
  const extractKnownCylinders = (allCylinders: CustomerCylinder[], currentOrderId: string): KnownCylinder[] => {
    return allCylinders
      .filter(c =>
        c.orderStatus === 'Completed' &&
        c.state === 'Delivered' &&
        c.labelToken
      )
      .map(c => {
        const deliveredEvent = c.history
          .filter(h => h.eventType === 'Delivered')
          .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())[0];
        return {
          cylinderId: c.cylinderId,
          sequentialNumber: c.sequentialNumber,
          labelToken: c.labelToken!,
          lastDeliveredAt: deliveredEvent?.timestamp || c.createdAt,
        };
      })
      // Remove duplicates (same labelToken) keeping most recent
      .filter((c, i, arr) => arr.findIndex(x => x.labelToken === c.labelToken) === i);
  };

  const handleCustomerSelect = async (customer: Customer) => {
    if (creatingOrderRef.current) return;

    creatingOrderRef.current = true;
    setSelectedCustomer(customer);
    setLoading(true);
    setError(null);

    try {
      const newOrder = await ordersApi.create(customer.customerId);
      const customerData = await customersApi.getCylinders(customer.customerId);

      // Cylinders already in this order
      const existingOrderCylinders = customerData.cylinders
        .filter(c => c.orderId === newOrder.orderId)
        .map(c => ({
          cylinderId: c.cylinderId,
          sequentialNumber: c.sequentialNumber,
          labelToken: c.labelToken,
          state: c.state,
        }));

      // Known cylinders from previous completed orders
      const known = extractKnownCylinders(customerData.cylinders, newOrder.orderId);
      // Filter out any that are already in the current order
      const currentIds = new Set(existingOrderCylinders.map(c => c.cylinderId));
      const filteredKnown = known.filter(c => !currentIds.has(c.cylinderId));

      setOrder(newOrder);
      setCylinders(existingOrderCylinders);
      setKnownCylinders(filteredKnown);
      setStep('order');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao criar pedido');
    } finally {
      setLoading(false);
      creatingOrderRef.current = false;
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

  const sendWelcomeWhatsApp = (customer: Customer, whatsappWindow?: Window | null) => {
    try {
      const savedSettings = localStorage.getItem('botijas_settings');
      const settings = savedSettings ? JSON.parse(savedSettings) : {};
      const template = settings.welcomeMessageTemplate || 'Obrigado por confiar na Oficina da Cerveja! A sua botija está segura connosco. Visite a nossa loja: {link}';
      const storeLink = settings.storeLink || '';
      const message = template.replace('{name}', customer.name).replace('{link}', storeLink);
      sendWhatsApp(customer.phone, message, whatsappWindow);
    } catch {
      whatsappWindow?.close();
    }
  };

  const handleCustomerCreated = async (customer: Customer, whatsappWindow?: Window | null) => {
    sendWelcomeWhatsApp(customer, whatsappWindow);
    await handleCustomerSelect(customer);
  };

  const handleScanToken = async (token: string) => {
    if (!order || !token.trim()) return;

    setLoading(true);
    setError(null);

    try {
      const cylinder = await ordersApi.scanCylinder(order.orderId, token.trim());
      setCylinders(prev => [...prev, cylinder]);
      // Remove from known if it was there
      setKnownCylinders(prev => prev.filter(k => k.labelToken !== token.trim()));
      setQrToken('');
      showSuccess(t('delivery.cylinderAdded'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao escanear botija');
    } finally {
      setLoading(false);
    }
  };

  const handleScanQR = () => handleScanToken(qrToken);

  // Add a single known cylinder to the order
  const handleAddKnownCylinder = async (known: KnownCylinder) => {
    if (!order) return;
    setError(null);

    try {
      const cylinder = await ordersApi.scanCylinder(order.orderId, known.labelToken);
      setCylinders(prev => [...prev, cylinder]);
      setKnownCylinders(prev => prev.filter(k => k.cylinderId !== known.cylinderId));
      showSuccess(t('delivery.cylinderAdded'));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao adicionar botija');
    }
  };

  // Add all known cylinders to the order (M0c)
  const handleAddAllKnown = async () => {
    if (!order || knownCylinders.length === 0) return;

    setAddingKnown(true);
    setError(null);
    let addedCount = 0;

    for (const known of knownCylinders) {
      try {
        const cylinder = await ordersApi.scanCylinder(order.orderId, known.labelToken);
        setCylinders(prev => [...prev, cylinder]);
        addedCount++;
      } catch {
        // Skip cylinders that fail (might be in another open order)
      }
    }

    setKnownCylinders([]);
    setAddingKnown(false);

    if (addedCount > 0) {
      showSuccess(t('delivery.knownAdded', { count: addedCount }), 3000);
    }
  };

  const handlePrintAndCreate = async (quantity: number) => {
    if (!order || !selectedCustomer || quantity < 1) return;

    setLoading(true);
    setError(null);

    try {
      // Create print job with customer info
      const printJob = await printJobsApi.create(
        quantity,
        selectedCustomer.name,
        selectedCustomer.phone
      );

      // Create N cylinders in batch (M1)
      const result = await ordersApi.addCylindersBatch(order.orderId, quantity);
      const newCylinders = result.cylinders.map((c: any) => ({
        cylinderId: c.cylinderId,
        sequentialNumber: c.sequentialNumber,
        labelToken: c.labelToken,
        state: c.state,
      }));

      // Assign QR codes to cylinders
      for (let i = 0; i < newCylinders.length; i++) {
        const cylinder = newCylinders[i];
        const qrToken = `${printJob.printJobId}-${i + 1}`;
        const assigned = await cylindersApi.assignLabel(cylinder.cylinderId, qrToken);
        cylinder.labelToken = assigned.labelToken;
      }

      setCylinders([...cylinders, ...newCylinders]);

      setShowPrintDialog(false);
      setPrintQuantity(1);
      const preview = { printJobId: printJob.printJobId, quantity: printJob.quantity };
      setPrintPreview(preview);
      setLastPrintPreview(preview);
      showSuccess(t('delivery.printJobCreatedAndCylinders', { count: quantity }), 3000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao imprimir e criar botijas');
    } finally {
      setLoading(false);
    }
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

  const handleFinishDelivery = () => {
    // M14: Validate completeness before finishing
    if (cylinders.length === 0) {
      setError(t('delivery.noCylinders'));
      return;
    }

    const unlabeledCount = cylinders.filter(c => !c.labelToken).length;
    if (unlabeledCount > 0) {
      setError(t('delivery.needsLabel', { count: unlabeledCount }));
      return;
    }

    setStep('complete');
  };

  const handleNewDelivery = () => {
    setStep('identify');
    setOrder(null);
    setSelectedCustomer(null);
    setCylinders([]);
    setKnownCylinders([]);
    setError(null);
    setSuccessMessage(null);
    setPrintPreview(null);
    setLastPrintPreview(null);
    setPrintReason(''); // M13: Reset reprint reason
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
      {step === 'order' && order && (
        <div className="space-y-6">
          {/* Order Header */}
          <div className="p-4 border rounded-lg bg-muted/30">
            <div className="flex justify-between items-start">
              <div>
                <h2 className="font-semibold text-lg">{selectedCustomer?.name}</h2>
                <div className="text-sm text-muted-foreground">{selectedCustomer?.phone}</div>
              </div>
              <div className="text-right">
                <div className="text-2xl font-bold">{cylinders.length}</div>
                <div className="text-xs text-muted-foreground">{t('order.cylinders')}</div>
              </div>
            </div>
          </div>

          {/* M15: Visual workflow guide */}
          <div className="bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg p-4">
            <div className="text-sm text-amber-900 dark:text-amber-100">
              <div className="font-semibold mb-2 flex items-center gap-2">
                <span>📋 {t('delivery.workflowGuide') || 'Workflow'}</span>
              </div>
              <div className="space-y-1 text-xs">
                <div className="flex items-center gap-2">
                  <span className="bg-amber-300 dark:bg-amber-700 text-amber-900 dark:text-amber-100 rounded-full w-5 h-5 flex items-center justify-center text-xs font-bold">1</span>
                  <span>{t('delivery.step1Scan') || 'Scan cylinders or add without label'}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="bg-amber-300 dark:bg-amber-700 text-amber-900 dark:text-amber-100 rounded-full w-5 h-5 flex items-center justify-center text-xs font-bold">2</span>
                  <span>{t('delivery.step2Print') || 'Print labels for unlabeled cylinders'}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="bg-amber-300 dark:bg-amber-700 text-amber-900 dark:text-amber-100 rounded-full w-5 h-5 flex items-center justify-center text-xs font-bold">3</span>
                  <span>{t('delivery.step3Assign') || 'Assign printed labels to cylinders'}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className="bg-amber-300 dark:bg-amber-700 text-amber-900 dark:text-amber-100 rounded-full w-5 h-5 flex items-center justify-center text-xs font-bold">4</span>
                  <span>{t('delivery.step4Finish') || 'Finish delivery (all cylinders must have labels)'}</span>
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

          {/* Known Cylinders Section (M0c) */}
          {knownCylinders.length > 0 && (
            <div className="border-2 border-blue-200 dark:border-blue-800 rounded-lg overflow-hidden">
              <div className="bg-blue-50 dark:bg-blue-900/30 p-3 border-b border-blue-200 dark:border-blue-800">
                <div className="flex justify-between items-center">
                  <div>
                    <h3 className="font-semibold text-blue-900 dark:text-blue-100">
                      {t('delivery.knownCylinders')} ({knownCylinders.length})
                    </h3>
                    <div className="text-xs text-blue-700 dark:text-blue-300">
                      {t('delivery.knownCylindersDesc')}
                    </div>
                  </div>
                  <button
                    onClick={handleAddAllKnown}
                    disabled={addingKnown}
                    className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg disabled:opacity-50 font-medium text-sm transition-colors"
                  >
                    {addingKnown
                      ? t('delivery.addingKnown')
                      : t('delivery.addAllKnown', { count: knownCylinders.length })
                    }
                  </button>
                </div>
              </div>
              <div className="divide-y">
                {knownCylinders.map((known) => (
                  <div
                    key={known.cylinderId}
                    className="p-3 flex items-center justify-between hover:bg-blue-50/50 dark:hover:bg-blue-900/10 transition-colors"
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-blue-100 dark:bg-blue-800 rounded-full flex items-center justify-center text-blue-600 dark:text-blue-300 font-mono text-sm font-bold">
                        #{known.sequentialNumber}
                      </div>
                      <div>
                        <div className="font-mono text-sm">#{String(known.sequentialNumber).padStart(4, '0')}</div>
                        <div className="text-xs text-muted-foreground">
                          {t('delivery.lastDelivery')}: {formatDate(known.lastDeliveredAt)}
                        </div>
                      </div>
                    </div>
                    <button
                      onClick={() => handleAddKnownCylinder(known)}
                      className="px-3 py-1.5 border border-blue-300 dark:border-blue-700 text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-900/30 rounded-lg text-sm font-medium transition-colors"
                    >
                      +
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Scan Input */}
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
              <div className="grid grid-cols-3 gap-2">
                <button
                  onClick={() => handlePrintAndCreate(1)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  1 {t('order.cylinders')}
                </button>
                <button
                  onClick={() => handlePrintAndCreate(3)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  3 {t('order.cylinders')}
                </button>
                <button
                  onClick={() => handlePrintAndCreate(5)}
                  disabled={loading}
                  className="px-3 py-2 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors text-sm"
                >
                  5 {t('order.cylinders')}
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

            {lastPrintPreview && (
              <button
                onClick={() => setPrintPreview(lastPrintPreview)}
                disabled={loading}
                className="w-full px-4 py-3 border rounded-lg hover:bg-accent disabled:opacity-50 font-medium transition-colors"
              >
                {t('delivery.reprintLastLabels', { count: lastPrintPreview.quantity })}
              </button>
            )}
          </div>

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
                        #{String(cylinder.sequentialNumber).padStart(4, '0')}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {t(`cylinder.status.${uiStatus}`)}
                      </div>
                    </div>
                    <div className={cylinder.labelToken ? 'text-green-600 dark:text-green-400' : 'text-amber-600 dark:text-amber-400'}>
                      {cylinder.labelToken ? '✓' : '🖨️'}
                    </div>
                  </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Action Buttons */}
          <div className="flex gap-3 pt-4 border-t">
            <button
              onClick={handleNewDelivery}
              className="px-4 py-2 border rounded-lg hover:bg-accent"
            >
              {t('common.cancel')}
            </button>
            <button
              onClick={handleFinishDelivery}
              disabled={cylinders.length === 0}
              className="flex-1 px-4 py-3 bg-green-600 hover:bg-green-700 text-white rounded-lg disabled:opacity-50 font-medium"
            >
              {t('delivery.finish')} ({cylinders.length} {t('order.cylinders')})
            </button>
          </div>
        </div>
      )}

      {/* Step 3: Complete */}
      {step === 'complete' && (
        <div className="space-y-6">
          <div className="text-center py-8 border-2 border-green-300 dark:border-green-700 bg-green-50 dark:bg-green-900/20 rounded-lg">
            <div className="text-4xl mb-4">✅</div>
            <h2 className="text-xl font-semibold mb-2">{t('delivery.success')}</h2>
            <div className="text-muted-foreground">
              {t('delivery.successDetail', {
                count: cylinders.length,
                customer: selectedCustomer?.name
              })}
            </div>
          </div>

          {/* Summary */}
          <div className="p-4 border rounded-lg">
            <h3 className="font-semibold mb-3">{t('delivery.summary')}</h3>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('customer.title')}</span>
                <span>{selectedCustomer?.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">{t('order.cylinders')}</span>
                <span>{cylinders.length}</span>
              </div>
            </div>
          </div>

          <button
            onClick={handleNewDelivery}
            className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg hover:opacity-90 font-medium"
          >
            {t('delivery.newDelivery')}
          </button>
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
                onClick={() => handlePrintAndCreate(printQuantity)}
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
              {Array.from({ length: printPreview.quantity }, (_, i) => (
                <LabelPreview
                  key={i}
                  qrContent={`${printPreview.printJobId}-${i + 1}`}
                  customerName={selectedCustomer.name}
                  customerPhone={selectedCustomer.phone}
                  sequentialNumber={i + 1}
                  storeName={(() => {
                    try {
                      const s = localStorage.getItem('botijas_settings');
                      return s ? JSON.parse(s).storeName || 'Oficina da Cerveja' : 'Oficina da Cerveja';
                    } catch { return 'Oficina da Cerveja'; }
                  })()}
                  storeLink={(() => {
                    try {
                      const s = localStorage.getItem('botijas_settings');
                      return s ? JSON.parse(s).storeLink || '' : '';
                    } catch { return ''; }
                  })()}
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
                🖨️ {t('delivery.printLabels', { count: printPreview.quantity })}
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
