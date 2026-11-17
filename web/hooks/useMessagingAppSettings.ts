'use client';

import { useCallback, useEffect, useState } from 'react';
import { AppSettings } from '@/lib/api';
import {
  DEFAULT_APP_SETTINGS,
  SETTINGS_UPDATED_EVENT,
  loadAppSettings,
  loadMessagingSettingsForSend,
} from '@/lib/settings';

export type MessagingTemplates = Pick<
  AppSettings,
  | 'whatsAppMessageTemplate'
  | 'shippingReadyMessageTemplate'
  | 'thankYouMessageTemplate'
  | 'deadlineMessageTemplate'
  | 'welcomeMessageTemplate'
  | 'storeLink'
>;

function templatesFromSettings(settings: AppSettings): MessagingTemplates {
  return {
    whatsAppMessageTemplate: settings.whatsAppMessageTemplate,
    shippingReadyMessageTemplate: settings.shippingReadyMessageTemplate,
    thankYouMessageTemplate: settings.thankYouMessageTemplate,
    deadlineMessageTemplate: settings.deadlineMessageTemplate,
    welcomeMessageTemplate: settings.welcomeMessageTemplate,
    storeLink: settings.storeLink,
  };
}

/**
 * Mantém templates de WhatsApp alinhados com Definições (mesmo separador, outro separador ou após guardar).
 */
export function useMessagingAppSettings() {
  const [templates, setTemplates] = useState<MessagingTemplates>(() =>
    templatesFromSettings(DEFAULT_APP_SETTINGS)
  );

  const apply = useCallback((settings: AppSettings) => {
    setTemplates(templatesFromSettings(settings));
  }, []);

  const reload = useCallback(async () => {
    const settings = await loadAppSettings(true);
    apply(settings);
  }, [apply]);

  useEffect(() => {
    void reload();

    const onUpdated = (event: Event) => {
      const detail = (event as CustomEvent<AppSettings>).detail;
      if (detail) {
        apply(detail);
      } else {
        void reload();
      }
    };

    const onFocus = () => {
      void reload();
    };

    window.addEventListener(SETTINGS_UPDATED_EVENT, onUpdated);
    window.addEventListener('focus', onFocus);
    return () => {
      window.removeEventListener(SETTINGS_UPDATED_EVENT, onUpdated);
      window.removeEventListener('focus', onFocus);
    };
  }, [apply, reload]);

  return {
    templates,
    loadForSend: loadMessagingSettingsForSend,
  };
}