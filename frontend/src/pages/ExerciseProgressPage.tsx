import { useEffect, useMemo, useState } from 'react'
import { fetchWorkouts } from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { Workout } from '../types/workout'

type ExerciseHistoryEntry = {
  workoutId: number
  exerciseId: number
  exerciseName: string
  date: string
  setOrder: number
  reps: number
  weightKg: number
  notes: string
  isPersonalRecord: boolean
  personalRecordWeightKg: number
}

export function ExerciseProgressPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [selectedExercise, setSelectedExercise] = useState('')
  const [exerciseSearch, setExerciseSearch] = useState('')
  const [historyDateFrom, setHistoryDateFrom] = useState('')
  const [historyDateTo, setHistoryDateTo] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadWorkouts()
  }, [])

  const exerciseNames = useMemo(() => {
    return [...new Set(
      workouts
        .flatMap((workout) => workout.exerciseEntries.map((exercise) => exercise.exerciseName.trim()))
        .filter(Boolean),
    )].sort((left, right) => left.localeCompare(right))
  }, [workouts])

  const filteredExerciseNames = useMemo(() => {
    const normalizedSearch = exerciseSearch.trim().toUpperCase()

    return exerciseNames.filter(
      (exerciseName) =>
        !normalizedSearch || exerciseName.toUpperCase().includes(normalizedSearch),
    )
  }, [exerciseNames, exerciseSearch])

  useEffect(() => {
    if (!filteredExerciseNames.includes(selectedExercise)) {
      setSelectedExercise(filteredExerciseNames[0] ?? '')
    }
  }, [filteredExerciseNames, selectedExercise])

  const history = useMemo(() => {
    if (!selectedExercise) {
      return []
    }

    return workouts
      .flatMap<ExerciseHistoryEntry>((workout) =>
        workout.exerciseEntries
          .filter(
            (exercise) =>
              exercise.exerciseName.trim().toUpperCase() === selectedExercise.trim().toUpperCase(),
          )
          .flatMap((exercise) =>
            exercise.sets.map((set) => ({
              workoutId: workout.id,
              exerciseId: exercise.id,
              exerciseName: exercise.exerciseName,
              date: workout.date,
              setOrder: set.order,
              reps: set.reps,
              weightKg: set.weightKg,
              notes: workout.notes,
              isPersonalRecord: set.weightKg === exercise.personalRecordWeightKg,
              personalRecordWeightKg: exercise.personalRecordWeightKg,
            })),
          ),
      )
      .sort(
        (left, right) =>
          new Date(left.date).getTime() - new Date(right.date).getTime() ||
          left.exerciseId - right.exerciseId ||
          left.setOrder - right.setOrder,
      )
  }, [selectedExercise, workouts])

  const filteredHistory = useMemo(() => {
    return history.filter((entry) => {
      const entryDate = entry.date.slice(0, 10)
      const matchesDateFrom = !historyDateFrom || entryDate >= historyDateFrom
      const matchesDateTo = !historyDateTo || entryDate <= historyDateTo

      return matchesDateFrom && matchesDateTo
    })
  }, [history, historyDateFrom, historyDateTo])

  const exerciseSummary = useMemo(() => {
    const source = filteredHistory.length > 0 ? filteredHistory : history
    const latest = source.at(-1) ?? null
    const best = source.reduce<ExerciseHistoryEntry | null>(
      (current, entry) =>
        !current ||
        entry.weightKg > current.weightKg ||
        (entry.weightKg === current.weightKg && entry.reps > current.reps)
          ? entry
          : current,
      null,
    )
    const sessionCount = new Set(source.map((entry) => entry.workoutId)).size
    const recentWindow = source.slice(-4)
    const previousWindow = source.slice(-8, -4)
    const recentAverage =
      recentWindow.length > 0
        ? Number(
            (
              recentWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / recentWindow.length
            ).toFixed(1),
          )
        : null
    const previousAverage =
      previousWindow.length > 0
        ? Number(
            (
              previousWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / previousWindow.length
            ).toFixed(1),
          )
        : null

    return {
      latest,
      best,
      sessionCount,
      totalSets: source.length,
      firstLoggedAt: source[0]?.date ?? null,
      recentAverage,
      recentTrendDelta:
        recentAverage !== null && previousAverage !== null
          ? Number((recentAverage - previousAverage).toFixed(1))
          : null,
    }
  }, [filteredHistory, history])

  const suggestion = useMemo(() => {
    if (!selectedExercise) {
      return null
    }

    const latest = filteredHistory.at(-1) ?? history.at(-1)

    return getSuggestedNextWeight(
      workouts,
      selectedExercise,
      latest?.setOrder ?? null,
      latest?.reps ?? null,
    )
  }, [filteredHistory, history, selectedExercise, workouts])

  async function loadWorkouts() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      const data = await fetchWorkouts()
      setWorkouts(data)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load exercise history.'))
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Exercise Progress</h1>
          <p className="hero-text">
            Pick an exercise and review recent performance, best lift, trend direction, and set history in one place.
          </p>
        </div>

        <div className="exercise-filter-card">
          <p className="section-note">
            Search first if your list is long, then select one movement to focus the chart and history below.
          </p>

          <label className="field">
            <span>Search exercise</span>
            <input
              type="text"
              placeholder="Bench press"
              value={exerciseSearch}
              onChange={(event) => setExerciseSearch(event.target.value)}
              disabled={isLoading || exerciseNames.length === 0}
            />
          </label>

          <label className="field">
            <span>Exercise</span>
            <select
              className="select-input"
              value={selectedExercise}
              onChange={(event) => setSelectedExercise(event.target.value)}
              disabled={isLoading || filteredExerciseNames.length === 0}
            >
              {filteredExerciseNames.length === 0 ? (
                <option value="">No exercises yet</option>
              ) : (
                filteredExerciseNames.map((exerciseName) => (
                  <option key={exerciseName} value={exerciseName}>
                    {exerciseName}
                  </option>
                ))
              )}
            </select>
          </label>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">Selected Exercise</span>
            <strong>{selectedExercise || 'None selected'}</strong>
            <span className="stat-subtext">
              {selectedExercise
                ? `${exerciseSummary.sessionCount} logged session${exerciseSummary.sessionCount === 1 ? '' : 's'}`
                : 'Choose an exercise to begin'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Last Logged Weight</span>
            <strong>{exerciseSummary.latest ? `${exerciseSummary.latest.weightKg} kg` : 'No data'}</strong>
            <span className="stat-subtext">
              {exerciseSummary.latest
                ? `Set ${exerciseSummary.latest.setOrder} on ${formatDate(exerciseSummary.latest.date)}`
                : 'Select an exercise'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Personal Best</span>
            <strong>{exerciseSummary.best ? `${exerciseSummary.best.weightKg} kg` : 'No data'}</strong>
            <span className="stat-subtext">
              {exerciseSummary.best
                ? `${exerciseSummary.best.reps} reps on ${formatDate(exerciseSummary.best.date)}`
                : 'Need at least one set'}
            </span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Recent Trend</span>
            <strong className={getTrendClassName(exerciseSummary.recentTrendDelta)}>
              {formatTrendLabel(exerciseSummary.recentTrendDelta)}
            </strong>
            <span className="stat-subtext">
              {exerciseSummary.recentTrendDelta === null
                ? 'Need more recent logs to compare windows'
                : `${exerciseSummary.recentTrendDelta > 0 ? '+' : ''}${exerciseSummary.recentTrendDelta} kg vs previous recent average`}
            </span>
          </article>
        </div>

        <div className="exercise-progress-hero-grid">
          <div className="exercise-summary-card">
            <div className="goal-progress-header">
              <span className="stat-label">Exercise Summary</span>
              <strong>{selectedExercise || 'No exercise selected'}</strong>
            </div>

            <div className="exercise-progress-summary-grid">
              <div>
                <span className="stat-label">Sessions</span>
                <strong>{exerciseSummary.sessionCount}</strong>
                <span className="stat-subtext">Distinct workout sessions</span>
              </div>
              <div>
                <span className="stat-label">Sets</span>
                <strong>{exerciseSummary.totalSets}</strong>
                <span className="stat-subtext">Total logged sets</span>
              </div>
              <div>
                <span className="stat-label">Recent Average</span>
                <strong>{exerciseSummary.recentAverage === null ? 'No data' : `${exerciseSummary.recentAverage} kg`}</strong>
                <span className="stat-subtext">Average of last 4 logged sets</span>
              </div>
              <div>
                <span className="stat-label">First Logged</span>
                <strong>{exerciseSummary.firstLoggedAt ? formatDate(exerciseSummary.firstLoggedAt) : 'No data'}</strong>
                <span className="stat-subtext">Earliest entry in current view</span>
              </div>
            </div>
          </div>

          <div className="suggestion-card hero-suggestion-card">
            <span className="stat-label">Suggested Next Weight</span>
            <strong>{suggestion ? `${suggestion.suggestedWeightKg} kg` : 'No suggestion yet'}</strong>
            <span className="stat-subtext">
              {suggestion ? suggestion.reason : 'Log this exercise to generate a recommendation.'}
            </span>
          </div>
        </div>
      </section>

      <section className="content-grid exercise-progress-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Progress chart</h2>
              <p>Weight lifted over time for the selected exercise, with a quick summary below the chart.</p>
            </div>
          </div>

          <div className="filter-toolbar">
            <label className="field filter-field">
              <span>From</span>
              <input
                type="date"
                value={historyDateFrom}
                onChange={(event) => setHistoryDateFrom(event.target.value)}
              />
            </label>
            <label className="field filter-field">
              <span>To</span>
              <input
                type="date"
                value={historyDateTo}
                onChange={(event) => setHistoryDateTo(event.target.value)}
              />
            </label>
          </div>

          {isLoading ? (
            <StateCard title="Loading progress chart" description="Preparing the selected exercise trend." loading />
          ) : errorMessage ? (
            <StateCard title="Exercise history unavailable" description={errorMessage} tone="error" />
          ) : filteredHistory.length < 2 ? (
            <StateCard
              title="Not enough entries yet"
              description="Log this exercise at least twice to unlock the chart."
            />
          ) : (
            <ExerciseProgressChart entries={filteredHistory} selectedExercise={selectedExercise} />
          )}
        </div>
      </section>

      <section className="content-grid exercise-progress-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Exercise history</h2>
              <p>Most recent sets first, with reps, weight, and any saved workout notes.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading exercise history" description="Collecting past sets for this movement." loading />
          ) : errorMessage ? (
            <StateCard title="Exercise history unavailable" description={errorMessage} tone="error" />
          ) : history.length === 0 ? (
            <StateCard
              title="No exercise entries yet"
              description="Save this movement in a workout to start tracking it."
            />
          ) : filteredHistory.length === 0 ? (
            <StateCard
              title="No matches found"
              description="Adjust the date filters to see more entries."
            />
          ) : (
            <div className="exercise-history-list" role="list">
              {[...filteredHistory]
                .sort(
                  (left, right) =>
                    new Date(right.date).getTime() - new Date(left.date).getTime() ||
                    right.exerciseId - left.exerciseId ||
                    right.setOrder - left.setOrder,
                )
                .map((entry) => (
                  <article key={`${entry.exerciseId}-${entry.setOrder}-${entry.date}`} className="exercise-history-card" role="listitem">
                    <div className="workout-card-header">
                      <div>
                        <p className="entry-date">{formatDate(entry.date)}</p>
                        <strong className="entry-weight">{entry.weightKg} kg</strong>
                      </div>
                      <div className="exercise-summary-meta">
                        {entry.isPersonalRecord ? <span className="pr-badge">PR</span> : null}
                        <span className="record-hint">Best: {entry.personalRecordWeightKg} kg</span>
                      </div>
                    </div>

                    <div className="exercise-history-metrics">
                      <div>
                        <span className="stat-label">Set</span>
                        <strong>{entry.setOrder}</strong>
                      </div>
                      <div>
                        <span className="stat-label">Reps</span>
                        <strong>{entry.reps}</strong>
                      </div>
                      <div>
                        <span className="stat-label">Workout</span>
                        <strong>{formatDate(entry.date)}</strong>
                      </div>
                      <div>
                        <span className="stat-label">Delta vs latest</span>
                        <strong className={getTrendClassName(entry.weightKg - (exerciseSummary.latest?.weightKg ?? entry.weightKg))}>
                          {formatDelta(entry.weightKg - (exerciseSummary.latest?.weightKg ?? entry.weightKg))}
                        </strong>
                      </div>
                    </div>

                    {entry.notes ? <p className="workout-notes">{entry.notes}</p> : null}
                  </article>
                ))}
            </div>
          )}
        </div>
      </section>
    </main>
  )
}

