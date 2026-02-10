'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { customersApi } from '@/lib/api';

interface Customer {
  customerId: string;
  name: string;
  phone: string;
}

interface CustomerSearchProps {
  onSelect: (customer: Customer) => void;
  disabled?: boolean;
}

export function CustomerSearch({ onSelect, disabled = false }: CustomerSearchProps) {
  const t = useTranslations();
  const [query, setQuery] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSearch = async () => {
    if (!query.trim()) {
      setCustomers([]);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await customersApi.search(query);
      setCustomers(result.customers);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao buscar clientes');
      setCustomers([]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
          placeholder={t('customer.search')}
          className="flex-1 px-4 py-2 border rounded-lg bg-background text-foreground"
        />
        <button
          onClick={handleSearch}
          disabled={loading || disabled}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90 disabled:opacity-50"
        >
          {loading ? t('common.loading') : t('common.search')}
        </button>
      </div>

      {error && (
        <div className="p-3 bg-destructive/10 text-destructive rounded-lg">
          {error}
        </div>
      )}

      {customers.length > 0 && (
        <div className="space-y-2">
          <h3 className="font-semibold">{t('customer.title')}</h3>
          {customers.map((customer) => (
            <button
              key={customer.customerId}
              onClick={() => onSelect(customer)}
              disabled={disabled}
              className="w-full p-4 border rounded-lg hover:bg-accent text-left"
            >
              <div className="font-semibold">{customer.name}</div>
              <div className="text-sm text-muted-foreground">{customer.phone}</div>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
