'use client';

import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';

interface Settings {
  storeName: string;
  storePhone: string;
  storeLink: string;
  appTitle: string;
  whatsappMessageTemplate: string;
  welcomeMessageTemplate: string;
  thankYouMessageTemplate: string;
  labelTemplate: string;
  printerType: 'label' | 'a4';
  labelWidthMm: number;
  labelHeightMm: number;
  whatsappMode: 'manual' | 'api';
  whatsappApiUrl: string;
  whatsappApiToken: string;
  maxPhoneDigits: number;
}

const DEFAULT_SETTINGS: Settings = {
  storeName: 'Oficina da Cerveja',
  storePhone: '',
  storeLink: '',
  appTitle: '',
  whatsappMessageTemplate: 'Olá {name}! As suas {count} botija(s) de CO₂ estão prontas para recolha. Visite-nos quando puder!',
  welcomeMessageTemplate: 'Obrigado por confiar na Oficina da Cerveja! A sua botija está segura connosco. Visite a nossa loja: {link}',
  thankYouMessageTemplate: 'Obrigado por utilizar o nosso serviço de enchimento. Obrigado, equipa da Oficina da Cerveja!',
  labelTemplate: 'default',
  printerType: 'label',
  labelWidthMm: 50,
  labelHeightMm: 75,
  whatsappMode: 'manual',
  whatsappApiUrl: '',
  whatsappApiToken: '',
  maxPhoneDigits: 9,
};

