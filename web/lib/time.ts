export function getWaitTimeMinutes(timestamp: string, now: Date = new Date()): number {
  const start = new Date(timestamp);
  if (Number.isNaN(start.getTime())) {
    return 0;
  }
  return Math.floor((now.getTime() - start.getTime()) / 60000);
}

export function formatWaitTime(waitMinutes: number): string {
  if (waitMinutes >= 1440) {
    const days = Math.floor(waitMinutes / 1440);
    const hours = Math.floor((waitMinutes % 1440) / 60);
    return `${days}d ${hours}h`;
  }

  if (waitMinutes >= 60) {
    const hours = Math.floor(waitMinutes / 60);
    const mins = waitMinutes % 60;
    return `${hours}h ${mins}m`;
  }

  return `${waitMinutes}m`;
}
