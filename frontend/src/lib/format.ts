export function formatDate(date: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(date))
}

export function getTodayDateValue() {
  return new Date().toISOString().slice(0, 10)
}
