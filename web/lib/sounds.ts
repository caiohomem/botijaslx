// M10: Sound notification utility for critical actions

type SoundType = 'success' | 'warning' | 'error' | 'complete';

// Simple beep sounds using Web Audio API
const createBeep = (frequency: number, duration: number, type: 'sine' | 'square' = 'sine'): Promise<void> => {
  return new Promise((resolve) => {
    try {
      const audioContext = new (window.AudioContext || (window as any).webkitAudioContext)();
      const oscillator = audioContext.createOscillator();
      const gainNode = audioContext.createGain();

      oscillator.connect(gainNode);
      gainNode.connect(audioContext.destination);

      oscillator.frequency.value = frequency;
      oscillator.type = type;

      gainNode.gain.setValueAtTime(0.3, audioContext.currentTime);
      gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + duration / 1000);

      oscillator.start(audioContext.currentTime);
      oscillator.stop(audioContext.currentTime + duration / 1000);

      setTimeout(resolve, duration);
    } catch {
      // Fallback if Web Audio API is not available
      resolve();
    }
  });
};

export const playSound = async (type: SoundType) => {
  try {
    // Check if user has disabled notifications
    const settings = localStorage.getItem('botijas_settings');
    const config = settings ? JSON.parse(settings) : {};
    if (config.soundNotificationsDisabled) return;

    switch (type) {
      case 'success':
        // Two ascending beeps
        await createBeep(800, 100);
        await createBeep(1000, 100);
        break;
      case 'complete':
        // Three ascending beeps for order completion
        await createBeep(700, 120);
        await createBeep(900, 120);
        await createBeep(1100, 120);
        break;
      case 'warning':
        // Single longer beep
        await createBeep(600, 200);
        break;
      case 'error':
        // Low frequency beep
        await createBeep(400, 300);
        break;
    }
  } catch {
    // Silently fail if audio is not available
  }
};
