import { generateWhatsAppLink } from './api';

interface WhatsAppSettings {
  whatsappMode?: 'manual' | 'api';
  whatsappApiUrl?: string;
  whatsappApiToken?: string;
}

function getSettings(): WhatsAppSettings {
  try {
    const saved = localStorage.getItem('botijas_settings');
    return saved ? JSON.parse(saved) : {};
  } catch {
    return {};
  }
}

/**
 * Send a WhatsApp message.
 * - In 'manual' mode: opens wa.me link (user clicks send).
 * - In 'api' mode: sends via WhatsApp Business Cloud API automatically.
 *
 * Returns true if message was sent/opened successfully.
 */
export async function sendWhatsApp(
  phone: string,
  message: string,
  preOpenedWindow?: Window | null
): Promise<boolean> {
  const settings = getSettings();

  if (settings.whatsappMode === 'api' && settings.whatsappApiUrl && settings.whatsappApiToken) {
    // Close pre-opened window if we're using API mode
    preOpenedWindow?.close();

    return sendViaApi(phone, message, settings.whatsappApiUrl, settings.whatsappApiToken);
  }

  // Manual mode: open wa.me link
  const link = generateWhatsAppLink(phone, message);
  if (preOpenedWindow) {
    preOpenedWindow.location.href = link;
  } else {
    window.open(link, '_blank');
  }
  return true;
}

async function sendViaApi(
  phone: string,
  message: string,
  apiUrl: string,
  apiToken: string
): Promise<boolean> {
  const cleanPhone = phone.replace(/\D/g, '');

  try {
    const response = await fetch(apiUrl, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${apiToken}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        messaging_product: 'whatsapp',
        to: cleanPhone,
        type: 'text',
        text: { body: message },
      }),
    });

    if (!response.ok) {
      console.error('WhatsApp API error:', response.status, await response.text());
      return false;
    }

    return true;
  } catch (err) {
    console.error('WhatsApp API error:', err);
    return false;
  }
}
