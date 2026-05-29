const DATE_ONLY_PATTERN = /^\d{4}-\d{2}-\d{2}$/

export function isDateOnlyValue(value: string): boolean {
  return DATE_ONLY_PATTERN.test(value)
}

export function formatLocalDateOnly(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function todayLocalDateOnly(now = new Date()): string {
  return formatLocalDateOnly(now)
}

export function parseDateOnlyToLocalDate(value: string): Date {
  const normalized = normalizeDateOnlyValue(value)
  const [year, month, day] = normalized.split('-').map(Number)

  return new Date(year, month - 1, day)
}

export function normalizeDateOnlyValue(value: string): string {
  if (isDateOnlyValue(value)) {
    return value
  }

  const match = value.match(/^(\d{4}-\d{2}-\d{2})/)
  if (match) {
    return match[1]
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return formatLocalDateOnly(parsed)
}

export function addDaysToDateOnly(value: string, days: number): string {
  const date = parseDateOnlyToLocalDate(value)
  date.setDate(date.getDate() + days)
  return formatLocalDateOnly(date)
}

export function compareDateOnlyValues(left: string, right: string): number {
  const leftValue = normalizeDateOnlyValue(left)
  const rightValue = normalizeDateOnlyValue(right)

  if (leftValue === rightValue) {
    return 0
  }

  return leftValue < rightValue ? -1 : 1
}

export function differenceInDateOnlyDays(start: string, end: string): number {
  const startDate = parseDateOnlyToLocalDate(start)
  const endDate = parseDateOnlyToLocalDate(end)
  const millisecondsPerDay = 86400000

  startDate.setHours(0, 0, 0, 0)
  endDate.setHours(0, 0, 0, 0)

  return Math.round((endDate.getTime() - startDate.getTime()) / millisecondsPerDay)
}

export function startOfWeekDateOnly(
  value: string | Date,
  weekStartsOn: 'sunday' | 'monday' = 'sunday',
): string {
  const date = typeof value === 'string' ? parseDateOnlyToLocalDate(value) : new Date(value)
  const day = date.getDay()
  const diff = weekStartsOn === 'monday' ? (day + 6) % 7 : day

  date.setHours(0, 0, 0, 0)
  date.setDate(date.getDate() - diff)

  return formatLocalDateOnly(date)
}

export function formatDateOnlyForDisplay(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(parseDateOnlyToLocalDate(value))
}

export function toLocalDateOnlyFromDateTime(value: string | Date): string {
  const parsed = value instanceof Date ? new Date(value) : new Date(value)
  return formatLocalDateOnly(parsed)
}
