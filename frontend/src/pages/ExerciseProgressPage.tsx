import { useEffect, useMemo, useState } from 'react'
import { fetchProgressiveOverloadRecommendation } from '../api/progressiveOverload'
import { fetchWorkouts } from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { getSuggestedNextWeight } from '../lib/exerciseSuggestions'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { ProgressiveOverloadRecommendation } from '../types/progressiveOverload'
import type { CardioActivityType, CardioIntensity, Workout } from '../types/workout'

type ProgressMode = 'strength' | 'cardio'

type StrengthHistoryEntry = {
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

type CardioHistoryEntry = {
  workoutId: number
  date: string
  activityType: CardioActivityType
  durationMinutes: number
  distanceKm: number | null
  intensity: CardioIntensity | null
  notes: string
}

export function ExerciseProgressPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [selectedMode, setSelectedMode] = useState<ProgressMode>('strength')
  const [selectedItem, setSelectedItem] = useState('')
  const [itemSearch, setItemSearch] = useState('')
  const [historyDateFrom, setHistoryDateFrom] = useState('')
  const [historyDateTo, setHistoryDateTo] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingOverloadRecommendation, setIsLoadingOverloadRecommendation] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [overloadRecommendation, setOverloadRecommendation] = useState<ProgressiveOverloadRecommendation | null>(null)
  const [overloadErrorMessage, setOverloadErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadWorkouts()
  }, [])

  useEffect(() => {
    if (selectedMode !== 'strength' || !selectedItem) {
      setOverloadRecommendation(null)
      setOverloadErrorMessage(null)
      setIsLoadingOverloadRecommendation(false)
      return
    }

    void loadOverloadRecommendation(selectedItem)
  }, [selectedItem, selectedMode])

  const strengthExerciseNames = useMemo(
    () =>
      [
        ...new Set(
          workouts
            .filter((workout) => workout.workoutType !== 'cardio')
            .flatMap((workout) => workout.exerciseEntries.map((exercise) => exercise.exerciseName.trim()))
            .filter(Boolean),
        ),
      ].sort((left, right) => left.localeCompare(right)),
    [workouts],
  )

  const cardioActivityTypes = useMemo(
    () =>
      [
        ...new Set(
          workouts
            .filter((workout) => workout.workoutType === 'cardio' && workout.cardioActivityType)
            .map((workout) => workout.cardioActivityType!)
            .filter(Boolean),
        ),
      ].sort((left, right) =>
        formatCardioActivityType(left).localeCompare(formatCardioActivityType(right)),
      ),
    [workouts],
  )

  useEffect(() => {
    if (selectedMode === 'strength' && strengthExerciseNames.length === 0 && cardioActivityTypes.length > 0) {
      setSelectedMode('cardio')
      return
    }

    if (selectedMode === 'cardio' && cardioActivityTypes.length === 0 && strengthExerciseNames.length > 0) {
      setSelectedMode('strength')
    }
  }, [cardioActivityTypes.length, selectedMode, strengthExerciseNames.length])

  const availableItems = selectedMode === 'strength' ? strengthExerciseNames : cardioActivityTypes
  const filteredItems = useMemo(() => {
    const normalizedSearch = itemSearch.trim().toUpperCase()

    return availableItems.filter((item) => {
      const label =
        selectedMode === 'strength' ? item : formatCardioActivityType(item as CardioActivityType)
      return !normalizedSearch || label.toUpperCase().includes(normalizedSearch)
    })
  }, [availableItems, itemSearch, selectedMode])

  useEffect(() => {
    if (!filteredItems.includes(selectedItem)) {
      setSelectedItem(filteredItems[0] ?? '')
    }
  }, [filteredItems, selectedItem])

  const strengthHistory = useMemo(() => {
    if (selectedMode !== 'strength' || !selectedItem) {
      return []
    }

    return workouts
      .flatMap<StrengthHistoryEntry>((workout) =>
        workout.exerciseEntries
          .filter(
            (exercise) =>
              exercise.exerciseName.trim().toUpperCase() === selectedItem.trim().toUpperCase(),
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
  }, [selectedItem, selectedMode, workouts])

  const cardioHistory = useMemo(() => {
    if (selectedMode !== 'cardio' || !selectedItem) {
      return []
    }

    return workouts
      .filter(
        (workout) =>
          workout.workoutType === 'cardio' && workout.cardioActivityType === selectedItem,
      )
      .map<CardioHistoryEntry>((workout) => ({
        workoutId: workout.id,
        date: workout.date,
        activityType: workout.cardioActivityType ?? 'other',
        durationMinutes: workout.cardioDurationMinutes ?? 0,
        distanceKm: workout.cardioDistanceKm,
        intensity: workout.cardioIntensity,
        notes: workout.notes,
      }))
      .sort(
        (left, right) =>
          new Date(left.date).getTime() - new Date(right.date).getTime() || left.workoutId - right.workoutId,
      )
  }, [selectedItem, selectedMode, workouts])

  const filteredStrengthHistory = useMemo(
    () => filterHistoryByDate(strengthHistory, historyDateFrom, historyDateTo),
    [historyDateFrom, historyDateTo, strengthHistory],
  )

  const filteredCardioHistory = useMemo(
    () => filterHistoryByDate(cardioHistory, historyDateFrom, historyDateTo),
    [cardioHistory, historyDateFrom, historyDateTo],
  )

  const strengthSummary = useMemo(() => {
    const source = filteredStrengthHistory.length > 0 ? filteredStrengthHistory : strengthHistory
    const latest = source.at(-1) ?? null
    const best = source.reduce<StrengthHistoryEntry | null>(
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
        ? Number((recentWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / recentWindow.length).toFixed(1))
        : null
    const previousAverage =
      previousWindow.length > 0
        ? Number((previousWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / previousWindow.length).toFixed(1))
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
  }, [filteredStrengthHistory, strengthHistory])

  const cardioSummary = useMemo(() => {
    const source = filteredCardioHistory.length > 0 ? filteredCardioHistory : cardioHistory
    const latest = source.at(-1) ?? null
    const totalDuration = source.reduce((sum, entry) => sum + entry.durationMinutes, 0)
    const totalDistance = Number(
      source.reduce((sum, entry) => sum + (entry.distanceKm ?? 0), 0).toFixed(1),
    )
    const loggedDistanceSessions = source.filter((entry) => entry.distanceKm !== null).length
    const recentWindow = source.slice(-3)
    const previousWindow = source.slice(-6, -3)
    const recentAverageDuration =
      recentWindow.length > 0
        ? Number(
            (recentWindow.reduce((sum, entry) => sum + entry.durationMinutes, 0) / recentWindow.length).toFixed(1),
          )
        : null
    const previousAverageDuration =
      previousWindow.length > 0
        ? Number(
            (
              previousWindow.reduce((sum, entry) => sum + entry.durationMinutes, 0) /
              previousWindow.length
            ).toFixed(1),
          )
        : null
    const longestSession = source.reduce<CardioHistoryEntry | null>(
      (current, entry) => (!current || entry.durationMinutes > current.durationMinutes ? entry : current),
      null,
    )
    const farthestSession = source.reduce<CardioHistoryEntry | null>(
      (current, entry) =>
        entry.distanceKm === null
          ? current
          : !current || (entry.distanceKm ?? 0) > (current.distanceKm ?? 0)
            ? entry
            : current,
      null,
    )

    return {
      latest,
      sessionCount: source.length,
      totalDuration,
      totalDistance,
      loggedDistanceSessions,
      recentAverageDuration,
      recentTrendDelta:
        recentAverageDuration !== null && previousAverageDuration !== null
          ? Number((recentAverageDuration - previousAverageDuration).toFixed(1))
          : null,
      firstLoggedAt: source[0]?.date ?? null,
      longestSession,
      farthestSession,
    }
  }, [cardioHistory, filteredCardioHistory])

  const suggestion = useMemo(() => {
    if (selectedMode !== 'strength' || !selectedItem) {
      return null
    }

    const latest = filteredStrengthHistory.at(-1) ?? strengthHistory.at(-1)

    return getSuggestedNextWeight(
      workouts,
      selectedItem,
      latest?.setOrder ?? null,
      latest?.reps ?? null,
    )
  }, [filteredStrengthHistory, selectedItem, selectedMode, strengthHistory, workouts])

  async function loadWorkouts() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      const data = await fetchWorkouts()
      setWorkouts(data)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load progress history.'))
    } finally {
      setIsLoading(false)
    }
  }

  async function loadOverloadRecommendation(exerciseName: string) {
    try {
      setIsLoadingOverloadRecommendation(true)
      setOverloadErrorMessage(null)
      setOverloadRecommendation(await fetchProgressiveOverloadRecommendation(exerciseName))
    } catch (error) {
      setOverloadRecommendation(null)
      setOverloadErrorMessage(getRequestErrorMessage(error, 'Unable to load progressive overload guidance.'))
    } finally {
      setIsLoadingOverloadRecommendation(false)
    }
  }

  const currentChartDescription =
    selectedMode === 'strength'
      ? 'Weight lifted over time for the selected exercise.'
      : `Recent ${formatCardioActivityType((selectedItem as CardioActivityType) || 'other')} sessions over time.`

  return (
    <main className="page-shell">
      <section className="progress-hero-forge">
        <div className="progress-hero-main">
          <span className="eyebrow">FORGE / Analytics</span>
          <h1>Progress</h1>
          <p className="hero-text">
            Review strength and cardio progress in one place, with focused summaries, cleaner filtering, and searchable history.
          </p>
        </div>

        <div className="progress-hero-filter">
          <div className="progress-mode-switch" role="tablist" aria-label="Progress mode">
            <button
              type="button"
              className={selectedMode === 'strength' ? 'readiness-option readiness-option-active' : 'readiness-option'}
              onClick={() => setSelectedMode('strength')}
              disabled={isLoading || strengthExerciseNames.length === 0}
            >
              Strength
            </button>
            <button
              type="button"
              className={selectedMode === 'cardio' ? 'readiness-option readiness-option-active' : 'readiness-option'}
              onClick={() => setSelectedMode('cardio')}
              disabled={isLoading || cardioActivityTypes.length === 0}
            >
              Cardio
            </button>
          </div>

          <div className="progress-filter-panel">
            <label className="field">
              <span>Search {selectedMode === 'strength' ? 'exercise' : 'activity'}</span>
              <input
                type="text"
                placeholder={selectedMode === 'strength' ? 'Bench press' : 'Walking'}
                value={itemSearch}
                onChange={(event) => setItemSearch(event.target.value)}
                disabled={isLoading || availableItems.length === 0}
              />
            </label>

            <label className="field">
              <span>{selectedMode === 'strength' ? 'Exercise' : 'Cardio activity'}</span>
              <select
                className="select-input"
                value={selectedItem}
                onChange={(event) => setSelectedItem(event.target.value)}
                disabled={isLoading || filteredItems.length === 0}
              >
                {filteredItems.length === 0 ? (
                  <option value="">
                    {selectedMode === 'strength' ? 'No exercises yet' : 'No cardio sessions yet'}
                  </option>
                ) : (
                  filteredItems.map((item) => (
                    <option key={item} value={item}>
                      {selectedMode === 'strength' ? item : formatCardioActivityType(item as CardioActivityType)}
                    </option>
                  ))
                )}
              </select>
            </label>
          </div>
        </div>
      </section>

      {selectedMode === 'strength' ? (
        <>
          <section className="forge-stat-strip forge-stat-strip-progress">
            <ProgressSignalCard
              tone="lime"
              label="Selected exercise"
              value={selectedItem || 'None selected'}
              description={
                selectedItem
                  ? `${strengthSummary.sessionCount} logged session${strengthSummary.sessionCount === 1 ? '' : 's'}`
                  : 'Choose an exercise to begin'
              }
            />
            <ProgressSignalCard
              tone="blue"
              label="Last logged weight"
              value={strengthSummary.latest ? `${strengthSummary.latest.weightKg} kg` : 'No data'}
              description={
                strengthSummary.latest
                  ? `Set ${strengthSummary.latest.setOrder} on ${formatDate(strengthSummary.latest.date)}`
                  : 'Select an exercise'
              }
            />
            <ProgressSignalCard
              tone="teal"
              label="Personal best"
              value={strengthSummary.best ? `${strengthSummary.best.weightKg} kg` : 'No data'}
              description={
                strengthSummary.best
                  ? `${strengthSummary.best.reps} reps on ${formatDate(strengthSummary.best.date)}`
                  : 'Need at least one set'
              }
            />
            <ProgressSignalCard
              tone="violet"
              label="Recent trend"
              value={formatStrengthTrendLabel(strengthSummary.recentTrendDelta)}
              description={
                strengthSummary.recentTrendDelta === null
                  ? 'Need more recent logs to compare windows'
                  : `${strengthSummary.recentTrendDelta > 0 ? '+' : ''}${strengthSummary.recentTrendDelta} kg vs previous recent average`
              }
            />
          </section>

          <section className="progress-summary-grid">
            <div className="panel">
              <div className="panel-header">
                <div>
                  <h2>Strength summary</h2>
                  <p>{selectedItem || 'No exercise selected'}</p>
                </div>
              </div>

              <div className="exercise-progress-summary-grid progress-analytics-grid">
                <div>
                  <span className="stat-label">Sessions</span>
                  <strong>{strengthSummary.sessionCount}</strong>
                  <span className="stat-subtext">Distinct workout sessions</span>
                </div>
                <div>
                  <span className="stat-label">Sets</span>
                  <strong>{strengthSummary.totalSets}</strong>
                  <span className="stat-subtext">Total logged sets</span>
                </div>
                <div>
                  <span className="stat-label">Recent average</span>
                  <strong>{strengthSummary.recentAverage === null ? 'No data' : `${strengthSummary.recentAverage} kg`}</strong>
                  <span className="stat-subtext">Average of the last 4 logged sets</span>
                </div>
                <div>
                  <span className="stat-label">First logged</span>
                  <strong>{strengthSummary.firstLoggedAt ? formatDate(strengthSummary.firstLoggedAt) : 'No data'}</strong>
                  <span className="stat-subtext">Earliest strength entry in view</span>
                </div>
              </div>
            </div>

            <div className="panel">
              <div className="panel-header">
                <div>
                  <h2>Next set guidance</h2>
                  <p>Smart suggestion built from your saved set history.</p>
                </div>
              </div>

              <div className="suggestion-card hero-suggestion-card progress-suggestion-panel">
                <span className="stat-label">Progressive overload target</span>
                <strong>
                  {isLoadingOverloadRecommendation
                    ? 'Loading...'
                    : overloadRecommendation
                      ? overloadRecommendation.recommendedWeightKg !== null
                        ? `${overloadRecommendation.recommendedWeightKg} kg`
                        : 'No target yet'
                      : suggestion
                        ? `${suggestion.suggestedWeightKg} kg`
                        : 'No suggestion yet'}
                </strong>
                <span className="stat-subtext">
                  {overloadErrorMessage ??
                    overloadRecommendation?.shortReason ??
                    (suggestion ? suggestion.reason : 'Log this exercise to generate a recommendation.')}
                </span>
                <span className="record-hint">
                  {overloadRecommendation
                    ? `${formatProgressionStatus(overloadRecommendation.progressionStatus)} • ${overloadRecommendation.recommendedRepTarget}`
                    : suggestion?.confidenceLabel ?? 'Uses recent exercise history.'}
                </span>
              </div>
            </div>
          </section>
        </>
      ) : (
        <>
          <section className="forge-stat-strip forge-stat-strip-progress">
            <ProgressSignalCard
              tone="lime"
              label="Selected activity"
              value={selectedItem ? formatCardioActivityType(selectedItem as CardioActivityType) : 'None selected'}
              description={
                selectedItem
                  ? `${cardioSummary.sessionCount} logged session${cardioSummary.sessionCount === 1 ? '' : 's'}`
                  : 'Choose an activity to begin'
              }
            />
            <ProgressSignalCard
              tone="blue"
              label="Last logged"
              value={cardioSummary.latest ? `${cardioSummary.latest.durationMinutes} min` : 'No data'}
              description={
                cardioSummary.latest
                  ? `${formatCardioIntensity(cardioSummary.latest.intensity)} on ${formatDate(cardioSummary.latest.date)}`
                  : 'Select a cardio activity'
              }
            />
            <ProgressSignalCard
              tone="teal"
              label="Total duration"
              value={cardioSummary.sessionCount > 0 ? `${cardioSummary.totalDuration} min` : 'No data'}
              description="Across the current cardio view"
            />
            <ProgressSignalCard
              tone="violet"
              label="Total distance"
              value={cardioSummary.loggedDistanceSessions > 0 ? `${cardioSummary.totalDistance} km` : 'Not logged'}
              description={
                cardioSummary.loggedDistanceSessions > 0
                  ? `${cardioSummary.loggedDistanceSessions} session${cardioSummary.loggedDistanceSessions === 1 ? '' : 's'} included distance`
                  : 'Distance is optional for cardio sessions'
              }
            />
          </section>

          <section className="progress-summary-grid">
            <div className="panel">
              <div className="panel-header">
                <div>
                  <h2>Cardio summary</h2>
                  <p>{selectedItem ? formatCardioActivityType(selectedItem as CardioActivityType) : 'No activity selected'}</p>
                </div>
              </div>

              <div className="exercise-progress-summary-grid progress-analytics-grid">
                <div>
                  <span className="stat-label">Sessions</span>
                  <strong>{cardioSummary.sessionCount}</strong>
                  <span className="stat-subtext">Saved cardio sessions</span>
                </div>
                <div>
                  <span className="stat-label">Longest session</span>
                  <strong>{cardioSummary.longestSession ? `${cardioSummary.longestSession.durationMinutes} min` : 'No data'}</strong>
                  <span className="stat-subtext">
                    {cardioSummary.longestSession ? formatDate(cardioSummary.longestSession.date) : 'Log a cardio session'}
                  </span>
                </div>
                <div>
                  <span className="stat-label">Recent trend</span>
                  <strong className={getTrendClassName(cardioSummary.recentTrendDelta)}>
                    {formatCardioTrendLabel(cardioSummary.recentTrendDelta)}
                  </strong>
                  <span className="stat-subtext">
                    {cardioSummary.recentTrendDelta === null
                      ? 'Need more recent cardio logs to compare windows'
                      : `${cardioSummary.recentTrendDelta > 0 ? '+' : ''}${cardioSummary.recentTrendDelta} min vs previous recent average`}
                  </span>
                </div>
                <div>
                  <span className="stat-label">Farthest logged</span>
                  <strong>{cardioSummary.farthestSession?.distanceKm ? `${cardioSummary.farthestSession.distanceKm} km` : 'Not logged'}</strong>
                  <span className="stat-subtext">
                    {cardioSummary.farthestSession ? formatDate(cardioSummary.farthestSession.date) : 'Distance has not been logged yet'}
                  </span>
                </div>
              </div>
            </div>

            <div className="panel">
              <div className="panel-header">
                <div>
                  <h2>Pattern readout</h2>
                  <p>Recent cardio intensity and duration in a simpler summary card.</p>
                </div>
              </div>

              <div className="suggestion-card hero-suggestion-card progress-suggestion-panel">
                <span className="stat-label">Recent cardio pattern</span>
                <strong>
                  {cardioSummary.latest
                    ? `${formatCardioIntensity(cardioSummary.latest.intensity)} ${formatCardioActivityType(cardioSummary.latest.activityType)}`
                    : 'No pattern yet'}
                </strong>
                <span className="stat-subtext">
                  {cardioSummary.latest
                    ? `Recent sessions average ${cardioSummary.recentAverageDuration ?? cardioSummary.latest.durationMinutes} minutes. Use duration and optional distance together to track steady progress.`
                    : 'Log this activity to start tracking sessions, duration, and optional distance in one place.'}
                </span>
              </div>
            </div>
          </section>
        </>
      )}

      <section className="progress-main-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Progress chart</h2>
              <p>{currentChartDescription}</p>
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
            <StateCard title="Loading progress chart" description="Preparing the selected progress trend." loading />
          ) : errorMessage ? (
            <StateCard title="Progress unavailable" description={errorMessage} tone="error" />
          ) : selectedMode === 'strength' ? (
            filteredStrengthHistory.length < 2 ? (
              <StateCard
                title="Not enough entries yet"
                description="Log this exercise at least twice to unlock the chart."
              />
            ) : (
              <StrengthProgressChart entries={filteredStrengthHistory} selectedExercise={selectedItem} />
            )
          ) : filteredCardioHistory.length < 2 ? (
            <StateCard
              title="Not enough cardio entries yet"
              description="Log this activity at least twice to unlock the chart."
            />
          ) : (
            <CardioProgressChart entries={filteredCardioHistory} selectedActivity={selectedItem as CardioActivityType} />
          )}
        </div>

        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>{selectedMode === 'strength' ? 'Exercise history' : 'Cardio history'}</h2>
              <p>
                {selectedMode === 'strength'
                  ? 'Most recent sets first, with reps, weight, and any saved workout notes.'
                  : 'Most recent cardio sessions first, with duration, optional distance, intensity, and notes.'}
              </p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading history" description="Collecting saved progress entries." loading />
          ) : errorMessage ? (
            <StateCard title="History unavailable" description={errorMessage} tone="error" />
          ) : selectedMode === 'strength' ? (
            strengthHistory.length === 0 ? (
              <StateCard
                title="No exercise entries yet"
                description="Save this movement in a workout to start tracking it."
              />
            ) : filteredStrengthHistory.length === 0 ? (
              <StateCard
                title="No matches found"
                description="Adjust the date filters to see more entries."
              />
            ) : (
              <div className="exercise-history-list list-scroll-region" role="list">
                {[...filteredStrengthHistory]
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
                          <strong className={getTrendClassName(entry.weightKg - (strengthSummary.latest?.weightKg ?? entry.weightKg))}>
                            {formatStrengthDelta(entry.weightKg - (strengthSummary.latest?.weightKg ?? entry.weightKg))}
                          </strong>
                        </div>
                      </div>

                      {entry.notes ? <p className="workout-notes">{entry.notes}</p> : null}
                    </article>
                  ))}
              </div>
            )
          ) : cardioHistory.length === 0 ? (
            <StateCard
              title="No cardio entries yet"
              description="Save cardio sessions to start tracking this activity."
            />
          ) : filteredCardioHistory.length === 0 ? (
            <StateCard
              title="No matches found"
              description="Adjust the date filters to see more entries."
            />
          ) : (
            <div className="exercise-history-list list-scroll-region" role="list">
              {[...filteredCardioHistory]
                .sort(
                  (left, right) =>
                    new Date(right.date).getTime() - new Date(left.date).getTime() || right.workoutId - left.workoutId,
                )
                .map((entry) => (
                  <article key={`${entry.workoutId}-${entry.date}`} className="exercise-history-card" role="listitem">
                    <div className="workout-card-header">
                      <div>
                        <p className="entry-date">{formatDate(entry.date)}</p>
                        <strong className="entry-weight">{entry.durationMinutes} min</strong>
                      </div>
                      <div className="exercise-summary-meta">
                        <span className="info-pill info-pill-cardio">{formatCardioActivityType(entry.activityType)}</span>
                        <span className="record-hint">{formatCardioIntensity(entry.intensity)}</span>
                      </div>
                    </div>

                    <div className="exercise-history-metrics">
                      <div>
                        <span className="stat-label">Duration</span>
                        <strong>{entry.durationMinutes} min</strong>
                      </div>
                      <div>
                        <span className="stat-label">Distance</span>
                        <strong>{entry.distanceKm === null ? 'Not logged' : `${entry.distanceKm} km`}</strong>
                      </div>
                      <div>
                        <span className="stat-label">Intensity</span>
                        <strong>{formatCardioIntensity(entry.intensity)}</strong>
                      </div>
                      <div>
                        <span className="stat-label">Delta vs latest</span>
                        <strong className={getTrendClassName(entry.durationMinutes - (cardioSummary.latest?.durationMinutes ?? entry.durationMinutes))}>
                          {formatCardioDelta(entry.durationMinutes - (cardioSummary.latest?.durationMinutes ?? entry.durationMinutes))}
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

function ProgressSignalCard({
  tone,
  label,
  value,
  description,
}: {
  tone: 'lime' | 'blue' | 'teal' | 'violet'
  label: string
  value: string
  description: string
}) {
  return (
    <article className={`forge-stat-card forge-stat-card-${tone}`}>
      <div className="forge-stat-glow" aria-hidden="true" />
      <span className="stat-label">{label}</span>
      <strong>{value}</strong>
      <p>{description}</p>
    </article>
  )
}

function StrengthProgressChart({
  entries,
  selectedExercise,
}: {
  entries: StrengthHistoryEntry[]
  selectedExercise: string
}) {
  return (
    <ProgressChart
      title={`${selectedExercise} progress chart`}
      lineId="exercise-area"
      entries={entries.map((entry) => ({
        id: `${entry.exerciseId}-${entry.setOrder}-${entry.date}`,
        date: entry.date,
        value: entry.weightKg,
      }))}
      metricLabel="kg"
      bestLabel="Best logged set"
    />
  )
}

function CardioProgressChart({
  entries,
  selectedActivity,
}: {
  entries: CardioHistoryEntry[]
  selectedActivity: CardioActivityType
}) {
  const distanceEntries = entries.filter((entry) => entry.distanceKm !== null)
  const useDistance = distanceEntries.length >= 2
  const chartEntries = useDistance ? distanceEntries : entries

  return (
    <ProgressChart
      title={`${formatCardioActivityType(selectedActivity)} progress chart`}
      lineId="cardio-area"
      entries={chartEntries.map((entry) => ({
        id: `${entry.workoutId}-${entry.date}`,
        date: entry.date,
        value: useDistance ? (entry.distanceKm ?? 0) : entry.durationMinutes,
      }))}
      metricLabel={useDistance ? 'km' : 'min'}
      bestLabel={useDistance ? 'Farthest logged session' : 'Longest logged session'}
    />
  )
}

function ProgressChart({
  title,
  lineId,
  entries,
  metricLabel,
  bestLabel,
}: {
  title: string
  lineId: string
  entries: Array<{ id: string; date: string; value: number }>
  metricLabel: string
  bestLabel: string
}) {
  const width = 900
  const height = 300
  const padding = { top: 20, right: 24, bottom: 44, left: 52 }
  const minValue = Math.min(...entries.map((entry) => entry.value))
  const maxValue = Math.max(...entries.map((entry) => entry.value))
  const range = Math.max(maxValue - minValue, 1)

  const points = entries.map((entry, index) => {
    const x =
      padding.left +
      (index * (width - padding.left - padding.right)) / Math.max(entries.length - 1, 1)
    const y =
      height -
      padding.bottom -
      ((entry.value - minValue) / range) * (height - padding.top - padding.bottom)

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
    const value = maxValue - (range / 3) * index
    const y = padding.top + ((height - padding.top - padding.bottom) / 3) * index

    return { label: `${value.toFixed(1)} ${metricLabel}`, y }
  })

  return (
    <div className="chart-card">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="progress-chart"
        role="img"
        aria-label={title}
      >
        <defs>
          <linearGradient id={lineId} x1="0" y1="0" x2="0" y2="1">
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

        <path d={areaPath} fill={`url(#${lineId})`} />
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
          <span className="stat-label">First</span>
          <strong>{entries[0] ? `${entries[0].value} ${metricLabel}` : 'No data'}</strong>
          <span className="stat-subtext">{entries[0] ? formatDate(entries[0].date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Latest</span>
          <strong>{entries.at(-1) ? `${entries.at(-1)!.value} ${metricLabel}` : 'No data'}</strong>
          <span className="stat-subtext">{entries.at(-1) ? formatDate(entries.at(-1)!.date) : ''}</span>
        </div>
        <div>
          <span className="stat-label">Peak</span>
          <strong>{`${Math.max(...entries.map((entry) => entry.value))} ${metricLabel}`}</strong>
          <span className="stat-subtext">{bestLabel}</span>
        </div>
      </div>
    </div>
  )
}

function filterHistoryByDate<T extends { date: string }>(entries: T[], dateFrom: string, dateTo: string) {
  return entries.filter((entry) => {
    const entryDate = entry.date.slice(0, 10)
    const matchesDateFrom = !dateFrom || entryDate >= dateFrom
    const matchesDateTo = !dateTo || entryDate <= dateTo

    return matchesDateFrom && matchesDateTo
  })
}

function formatStrengthTrendLabel(change: number | null) {
  if (change === null || change === 0) {
    return 'Stable'
  }

  return change > 0 ? 'Improving' : 'Cooling off'
}

function formatCardioTrendLabel(change: number | null) {
  if (change === null || change === 0) {
    return 'Steady'
  }

  return change > 0 ? 'Building up' : 'Lighter lately'
}

function formatStrengthDelta(change: number) {
  const rounded = Number(change.toFixed(1))
  return `${rounded > 0 ? '+' : ''}${rounded} kg`
}

function formatCardioDelta(change: number) {
  const rounded = Number(change.toFixed(1))
  return `${rounded > 0 ? '+' : ''}${rounded} min`
}

function getTrendClassName(change: number | null) {
  if (change === null || change === 0) {
    return 'trend-neutral'
  }

  return change > 0 ? 'trend-up' : 'trend-down'
}

function formatProgressionStatus(status: ProgressiveOverloadRecommendation['progressionStatus']) {
  switch (status) {
    case 'increase':
      return 'Increase'
    case 'deload':
      return 'Deload'
    case 'hold':
      return 'Hold'
  }
}

function formatCardioActivityType(activityType: CardioActivityType) {
  switch (activityType) {
    case 'walking':
      return 'Walking'
    case 'running':
      return 'Running'
    case 'cycling':
      return 'Cycling'
    default:
      return 'Other'
  }
}

function formatCardioIntensity(intensity: CardioIntensity | null) {
  if (intensity === 'high') {
    return 'High intensity'
  }

  if (intensity === 'moderate') {
    return 'Moderate intensity'
  }

  if (intensity === 'low') {
    return 'Low intensity'
  }

  return 'Intensity not set'
}
