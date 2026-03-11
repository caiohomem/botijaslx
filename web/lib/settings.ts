import { AppSettings, settingsApi } from './api';

export const SETTINGS_UPDATED_EVENT = 'app-settings-updated';

export const DEFAULT_APP_SETTINGS: AppSettings = {
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
  maxPhoneDigits: 9,
  soundNotificationsDisabled: false,
  updatedAt: '',
};

let cachedSettings: AppSettings | null = null;

export async function loadAppSettings(forceRefresh = false): Promise<AppSettings> {
  if (!forceRefresh && cachedSettings) {
    return cachedSettings;
  }

  try {
    const settings = await settingsApi.get();
    cachedSettings = { ...DEFAULT_APP_SETTINGS, ...settings };
    return cachedSettings;
  } catch {
    cachedSettings = { ...DEFAULT_APP_SETTINGS };
    return cachedSettings;
  }
}

export async function saveAppSettings(settings: AppSettings): Promise<AppSettings> {
  const saved = await settingsApi.update(settings);
  cachedSettings = { ...DEFAULT_APP_SETTINGS, ...saved };

  if (typeof window !== 'undefined') {
    window.dispatchEvent(new CustomEvent(SETTINGS_UPDATED_EVENT, { detail: cachedSettings }));
  }

  return cachedSettings;
}

export function getCachedAppSettings(): AppSettings {
  return cachedSettings ?? { ...DEFAULT_APP_SETTINGS };
}
