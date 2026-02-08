'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { customersApi } from '@/lib/api';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
}

interface CreateCustomerFormProps {
  onCreated: (customer: Customer, whatsappWindow?: Window | null) => void;
  onCancel: () => void;
}

export function CreateCustomerForm({ onCreated, onCancel }: CreateCustomerFormProps) {
  const t = useTranslations();
  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    // Open blank window immediately (user gesture context) to avoid popup blocker
    const whatsappWindow = window.open('', '_blank');

    try {
      const customer = await customersApi.create({ name, phone });
      onCreated(customer, whatsappWindow);
    } catch (err) {
      whatsappWindow?.close();
      setError(err instanceof Error ? err.message : 'Erro ao criar cliente');
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label className="block text-sm font-medium mb-1">{t('customer.name')}</label>
        <input
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          className="w-full px-4 py-2 border rounded-lg bg-background text-foreground"
        />
      </div>

      <div>
        <label className="block text-sm font-medium mb-1">{t('customer.phone')}</label>
        <input
          type="tel"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          required
          className="w-full px-4 py-2 border rounded-lg bg-background text-foreground"
        />
      </div>

      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg">
          {error}
        </div>
      )}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={loading}
          className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50"
        >
          {loading ? t('common.loading') : t('common.save')}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2 border rounded-lg hover:bg-accent"
        >
          {t('common.cancel')}
        </button>
      </div>
    </form>
  );
}
