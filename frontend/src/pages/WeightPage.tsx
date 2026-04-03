import { useEffect, useMemo, useState } from 'react'
import {
  createWeightEntry,
  deleteWeightEntry,
  fetchWeightEntries,
  updateWeightEntry,
} from '../api/weightEntries'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { WeightEntry } from '../types/weight'

type FormState = {
  date: string
  weightKg: string
}

type FormErrors = Partial<Record<keyof FormState, string>>

const initialFormState = (): FormState => ({
  date: new Date().toISOString().slice(0, 10),
  weightKg: '',
})

function validateForm(form: FormState): FormErrors {
  const errors: FormErrors = {}

  if (!form.date) {
    errors.date = 'Date is required.'
  } else if (form.date > new Date().toISOString().slice(0, 10)) {
    errors.date = 'Date cannot be in the future.'
  }

  if (!form.weightKg.trim()) {
    errors.weightKg = 'Weight is required.'
  } else {
    const parsedWeight = Number(form.weightKg)

    if (Number.isNaN(parsedWeight)) {
      errors.weightKg = 'Enter a valid number.'
    } else if (parsedWeight < 20 || parsedWeight > 500) {
      errors.weightKg = 'Weight must be between 20 and 500 kg.'
    }
  }

  return errors
}

export function WeightPage() {
  const [entries, setEntries] = useState<WeightEntry[]>([])
  const [form, setForm] = useState<FormState>(initialFormState)
  const [errors, setErrors] = useState<FormErrors>({})
  const [editingId, setEditingId] = useState<number | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadEntries()
  }, [])

  const stats = useMemo(() => {
    const latestEntry = entries[0]
    const previousEntry = entries[1]
    const delta =
      latestEntry && previousEntry
        ? Number((latestEntry.weightKg - previousEntry.weightKg).toFixed(1))
        : null
    const weeklyAverages = buildWeeklyAverages(entries)
    const latestWeek = weeklyAverages[0]
    const previousWeek = weeklyAverages[1]
    const weeklyDelta =
      latestWeek && previousWeek
        ? Number((latestWeek.averageWeightKg - previousWeek.averageWeightKg).toFixed(1))
        : null
    const trendLabel =
      weeklyDelta === null || weeklyDelta === 0
        ? 'Stable'
        : weeklyDelta > 0
          ? 'Increasing'
          : 'Decreasing'

    return {
      latestEntry,
      totalEntries: entries.length,
      delta,
      latestWeek,
      weeklyAverages,
      weeklyDelta,
      trendLabel,
    }
  }, [entries])

  const chartData = useMemo(() => {
    return [...entries]
      .sort((left, right) => new Date(left.date).getTime() - new Date(right.date).getTime())
      .map((entry) => ({
        ...entry,
        shortDate: new Intl.DateTimeFormat(undefined, {
          month: 'short',
          day: 'numeric',
        }).format(new Date(entry.date)),
      }))
  }, [entries])

  async function loadEntries() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      const data = await fetchWeightEntries()
      setEntries(data)
    } catch {
      setErrorMessage('Unable to load weight entries. Check that the API is running.')
    } finally {
      setIsLoading(false)
    }
  }

  function resetForm() {
    setForm(initialFormState())
    setErrors({})
    setEditingId(null)
  }

  function startEdit(entry: WeightEntry) {
    setEditingId(entry.id)
    setErrors({})
    setFeedback(null)
    setForm({
      date: entry.date.slice(0, 10),
      weightKg: entry.weightKg.toString(),
    })
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const nextErrors = validateForm(form)
    setErrors(nextErrors)
    setFeedback(null)
    setErrorMessage(null)

    if (Object.keys(nextErrors).length > 0) {
      return
    }

    const payload = {
      date: form.date,
      weightKg: Number(form.weightKg),
    }

    try {
      setIsSaving(true)

      if (editingId === null) {
        const createdEntry = await createWeightEntry(payload)
        setEntries((currentEntries) => [createdEntry, ...currentEntries].sort(compareEntries))
        setFeedback('Weight entry added.')
      } else {
        const updatedEntry = await updateWeightEntry(editingId, payload)
        setEntries((currentEntries) =>
          currentEntries.map((entry) => (entry.id === editingId ? updatedEntry : entry)).sort(compareEntries),
        )
        setFeedback('Weight entry updated.')
      }

      resetForm()
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to save this entry.'))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDelete(entry: WeightEntry) {
    const confirmed = window.confirm(`Delete the entry for ${formatDate(entry.date)}?`)
    if (!confirmed) {
      return
    }

    try {
      setErrorMessage(null)
      setFeedback(null)
      await deleteWeightEntry(entry.id)
      setEntries((currentEntries) => currentEntries.filter((currentEntry) => currentEntry.id !== entry.id))

      if (editingId === entry.id) {
        resetForm()
      }

      setFeedback('Weight entry deleted.')
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to delete this entry.'))
    }
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Weight</h1>
          <p className="hero-text">
            Record weigh-ins, correct mistakes, and keep a clean history without leaving the page.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Latest</span>
            <strong>{stats.latestEntry ? `${stats.latestEntry.weightKg} kg` : 'No data'}</strong>
            <span className="stat-subtext">
              {stats.latestEntry ? formatDate(stats.latestEntry.date) : 'Add your first entry'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Entries</span>
            <strong>{stats.totalEntries}</strong>
            <span className="stat-subtext">Tracked weigh-ins</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Change</span>
            <strong>
              {stats.delta === null ? 'N/A' : `${stats.delta > 0 ? '+' : ''}${stats.delta} kg`}
            </strong>
            <span className="stat-subtext">Compared with previous entry</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Weekly Average</span>
            <strong>
              {stats.latestWeek ? `${stats.latestWeek.averageWeightKg} kg` : 'No data'}
            </strong>
            <span className="stat-subtext">
              {stats.latestWeek ? stats.latestWeek.label : 'Need weigh-ins this week'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Trend</span>
            <strong className={getTrendClassName(stats.weeklyDelta)}>{stats.trendLabel}</strong>
            <span className="stat-subtext">
              {stats.weeklyDelta === null
                ? 'Need two weeks of data'
                : `${stats.weeklyDelta > 0 ? '+' : ''}${stats.weeklyDelta} kg vs previous week`}
            </span>
          </article>
        </div>
      </section>

      <section className="content-grid">
        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>{editingId === null ? 'Add entry' : 'Edit entry'}</h2>
              <p>{editingId === null ? 'Save a new weigh-in.' : 'Update the selected weigh-in.'}</p>
            </div>
            {editingId !== null ? (
              <button type="button" className="ghost-button" onClick={resetForm}>
                Cancel edit
              </button>
            ) : null}
          </div>

          <form className="weight-form" onSubmit={handleSubmit} noValidate>
            <label className="field">
              <span>Date</span>
              <input
                type="date"
                value={form.date}
                onChange={(event) => setForm((current) => ({ ...current, date: event.target.value }))}
                aria-invalid={Boolean(errors.date)}
              />
              {errors.date ? <small className="field-error">{errors.date}</small> : null}
            </label>

            <label className="field">
              <span>Weight (kg)</span>
              <input
                type="number"
                inputMode="decimal"
                min="20"
                max="500"
                step="0.1"
                placeholder="82.4"
                value={form.weightKg}
                onChange={(event) =>
                  setForm((current) => ({ ...current, weightKg: event.target.value }))
                }
                aria-invalid={Boolean(errors.weightKg)}
              />
              {errors.weightKg ? <small className="field-error">{errors.weightKg}</small> : null}
            </label>

            <button type="submit" className="primary-button" disabled={isSaving}>
              {isSaving ? 'Saving...' : editingId === null ? 'Add entry' : 'Save changes'}
            </button>
          </form>

          {feedback ? <p className="feedback success">{feedback}</p> : null}
          {errorMessage ? <p className="feedback error">{errorMessage}</p> : null}
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Progress</h2>
              <p>Body weight over time.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading chart" description="Building your body-weight trend." loading />
          ) : chartData.length < 2 ? (
            <StateCard
              title="Not enough data yet"
              description="Add at least two weigh-ins to unlock the progress chart."
            />
          ) : (
            <WeightProgressChart entries={chartData} />
          )}
        </div>
      </section>

      <section className="content-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Weekly averages</h2>
              <p>Average body weight grouped by week.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading weekly averages" description="Grouping your weigh-ins by week." loading />
          ) : stats.weeklyAverages.length === 0 ? (
            <StateCard
              title="No weekly averages yet"
              description="Add weigh-ins on different days to generate weekly summaries."
            />
          ) : (
            <div className="weekly-average-list" role="list">
              {stats.weeklyAverages.map((week, index) => {
                const previousWeek = stats.weeklyAverages[index + 1]
                const change =
                  previousWeek
                    ? Number((week.averageWeightKg - previousWeek.averageWeightKg).toFixed(1))
                    : null

                return (
                  <article key={week.weekKey} className="weekly-average-card" role="listitem">
                    <div>
                      <p className="entry-date">{week.label}</p>
                      <strong className="entry-weight">{week.averageWeightKg} kg</strong>
                    </div>
                    <div className="weekly-average-meta">
                      <span className="stat-subtext">{week.entryCount} weigh-ins</span>
                      <span className={getTrendClassName(change)}>
                        {change === null ? 'Baseline' : `${change > 0 ? '+' : ''}${change} kg`}
                      </span>
                    </div>
                  </article>
                )
              })}
            </div>
          )}
        </div>
      </section>

      <section className="content-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>History</h2>
              <p>Newest entries first.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading weigh-ins" description="Fetching your recorded entries." loading />
          ) : entries.length === 0 ? (
            <StateCard
              title="No weigh-ins yet"
              description="Add your first body-weight entry to start building history."
            />
          ) : (
            <div className="entry-list" role="list">
              {entries.map((entry) => (
                <article key={entry.id} className="entry-card" role="listitem">
                  <div>
                    <p className="entry-date">{formatDate(entry.date)}</p>
                    <strong className="entry-weight">{entry.weightKg} kg</strong>
                  </div>
                  <div className="entry-actions">
                    <button type="button" className="ghost-button" onClick={() => startEdit(entry)}>
                      Edit
                    </button>
                    <button type="button" className="danger-button" onClick={() => handleDelete(entry)}>
                      Delete
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
      </section>
    </main>
  )
}

function compareEntries(left: WeightEntry, right: WeightEntry) {
  return new Date(right.date).getTime() - new Date(left.date).getTime() || right.id - left.id
}

function buildWeeklyAverages(entries: WeightEntry[]) {
  const groupedEntries = new Map<string, WeightEntry[]>()

  for (const entry of entries) {
    const weekStart = getWeekStart(entry.date)
    const weekKey = weekStart.toISOString().slice(0, 10)
    const currentWeek = groupedEntries.get(weekKey) ?? []
    currentWeek.push(entry)
    groupedEntries.set(weekKey, currentWeek)
  }

  return [...groupedEntries.entries()]
    .map(([weekKey, weekEntries]) => {
      const averageWeightKg = Number(
        (
          weekEntries.reduce((sum, entry) => sum + entry.weightKg, 0) /
          weekEntries.length
        ).toFixed(1),
      )

      return {
        weekKey,
        label: formatWeekLabel(weekKey),
        averageWeightKg,
        entryCount: weekEntries.length,
      }
    })
    .sort((left, right) => new Date(right.weekKey).getTime() - new Date(left.weekKey).getTime())
}

function getWeekStart(date: string) {
  const result = new Date(date)
  const day = result.getDay()
  const diff = (day + 6) % 7
  result.setHours(0, 0, 0, 0)
  result.setDate(result.getDate() - diff)
  return result
}

function formatWeekLabel(weekKey: string) {
  const start = new Date(weekKey)
  const end = new Date(start)
  end.setDate(end.getDate() + 6)

  return `${formatDate(start.toISOString())} to ${formatDate(end.toISOString())}`
}

function getTrendClassName(change: number | null) {
  if (change === null || change === 0) {
    return 'trend-neutral'
  }

  return change > 0 ? 'trend-up' : 'trend-down'
}

type ChartEntry = WeightEntry & {
  shortDate: string
}

function WeightProgressChart({ entries }: { entries: ChartEntry[] }) {
  const width = 760
  const height = 280
  const padding = { top: 20, right: 24, bottom: 44, left: 52 }
  const minWeight = Math.min(...entries.map((entry) => entry.weightKg))
  const maxWeight = Math.max(...entries.map((entry) => entry.weightKg))
  const range = Math.max(maxWeight - minWeight, 1)

  const points = entries.map((entry, index) => {
    const x =
      padding.left +
      (index * (width - padding.left - padding.right)) / Math.max(entries.length - 1, 1)
    const y =
      height -
      padding.bottom -
      ((entry.weightKg - minWeight) / range) * (height - padding.top - padding.bottom)

    return { ...entry, x, y }
  })

  const linePath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ')
  const areaPath = `${linePath} L ${points.at(-1)?.x ?? 0} ${height - padding.bottom} L ${points[0]?.x ?? 0} ${height - padding.bottom} Z`
  const ticks = Array.from({ length: 4 }, (_, index) => {
    const value = maxWeight - (range / 3) * index
    const y =
      padding.top + ((height - padding.top - padding.bottom) / 3) * index

    return {
      label: `${value.toFixed(1)} kg`,
      y,
    }
  })

  return (
    <div className="chart-card">
      <svg viewBox={`0 0 ${width} ${height}`} className="progress-chart" role="img" aria-label="Body weight progress chart">
        <defs>
          <linearGradient id="weight-area" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--accent-strong)" stopOpacity="0.28" />
            <stop offset="100%" stopColor="var(--accent-strong)" stopOpacity="0.02" />
          </linearGradient>
        </defs>

        {ticks.map((tick) => (
          <g key={tick.label}>
            <line
              x1={padding.left}
              x2={width - padding.right}
              y1={tick.y}
              y2={tick.y}
              className="chart-grid-line"
            />
            <text x={padding.left - 10} y={tick.y + 4} textAnchor="end" className="chart-axis-label">
              {tick.label}
            </text>
          </g>
        ))}

        <path d={areaPath} fill="url(#weight-area)" />
        <path d={linePath} className="chart-line" />

        {points.map((point) => (
          <g key={point.id}>
            <circle cx={point.x} cy={point.y} r="5" className="chart-point" />
            <text x={point.x} y={height - 16} textAnchor="middle" className="chart-axis-label">
              {point.shortDate}
            </text>
          </g>
        ))}
      </svg>

      <div className="chart-summary">
        <div>
          <span className="stat-label">Start</span>
          <strong>{entries[0]?.weightKg} kg</strong>
          <span className="stat-subtext">{entries[0] ? formatDate(entries[0].date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Current</span>
          <strong>{entries.at(-1)?.weightKg} kg</strong>
          <span className="stat-subtext">{entries.at(-1) ? formatDate(entries.at(-1)!.date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Net Change</span>
          <strong>
            {`${(Number((entries.at(-1)!.weightKg - entries[0]!.weightKg).toFixed(1)) > 0 ? '+' : '')}${Number((entries.at(-1)!.weightKg - entries[0]!.weightKg).toFixed(1))} kg`}
          </strong>
          <span className="stat-subtext">Across tracked period</span>
        </div>
      </div>
    </div>
  )
}