export default function SettingsPage() {
  const t = useTranslations();
  const [settings, setSettings] = useState<Settings>(DEFAULT_SETTINGS);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    // Load settings from localStorage
    const savedSettings = localStorage.getItem('botijas_settings');
    if (savedSettings) {
      try {
        setSettings({ ...DEFAULT_SETTINGS, ...JSON.parse(savedSettings) });
      } catch {
        // Invalid JSON, use defaults
      }
    }
  }, []);

  const handleSave = () => {
    localStorage.setItem('botijas_settings', JSON.stringify(settings));
    // Dispatch storage event so Header picks up the new title
    window.dispatchEvent(new Event('storage'));
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  const handleReset = () => {
    setSettings(DEFAULT_SETTINGS);
    localStorage.removeItem('botijas_settings');
  };

  return (
    <div className="space-y-6 max-w-2xl">
      <h1 className="text-2xl font-bold">{t('settings.title')}</h1>

      {saved && (
        <div className="p-3 bg-green-500/10 text-green-600 dark:text-green-400 rounded-lg">
          {t('settings.saved')}
        </div>
      )}

      {/* Store Info */}
      <div className="p-4 border rounded-lg space-y-4">
        <h2 className="font-semibold">{t('settings.storeInfo')}</h2>
        
        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.appTitle')}</label>
          <input
            type="text"
            value={settings.appTitle}
            onChange={(e) => setSettings({ ...settings, appTitle: e.target.value })}
            placeholder={t('common.appName')}
            className="w-full px-3 py-2 border rounded-lg bg-background"
          />
          <p className="text-xs text-muted-foreground">
            {t('settings.appTitleHelp')}
          </p>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.storeName')}</label>
          <input
            type="text"
            value={settings.storeName}
            onChange={(e) => setSettings({ ...settings, storeName: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg bg-background"
          />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.storePhone')}</label>
          <input
            type="text"
            value={settings.storePhone}
            onChange={(e) => setSettings({ ...settings, storePhone: e.target.value })}
            placeholder="+351..."
            className="w-full px-3 py-2 border rounded-lg bg-background"
          />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.storeLink')}</label>
          <input
            type="url"
            value={settings.storeLink}
            onChange={(e) => setSettings({ ...settings, storeLink: e.target.value })}
            placeholder="https://..."
            className="w-full px-3 py-2 border rounded-lg bg-background"
          />
        </div>
      </div>

      {/* Customer Settings */}
      <div className="p-4 border rounded-lg space-y-4">
        <h2 className="font-semibold">{t('settings.customerSettings')}</h2>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.maxPhoneDigits')}</label>
          <input
            type="number"
            min="1"
            max="20"
            value={settings.maxPhoneDigits}
            onChange={(e) => setSettings({ ...settings, maxPhoneDigits: Math.max(1, parseInt(e.target.value) || 9) })}
            className="w-full px-3 py-2 border rounded-lg bg-background"
          />
          <p className="text-xs text-muted-foreground">
            {t('settings.maxPhoneDigitsHelp')}
          </p>
        </div>
      </div>

      {/* WhatsApp */}
      <div className="p-4 border rounded-lg space-y-4">
        <h2 className="font-semibold">{t('settings.whatsappSettings')}</h2>
        
        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.welcomeMessageTemplate')}</label>
          <textarea
            value={settings.welcomeMessageTemplate}
            onChange={(e) => setSettings({ ...settings, welcomeMessageTemplate: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
          />
          <p className="text-xs text-muted-foreground">
            {t('settings.welcomeTemplateHelp')}
          </p>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.messageTemplate')}</label>
          <textarea
            value={settings.whatsappMessageTemplate}
            onChange={(e) => setSettings({ ...settings, whatsappMessageTemplate: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
          />
          <p className="text-xs text-muted-foreground">
            {t('settings.templateHelp')}
          </p>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.thankYouMessageTemplate')}</label>
          <textarea
            value={settings.thankYouMessageTemplate}
            onChange={(e) => setSettings({ ...settings, thankYouMessageTemplate: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg bg-background h-24 resize-none"
          />
          <p className="text-xs text-muted-foreground">
            {t('settings.thankYouTemplateHelp')}
          </p>
        </div>
      </div>

      {/* WhatsApp Mode */}
      <div className="p-4 border rounded-lg space-y-4">
        <h2 className="font-semibold">{t('settings.whatsappMode')}</h2>

        <div className="space-y-2">
          <div className="flex gap-4">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="whatsappMode"
                value="manual"
                checked={settings.whatsappMode === 'manual'}
                onChange={() => setSettings({ ...settings, whatsappMode: 'manual' })}
              />
              <span className="text-sm">{t('settings.whatsappManual')}</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="whatsappMode"
                value="api"
                checked={settings.whatsappMode === 'api'}
                onChange={() => setSettings({ ...settings, whatsappMode: 'api' })}
              />
              <span className="text-sm">{t('settings.whatsappApi')}</span>
            </label>
          </div>
          <p className="text-xs text-muted-foreground">
            {t('settings.whatsappModeHelp')}
          </p>
        </div>

        {settings.whatsappMode === 'api' && (
          <>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('settings.whatsappApiUrl')}</label>
              <input
                type="url"
                value={settings.whatsappApiUrl}
                onChange={(e) => setSettings({ ...settings, whatsappApiUrl: e.target.value })}
                placeholder="https://graph.facebook.com/v18.0/PHONE_ID/messages"
                className="w-full px-3 py-2 border rounded-lg bg-background text-sm"
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t('settings.whatsappApiToken')}</label>
              <input
                type="password"
                value={settings.whatsappApiToken}
                onChange={(e) => setSettings({ ...settings, whatsappApiToken: e.target.value })}
                placeholder="Bearer token..."
                className="w-full px-3 py-2 border rounded-lg bg-background text-sm"
              />
            </div>
            <p className="text-xs text-muted-foreground">
              {t('settings.whatsappApiHelp')}
            </p>
          </>
        )}
      </div>

      {/* Labels & Printer */}
      <div className="p-4 border rounded-lg space-y-4">
        <h2 className="font-semibold">{t('settings.labelSettings')}</h2>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.printerType')}</label>
          <div className="flex gap-4">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="printerType"
                value="label"
                checked={settings.printerType === 'label'}
                onChange={() => setSettings({ ...settings, printerType: 'label' })}
              />
              <span className="text-sm">{t('settings.printerLabel')}</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="radio"
                name="printerType"
                value="a4"
                checked={settings.printerType === 'a4'}
                onChange={() => setSettings({ ...settings, printerType: 'a4' })}
              />
              <span className="text-sm">{t('settings.printerA4')}</span>
            </label>
          </div>
          <p className="text-xs text-muted-foreground">
            {t('settings.printerTypeHelp')}
          </p>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.labelSize')}</label>
          <div className="flex gap-2 items-center">
            <input
              type="number"
              min="10"
              max="200"
              value={settings.labelWidthMm}
              onChange={(e) => setSettings({ ...settings, labelWidthMm: Math.max(10, parseInt(e.target.value) || 50) })}
              className="w-20 px-3 py-2 border rounded-lg bg-background text-center"
            />
            <span className="text-sm text-muted-foreground">x</span>
            <input
              type="number"
              min="10"
              max="200"
              value={settings.labelHeightMm}
              onChange={(e) => setSettings({ ...settings, labelHeightMm: Math.max(10, parseInt(e.target.value) || 75) })}
              className="w-20 px-3 py-2 border rounded-lg bg-background text-center"
            />
            <span className="text-sm text-muted-foreground">mm</span>
          </div>
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium">{t('settings.labelTemplate')}</label>
          <select
            value={settings.labelTemplate}
            onChange={(e) => setSettings({ ...settings, labelTemplate: e.target.value })}
            className="w-full px-3 py-2 border rounded-lg bg-background"
          >
            <option value="default">{t('settings.labelTemplates.default')}</option>
            <option value="compact">{t('settings.labelTemplates.compact')}</option>
            <option value="large">{t('settings.labelTemplates.large')}</option>
          </select>
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-3">
        <button
          onClick={handleReset}
          className="px-4 py-2 border rounded-lg hover:bg-accent"
        >
          {t('settings.reset')}
        </button>
        <button
          onClick={handleSave}
          className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:opacity-90"
        >
          {t('settings.save')}
        </button>
      </div>
    </div>
  );
}
