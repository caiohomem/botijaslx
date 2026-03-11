import { generateWhatsAppLink } from './api';

/**
 * Send a WhatsApp message.
 * Returns true if message was sent/opened successfully.
 */
export async function sendWhatsApp(
  phone: string,
  message: string,
  preOpenedWindow?: Window | null
): Promise<boolean> {
  const link = generateWhatsAppLink(phone, message);
  if (preOpenedWindow) {
    preOpenedWindow.location.href = link;
  } else {
    window.open(link, '_blank');
  }
  return true;
}
