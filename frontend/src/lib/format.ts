import { formatDateOnlyForDisplay, isDateOnlyValue, todayLocalDateOnly } from './dateOnly'

export function formatDate(date: string) {
  if (isDateOnlyValue(date)) {
    return formatDateOnlyForDisplay(date)
  }

  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(date))
}

export function getTodayDateValue() {
  return todayLocalDateOnly()
}