function ExerciseProgressChart({
  entries,
  selectedExercise,
}: {
  entries: ExerciseHistoryEntry[]
  selectedExercise: string
}) {
  const width = 900
  const height = 300
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

    return {
      ...entry,
      x,
      y,
      shortDate: new Intl.DateTimeFormat(undefined, {
        month: 'short',
        day: 'numeric',
      }).format(new Date(entry.date)),
    }
  })

  const linePath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x} ${point.y}`).join(' ')
  const areaPath = `${linePath} L ${points.at(-1)?.x ?? 0} ${height - padding.bottom} L ${points[0]?.x ?? 0} ${height - padding.bottom} Z`
  const ticks = Array.from({ length: 4 }, (_, index) => {
    const value = maxWeight - (range / 3) * index
    const y = padding.top + ((height - padding.top - padding.bottom) / 3) * index

    return { label: `${value.toFixed(1)} kg`, y }
  })

  return (
    <div className="chart-card">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="progress-chart"
        role="img"
        aria-label={`${selectedExercise} progress chart`}
      >
        <defs>
          <linearGradient id="exercise-area" x1="0" y1="0" x2="0" y2="1">
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

        <path d={areaPath} fill="url(#exercise-area)" />
        <path d={linePath} className="chart-line" />

        {points.map((point) => (
          <g key={`${point.exerciseId}-${point.setOrder}-${point.date}`}>
            <circle cx={point.x} cy={point.y} r="5" className="chart-point" />
            <text x={point.x} y={height - 16} textAnchor="middle" className="chart-axis-label">
              {point.shortDate}
            </text>
          </g>
        ))}
      </svg>

      <div className="chart-summary">
        <div>
          <span className="stat-label">First</span>
          <strong>{entries[0]?.weightKg} kg</strong>
          <span className="stat-subtext">{entries[0] ? formatDate(entries[0].date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Latest</span>
          <strong>{entries.at(-1)?.weightKg} kg</strong>
          <span className="stat-subtext">{entries.at(-1) ? formatDate(entries.at(-1)!.date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Heaviest</span>
          <strong>{Math.max(...entries.map((entry) => entry.weightKg))} kg</strong>
          <span className="stat-subtext">Best logged set</span>
        </div>
      </div>
    </div>
  )
}

function formatTrendLabel(change: number | null) {
  if (change === null || change === 0) {
    return 'Stable'
  }

  return change > 0 ? 'Improving' : 'Cooling off'
}

function formatDelta(change: number) {
  const rounded = Number(change.toFixed(1))
  return `${rounded > 0 ? '+' : ''}${rounded} kg`
}

function getTrendClassName(change: number | null) {
  if (change === null || change === 0) {
    return 'trend-neutral'
  }

  return change > 0 ? 'trend-up' : 'trend-down'
}
