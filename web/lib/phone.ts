export type PhoneMode = 'pt' | 'international';

export function sanitizePhoneDigits(value: string): string {
  return value.replace(/\D/g, '');
}

export function getPhoneMaxDigits(mode: PhoneMode): number {
  return mode === 'pt' ? 9 : 14;
}

export function detectPhoneMode(phone: string): PhoneMode {
  const digits = sanitizePhoneDigits(phone);
  if (digits.length === 9 && !digits.startsWith('351')) {
    return 'pt';
  }

  return 'international';
}

export function clampPhoneDigits(value: string, mode: PhoneMode): string {
  return sanitizePhoneDigits(value).slice(0, getPhoneMaxDigits(mode));
}

export function formatPhoneForWhatsApp(phone: string): string {
  let digits = sanitizePhoneDigits(phone);

  if (digits.startsWith('00')) {
    digits = digits.slice(2);
  }

  if (digits.length === 9) {
    return `351${digits}`;
  }

  return digits;
}

export function formatPhoneForWhatsAppByType(phone: string, mode: PhoneMode): string {
  let digits = sanitizePhoneDigits(phone);

  if (digits.startsWith('00')) {
    digits = digits.slice(2);
  }

  if (mode === 'pt') {
    return digits.startsWith('351') ? digits : `351${digits}`;
  }

  return digits;
}
