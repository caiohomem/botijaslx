'use client';

import { useLocaleContext } from './LocaleProvider';
import { locales, type Locale } from '@/i18n';
import { useTranslations } from 'next-intl';

export function LanguageSelector() {
  const { locale, setLocale } = useLocaleContext();
  const t = useTranslations('language');

  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setLocale(e.target.value as Locale);
  };

  return (
    <select
      value={locale}
      onChange={handleChange}
      className="px-3 py-2 rounded-lg border bg-background text-foreground"
      aria-label={t('select')}
    >
      {locales.map((loc) => (
        <option key={loc} value={loc}>
          {loc === 'pt-PT' ? t('pt') : t('en')}
        </option>
      ))}
    </select>
  );
}
