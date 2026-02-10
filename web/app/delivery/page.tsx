'use client';

import { useState, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { CustomerSearch } from '@/components/CustomerSearch';
import { CreateCustomerForm } from '@/components/CreateCustomerForm';
import { ordersApi, printJobsApi, cylindersApi } from '@/lib/api';
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
  labelToken?: string;
  state: string;
}

interface PrintPreviewState {
  printJobId: string;
  quantity: number;
}

export default function DeliveryPage() {
  const t = useTranslations();
  const [step, setStep] = useState<'search' | 'create' | 'order' | 'complete'>('search');
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [order, setOrder] = useState<Order | null>(null);
  const [cylinders, setCylinders] = useState<Cylinder[]>([]);
  const [qrToken, setQrToken] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [labelInputs, setLabelInputs] = useState<Record<string, string>>({});
  const [showPrintDialog, setShowPrintDialog] = useState(false);
  const [printQuantity, setPrintQuantity] = useState(1);
  const [printPreview, setPrintPreview] = useState<PrintPreviewState | null>(null);
  const [lastPrintPreview, setLastPrintPreview] = useState<PrintPreviewState | null>(null);
  const labelsContainerRef = useRef<HTMLDivElement>(null);
  const creatingOrderRef = useRef(false);

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

      setSuccessMessage(t('delivery.printMarkedAsPrinted'));
      setTimeout(() => setSuccessMessage(null), 2000);
    } catch {
      // Printing from browser cannot be fully observed; ignore ack failures on UI.
    }
  };

  const handleCustomerSelect = async (customer: Customer) => {
    if (creatingOrderRef.current) {
      return;
    }

    creatingOrderRef.current = true;
    setSelectedCustomer(customer);
    setLoading(true);
    setError(null);

    try {
      const newOrder = await ordersApi.create(customer.customerId);
      setOrder(newOrder);
      setCylinders([]);
      setStep('order');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao criar pedido');
    } finally {
      setLoading(false);
      creatingOrderRef.current = false;
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
      setQrToken('');
      setSuccessMessage(t('delivery.cylinderAdded'));
      setTimeout(() => setSuccessMessage(null), 2000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao escanear botija');
    } finally {
      setLoading(false);
    }
  };

  const handleScanQR = () => handleScanToken(qrToken);

  const handlePrintAndCreate = async () => {
    if (!order || !selectedCustomer || printQuantity < 1) return;

    setLoading(true);
    setError(null);

    try {
      // Create print job with customer info
      const printJob = await printJobsApi.create(
        printQuantity,
        selectedCustomer.name,
        selectedCustomer.phone
      );

      // Create N cylinders in the order
      const newCylinders: Cylinder[] = [];
      for (let i = 0; i < printQuantity; i++) {
        const cylinder = await ordersApi.addCylinder(order.orderId);
        const qrToken = `${printJob.printJobId}-${i + 1}`;
        const assigned = await cylindersApi.assignLabel(cylinder.cylinderId, qrToken);
        newCylinders.push({ ...cylinder, labelToken: assigned.labelToken });
      }
      setCylinders([...cylinders, ...newCylinders]);

      setShowPrintDialog(false);
      setPrintQuantity(1);
      const preview = { printJobId: printJob.printJobId, quantity: printJob.quantity };
      setPrintPreview(preview);
      setLastPrintPreview(preview);
      setSuccessMessage(t('delivery.printJobCreatedAndCylinders', { count: printQuantity }));
      setTimeout(() => setSuccessMessage(null), 3000);
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
      // Atualizar a botija na lista com a nova etiqueta
      setCylinders(prev => prev.map(c => 
        c.cylinderId === cylinderId 
          ? { ...c, labelToken: result.labelToken }
          : c
      ));
      // Limpar input
      setLabelInputs(prev => {
        const newInputs = { ...prev };
        delete newInputs[cylinderId];
        return newInputs;
      });
      setSuccessMessage(t('delivery.labelAssigned'));
      setTimeout(() => setSuccessMessage(null), 2000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao atribuir etiqueta');
    } finally {
      setLoading(false);
    }
  };

  const handleFinishDelivery = () => {
    if (cylinders.length === 0) {
      setError(t('delivery.noCylinders'));
      return;
    }
    setStep('complete');
  };

  const handleNewDelivery = () => {
    setStep('search');
    setOrder(null);
    setSelectedCustomer(null);
    setCylinders([]);
    setError(null);
    setSuccessMessage(null);
    setPrintPreview(null);
    setLastPrintPreview(null);
  };

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">{t('navigation.delivery')}</h1>

      {/* Step 1: Search or Create Customer */}
      {(step === 'search' || step === 'create') && (
        <div className="space-y-6">
          <div className="flex gap-2">
            <button
              onClick={() => setStep('search')}
              className={`px-4 py-2 rounded-lg ${
                step === 'search'
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t('customer.search')}
            </button>
            <button
              onClick={() => setStep('create')}
              className={`px-4 py-2 rounded-lg ${
                step === 'create'
                  ? 'bg-primary text-primary-foreground'
                  : 'border hover:bg-accent'
              }`}
            >
              {t('customer.create')}
            </button>
          </div>

          {step === 'search' && <CustomerSearch onSelect={handleCustomerSelect} disabled={loading} />}
          {step === 'create' && (
            <CreateCustomerForm
              onCreated={handleCustomerCreated}
              onCancel={() => setStep('search')}
            />
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

            <button
              onClick={() => setShowPrintDialog(true)}
              disabled={loading}
              className="w-full px-4 py-3 bg-amber-500 hover:bg-amber-600 text-white rounded-lg disabled:opacity-50 font-medium transition-colors"
            >
              {t('delivery.printNewLabels')}
            </button>
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
                    <div className={`w-10 h-10 rounded-full flex items-center justify-center font-mono text-sm ${
                      cylinder.labelToken
                        ? 'bg-primary/10 text-primary'
                        : 'bg-amber-100 dark:bg-amber-800 text-amber-600 dark:text-amber-400'
                    }`}>
                      {cylinder.labelToken ? cylinder.labelToken.slice(0, 4) : `#${index + 1}`}
                    </div>
                    <div className="flex-1">
                      <div className={`text-sm ${cylinder.labelToken ? 'font-mono' : 'text-muted-foreground italic'}`}>
                        {cylinder.labelToken || t('delivery.pendingPrint')}
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
            <div className="flex gap-3 pt-2">
              <button
                onClick={() => { setShowPrintDialog(false); setPrintQuantity(1); }}
                className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={handlePrintAndCreate}
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
