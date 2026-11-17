import { AppSettings, settingsApi } from './api';

export const SETTINGS_UPDATED_EVENT = 'app-settings-updated';

export const DEFAULT_APP_SETTINGS: AppSettings = {
  storeName: 'Oficina da Cerveja',
  storePhone: '',
  storeLink: '',
  appTitle: '',
  whatsAppMessageTemplate: 'Olá {name}! As suas {count} botija(s) de CO₂ estão prontas para recolha. Visite-nos quando puder!',
  shippingReadyMessageTemplate: 'Olá {name}! As suas {count} botija(s) de CO₂ estão prontas e vamos enviar para a morada indicada. {link}',
  welcomeMessageTemplate: 'Obrigado por confiar na Oficina da Cerveja! A sua botija está segura connosco. Visite a nossa loja: {link}',
  thankYouMessageTemplate: 'Obrigado por utilizar o nosso serviço de enchimento. Obrigado, equipa da Oficina da Cerveja!',
  deadlineMessageTemplate: 'Olá {name}! As suas {count} botija(s) de CO₂ já estão prontas e à sua espera há {days} dia(s). Venha buscá-las quando puder!',
  printerType: 'label',
  labelWidthMm: 50,
  labelHeightMm: 75,
  debugEnabled: false,
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

export function formatWhatsAppTemplate(
  template: string,
  params: { name: string; count?: number | string; days?: number | string; link?: string }
): string {
  return template
    .replace(/\{name\}/g, params.name)
    .replace(/\{count\}/g, String(params.count ?? ''))
    .replace(/\{days\}/g, String(params.days ?? ''))
    .replace(/\{link\}/g, params.link ?? '');
}

/** Garante que o texto enviado corresponde à configuração guardada no servidor. */
export async function loadMessagingSettingsForSend(): Promise<AppSettings> {
  return loadAppSettings(true);
}
