import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { fetchLatestReadinessLog, upsertReadinessLog } from '../api/readiness'
import { fetchCycleGuidance } from '../api/cycle'
import { fetchGoals, updateGoals } from '../api/goals'
import { fetchWeightEntries } from '../api/weightEntries'
import { fetchWorkouts } from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { getWorkoutAssistantInsight } from '../lib/exerciseSuggestions'
import { formatDate, getTodayDateValue } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { CycleGuidance } from '../types/cycle'
import type { GoalSettings, GoalSettingsPayload } from '../types/goals'
import type { ReadinessLog } from '../types/readiness'
import type { WeightEntry } from '../types/weight'
import type { Workout } from '../types/workout'

const initialReadinessForm = {
  energyLevel: 2,
  sorenessLevel: 2,
  sleepQuality: 2,
  motivationLevel: 2,
  notes: '',
}

export function DashboardPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [weightEntries, setWeightEntries] = useState<WeightEntry[]>([])
  const [goals, setGoals] = useState<GoalSettings>({
    targetBodyWeightKg: null,
    weeklyWorkoutTarget: null,
    fitnessPhase: 'maintain',
  })
  const [goalForm, setGoalForm] = useState({
    targetBodyWeightKg: '',
    weeklyWorkoutTarget: '',
    fitnessPhase: 'maintain',
  })
  const [isLoading, setIsLoading] = useState(true)
  const [isSavingGoals, setIsSavingGoals] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [goalMessage, setGoalMessage] = useState<string | null>(null)
  const [goalErrorMessage, setGoalErrorMessage] = useState<string | null>(null)
  const [cycleGuidance, setCycleGuidance] = useState<CycleGuidance | null>(null)
  const [readinessLog, setReadinessLog] = useState<ReadinessLog | null>(null)
  const [readinessForm, setReadinessForm] = useState(initialReadinessForm)
  const [isSavingReadiness, setIsSavingReadiness] = useState(false)
  const [readinessMessage, setReadinessMessage] = useState<string | null>(null)
  const [readinessErrorMessage, setReadinessErrorMessage] = useState<string | null>(null)

  useEffect(() => {
    void loadDashboard()
  }, [])

  const metrics = useMemo(() => {
    const now = new Date()
    const weekStart = startOfWeek(now)
    const weekEnd = addDays(weekStart, 7)

    const workoutsThisWeek = workouts.filter((workout) => {
      const workoutDate = new Date(workout.date)
      return workoutDate >= weekStart && workoutDate < weekEnd
    })

    const weighInsThisWeek = weightEntries.filter((entry) => {
      const entryDate = new Date(entry.date)
      return entryDate >= weekStart && entryDate < weekEnd
    })

    const averageBodyWeight =
      weightEntries.length > 0
        ? Number(
            (
              weightEntries.reduce((sum, entry) => sum + entry.weightKg, 0) /
              weightEntries.length
            ).toFixed(1),
          )
        : null

    const averageBodyWeightThisWeek =
      weighInsThisWeek.length > 0
        ? Number(
            (
              weighInsThisWeek.reduce((sum, entry) => sum + entry.weightKg, 0) /
              weighInsThisWeek.length
            ).toFixed(1),
          )
        : null

    const workoutStreakWeeks = getWorkoutWeekStreak(workouts)
    const latestWorkout = workouts[0] ?? null
    const latestWeightEntry = weightEntries[0] ?? null
    const bodyWeightGoalProgress = getBodyWeightGoalProgress(
      latestWeightEntry?.weightKg ?? null,
      goals.targetBodyWeightKg,
      goals.fitnessPhase,
    )
    const weeklyWorkoutGoalProgress = getWeeklyWorkoutGoalProgress(
      workoutsThisWeek.length,
      goals.weeklyWorkoutTarget,
    )

    return {
      workoutsThisWeek: workoutsThisWeek.length,
      exercisesThisWeek: workoutsThisWeek.reduce(
        (count, workout) => count + workout.exerciseEntries.length,
        0,
      ),
      weighInsThisWeek: weighInsThisWeek.length,
      averageBodyWeight,
      averageBodyWeightThisWeek,
      totalWorkouts: workouts.length,
      workoutStreakWeeks,
      latestWorkout,
      latestWeightEntry,
      bodyWeightGoalProgress,
      weeklyWorkoutGoalProgress,
    }
  }, [goals, weightEntries, workouts])

  const assistantInsight = useMemo(
    () => getWorkoutAssistantInsight(workouts, goals, cycleGuidance, readinessLog),
    [cycleGuidance, goals, readinessLog, workouts],
  )

  const hasTodayReadinessLog = readinessLog?.date === getTodayDateValue()
  const readinessAverage =
    (readinessForm.energyLevel +
      readinessForm.sorenessLevel +
      readinessForm.sleepQuality +
      readinessForm.motivationLevel) /
    4

  async function loadDashboard() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      setGoalMessage(null)
      setGoalErrorMessage(null)
      setReadinessMessage(null)
      setReadinessErrorMessage(null)

      const [workoutData, weightData, goalsData, cycleGuidanceData, latestReadinessLog] = await Promise.all([
        fetchWorkouts(),
        fetchWeightEntries(),
        fetchGoals(),
        fetchCycleGuidance().catch(() => null),
        fetchLatestReadinessLog().catch(() => null),
      ])

      setWorkouts(workoutData)
      setWeightEntries(weightData)
      applyGoalState(goalsData)
      setCycleGuidance(cycleGuidanceData)
      setReadinessLog(latestReadinessLog)
      if (latestReadinessLog?.date === getTodayDateValue()) {
        setReadinessForm({
          energyLevel: latestReadinessLog.energyLevel,
          sorenessLevel: latestReadinessLog.sorenessLevel,
          sleepQuality: latestReadinessLog.sleepQuality,
          motivationLevel: latestReadinessLog.motivationLevel,
          notes: latestReadinessLog.notes ?? '',
        })
      }
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load dashboard data.'))
    } finally {
      setIsLoading(false)
    }
  }

  async function handleGoalSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const payload: GoalSettingsPayload = {
      targetBodyWeightKg: goalForm.targetBodyWeightKg === '' ? null : Number(goalForm.targetBodyWeightKg),
      weeklyWorkoutTarget: goalForm.weeklyWorkoutTarget === '' ? null : Number(goalForm.weeklyWorkoutTarget),
      fitnessPhase: goalForm.fitnessPhase as GoalSettings['fitnessPhase'],
    }

    try {
      setIsSavingGoals(true)
      setGoalErrorMessage(null)
      setGoalMessage(null)

      const savedGoals = await updateGoals(payload)
      applyGoalState(savedGoals)
      setGoalMessage('Goals updated.')
    } catch (error) {
      setGoalErrorMessage(getRequestErrorMessage(error, 'Unable to update goals.'))
    } finally {
      setIsSavingGoals(false)
    }
  }

  function applyGoalState(nextGoals: GoalSettings) {
    setGoals(nextGoals)
    setGoalForm({
      targetBodyWeightKg:
        nextGoals.targetBodyWeightKg === null ? '' : nextGoals.targetBodyWeightKg.toString(),
      weeklyWorkoutTarget:
        nextGoals.weeklyWorkoutTarget === null ? '' : nextGoals.weeklyWorkoutTarget.toString(),
      fitnessPhase: nextGoals.fitnessPhase,
    })
  }

  async function handleReadinessSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    try {
      setIsSavingReadiness(true)
      setReadinessMessage(null)
      setReadinessErrorMessage(null)

      const savedLog = await upsertReadinessLog({
        date: getTodayDateValue(),
        energyLevel: readinessForm.energyLevel,
        sorenessLevel: readinessForm.sorenessLevel,
        sleepQuality: readinessForm.sleepQuality,
        motivationLevel: readinessForm.motivationLevel,
        notes: readinessForm.notes.trim() === '' ? null : readinessForm.notes.trim(),
      })

      setReadinessLog(savedLog)
      setReadinessMessage(hasTodayReadinessLog ? 'Today’s check-in updated.' : 'Today’s check-in saved.')
    } catch (error) {
      setReadinessErrorMessage(getRequestErrorMessage(error, 'Unable to save today’s check-in.'))
    } finally {
      setIsSavingReadiness(false)
    }
  }

  return (
    <main className="page-shell">
      <section className="dashboard-hero-forge">
        <div className="dashboard-hero-main">
          <span className="eyebrow">FORGE / Overview</span>
          <h1>Dashboard</h1>
          <p className="hero-text">
            Your weekly training snapshot, body-weight direction, readiness, and coaching signals in one operating view.
          </p>

          <div className="dashboard-hero-actions">
            <div className="hero-inline-stat">
              <span className="stat-label">Current phase</span>
              <strong>{formatPhase(goals.fitnessPhase)}</strong>
              <span className="stat-subtext">{getPhaseSummary(goals.fitnessPhase)}</span>
            </div>
            <div className="hero-inline-stat hero-inline-stat-accent">
              <span className="stat-label">Focus this week</span>
              <strong>
                {goals.weeklyWorkoutTarget === null
                  ? 'Set a target'
                  : `${metrics.workoutsThisWeek}/${goals.weeklyWorkoutTarget}`}
              </strong>
              <span className="stat-subtext">
                {goals.weeklyWorkoutTarget === null
                  ? 'Add a weekly workout goal to measure consistency.'
                  : metrics.weeklyWorkoutGoalProgress.message}
              </span>
            </div>
          </div>
        </div>

        <div className="dashboard-hero-side">
          <article className="forge-focus-card">
            <span className="stat-label">Training status</span>
            <strong>{getDashboardStatus(metrics.workoutsThisWeek, goals.weeklyWorkoutTarget)}</strong>
            <p>{assistantInsight.weeklyNudge}</p>
            <div className="forge-focus-pills">
              <span className="info-pill">{metrics.workoutStreakWeeks} week streak</span>
              <span className="info-pill info-pill-strength">
                {metrics.averageBodyWeightThisWeek === null
                  ? 'No weekly weigh-ins'
                  : `${metrics.averageBodyWeightThisWeek} kg this week`}
              </span>
            </div>
          </article>
        </div>
      </section>

      <section className="forge-stat-strip forge-stat-strip-dashboard">
        <ForgeStatCard
          tone="lime"
          label="This week"
          value={metrics.workoutsThisWeek.toString()}
          description="Workouts logged during the current week"
          trend={metrics.weeklyWorkoutGoalProgress.message}
        />
        <ForgeStatCard
          tone="blue"
          label="Average weight"
          value={metrics.averageBodyWeight === null ? 'No data' : `${metrics.averageBodyWeight}`}
          unit={metrics.averageBodyWeight === null ? undefined : 'kg'}
          description="Average across all recorded weigh-ins"
        />
        <ForgeStatCard
          tone="teal"
          label="Workout streak"
          value={metrics.workoutStreakWeeks.toString()}
          description="Consecutive active weeks"
        />
        <ForgeStatCard
          tone="violet"
          label="Readiness"
          value={hasTodayReadinessLog && readinessLog ? readinessLog.readinessLabel : readinessAverage.toFixed(1)}
          description={
            hasTodayReadinessLog && readinessLog
              ? 'Today’s check-in already logged'
              : 'Live average from the current check-in inputs'
          }
        />
      </section>

      <section className="dashboard-forge-grid">
        <div className="panel panel-span-2 dashboard-panel-goals">
          <div className="panel-header">
            <div>
              <h2>Goals</h2>
              <p>Set body-weight and weekly workload targets, then keep progress visible in a cleaner operating layout.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading goals" description="Pulling your current goal settings." loading />
          ) : errorMessage ? (
            <StateCard title="Goals unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="dashboard-goals-layout">
              <form className="goal-form dashboard-goal-form" onSubmit={handleGoalSubmit}>
                <p className="section-note">
                  Keep this lightweight. A target weight and weekly workout count are enough to anchor the dashboard.
                </p>

                <label className="field">
                  <span>Target body weight (kg)</span>
                  <input
                    type="number"
                    min="20"
                    max="500"
                    step="0.1"
                    value={goalForm.targetBodyWeightKg}
                    onChange={(event) =>
                      setGoalForm((current) => ({
                        ...current,
                        targetBodyWeightKg: event.target.value,
                      }))
                    }
                    placeholder="e.g. 78.5"
                  />
                </label>

                <label className="field">
                  <span>Weekly workout target</span>
                  <input
                    type="number"
                    min="1"
                    max="14"
                    step="1"
                    value={goalForm.weeklyWorkoutTarget}
                    onChange={(event) =>
                      setGoalForm((current) => ({
                        ...current,
                        weeklyWorkoutTarget: event.target.value,
                      }))
                    }
                    placeholder="e.g. 4"
                  />
                </label>

                <label className="field">
                  <span>Fitness phase</span>
                  <select
                    className="select-input"
                    value={goalForm.fitnessPhase}
                    onChange={(event) =>
                      setGoalForm((current) => ({
                        ...current,
                        fitnessPhase: event.target.value,
                      }))
                    }
                  >
                    <option value="cut">Cut</option>
                    <option value="maintain">Maintain</option>
                    <option value="bulk">Bulk</option>
                  </select>
                </label>

                <div className="action-row">
                  <button type="submit" className="primary-button" disabled={isSavingGoals}>
                    {isSavingGoals ? 'Saving goals...' : 'Save goals'}
                  </button>
                </div>

                {goalMessage ? <p className="feedback success">{goalMessage}</p> : null}
                {goalErrorMessage ? <p className="feedback error">{goalErrorMessage}</p> : null}
              </form>

              <div className="goal-progress-list dashboard-goal-progress-grid">
                <article className="goal-progress-card dashboard-progress-card">
                  <div className="goal-progress-header">
                    <span className="stat-label">Body-weight goal</span>
                    <strong>
                      {goals.targetBodyWeightKg === null ? 'Not set' : `${goals.targetBodyWeightKg} kg`}
                    </strong>
                  </div>
                  <p className="goal-progress-copy">{metrics.bodyWeightGoalProgress.message}</p>
                  <div className="progress-track" aria-hidden="true">
                    <div
                      className="progress-fill"
                      style={{ width: `${metrics.bodyWeightGoalProgress.percentage}%` }}
                    />
                  </div>
                </article>

                <article className="goal-progress-card dashboard-progress-card">
                  <div className="goal-progress-header">
                    <span className="stat-label">Weekly workout goal</span>
                    <strong>
                      {goals.weeklyWorkoutTarget === null ? 'Not set' : `${goals.weeklyWorkoutTarget} / week`}
                    </strong>
                  </div>
                  <p className="goal-progress-copy">{metrics.weeklyWorkoutGoalProgress.message}</p>
                  <div className="progress-track" aria-hidden="true">
                    <div
                      className="progress-fill"
                      style={{ width: `${metrics.weeklyWorkoutGoalProgress.percentage}%` }}
                    />
                  </div>
                </article>

                <article className="goal-progress-card dashboard-progress-card dashboard-progress-card-phase">
                  <div className="goal-progress-header">
                    <span className="stat-label">Fitness phase</span>
                    <strong>{formatPhase(goals.fitnessPhase)}</strong>
                  </div>
                  <p className="goal-progress-copy">{getPhaseSummary(goals.fitnessPhase)}</p>
                </article>
              </div>
            </div>
          )}
        </div>

        <div className="panel dashboard-panel-readiness">
          <div className="panel-header">
            <div>
              <h2>How are you feeling today?</h2>
              <p>Log a quick readiness check-in so daily training suggestions reflect how you actually feel.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading check-in" description="Preparing today’s readiness prompt." loading />
          ) : errorMessage ? (
            <StateCard title="Check-in unavailable" description={errorMessage} tone="error" />
          ) : (
            <form className="readiness-form dashboard-readiness-form" onSubmit={handleReadinessSubmit}>
              <div className="dashboard-readiness-summary">
                <div>
                  <span className="stat-label">Today</span>
                  <strong>{hasTodayReadinessLog && readinessLog ? readinessLog.readinessLabel : 'Check in now'}</strong>
                  <p className="stat-subtext">
                    {hasTodayReadinessLog && readinessLog
                      ? `Energy ${readinessLog.energyLevel}/3, soreness ${readinessLog.sorenessLevel}/3, sleep ${readinessLog.sleepQuality}/3, motivation ${readinessLog.motivationLevel}/3.`
                      : 'A quick update improves daily workout suggestions and recovery context.'}
                  </p>
                </div>
                <div className="dashboard-readiness-score">
                  <span>Readiness avg</span>
                  <strong>{readinessAverage.toFixed(1)}</strong>
                </div>
              </div>

              <div className="readiness-grid dashboard-readiness-grid">
                <ReadinessSelector
                  label="Energy"
                  value={readinessForm.energyLevel}
                  lowLabel="Low"
                  mediumLabel="Okay"
                  highLabel="High"
                  onChange={(value) => setReadinessForm((current) => ({ ...current, energyLevel: value }))}
                />
                <ReadinessSelector
                  label="Soreness"
                  value={readinessForm.sorenessLevel}
                  lowLabel="Light"
                  mediumLabel="Some"
                  highLabel="Heavy"
                  onChange={(value) => setReadinessForm((current) => ({ ...current, sorenessLevel: value }))}
                />
                <ReadinessSelector
                  label="Sleep"
                  value={readinessForm.sleepQuality}
                  lowLabel="Poor"
                  mediumLabel="Okay"
                  highLabel="Good"
                  onChange={(value) => setReadinessForm((current) => ({ ...current, sleepQuality: value }))}
                />
                <ReadinessSelector
                  label="Motivation"
                  value={readinessForm.motivationLevel}
                  lowLabel="Low"
                  mediumLabel="Steady"
                  highLabel="High"
                  onChange={(value) => setReadinessForm((current) => ({ ...current, motivationLevel: value }))}
                />
              </div>

              <label className="field">
                <span>Notes (optional)</span>
                <textarea
                  className="text-area"
                  rows={3}
                  maxLength={500}
                  value={readinessForm.notes}
                  onChange={(event) => setReadinessForm((current) => ({ ...current, notes: event.target.value }))}
                  placeholder="Short context if something is affecting training today."
                />
              </label>

              <div className="feedback-stack">
                {hasTodayReadinessLog && readinessLog ? (
                  <p className="feedback success">
                    Today&apos;s status: {readinessLog.readinessLabel}. Energy {readinessLog.energyLevel}/3, soreness{' '}
                    {readinessLog.sorenessLevel}/3, sleep {readinessLog.sleepQuality}/3, motivation{' '}
                    {readinessLog.motivationLevel}/3.
                  </p>
                ) : (
                  <p className="feedback">No check-in logged yet today. A 10-second update improves today&apos;s recommendation.</p>
                )}
                {readinessMessage ? <p className="feedback success">{readinessMessage}</p> : null}
                {readinessErrorMessage ? <p className="feedback error">{readinessErrorMessage}</p> : null}
              </div>

              <div className="action-row">
                <button type="submit" className="primary-button" disabled={isSavingReadiness}>
                  {isSavingReadiness ? 'Saving check-in...' : hasTodayReadinessLog ? 'Update today’s check-in' : 'Save today’s check-in'}
                </button>
              </div>
            </form>
          )}
        </div>

        <div className="panel dashboard-panel-weekly">
          <div className="panel-header">
            <div>
              <h2>Weekly stats</h2>
              <p>Current week snapshot with the most useful totals first.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading weekly stats" description="Pulling your latest dashboard summary." loading />
          ) : errorMessage ? (
            <StateCard title="Dashboard unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="dashboard-metric-list dashboard-weekly-grid">
              <div className="dashboard-metric-card dashboard-weekly-card">
                <span className="stat-label">Workout count</span>
                <strong>{metrics.workoutsThisWeek}</strong>
                <span className="stat-subtext">Completed sessions logged this week</span>
              </div>
              <div className="dashboard-metric-card dashboard-weekly-card">
                <span className="stat-label">Exercises logged</span>
                <strong>{metrics.exercisesThisWeek}</strong>
                <span className="stat-subtext">Total exercise entries across this week</span>
              </div>
              <div className="dashboard-metric-card dashboard-weekly-card">
                <span className="stat-label">Weigh-ins</span>
                <strong>{metrics.weighInsThisWeek}</strong>
                <span className="stat-subtext">Entries captured during the current week</span>
              </div>
              <div className="dashboard-metric-card dashboard-weekly-card">
                <span className="stat-label">Weekly avg body weight</span>
                <strong>
                  {metrics.averageBodyWeightThisWeek === null
                    ? 'No data'
                    : `${metrics.averageBodyWeightThisWeek} kg`}
                </strong>
                <span className="stat-subtext">Average of this week&apos;s weigh-ins</span>
              </div>
            </div>
          )}
        </div>

        <div className="panel panel-span-2 dashboard-panel-assistant">
          <div className="panel-header">
            <div>
              <h2>Workout assistant</h2>
              <p>Simple coaching cues based on your logged history, readiness, and weekly target.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard
              title="Loading suggestions"
              description="Reviewing recent training patterns and weekly progress."
              loading
            />
          ) : errorMessage ? (
            <StateCard title="Suggestions unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="assistant-grid dashboard-assistant-grid">
              <article className="assistant-card assistant-card-highlight">
                <span className="stat-label">Consistency nudge</span>
                <strong>Weekly focus</strong>
                <p>{assistantInsight.weeklyNudge}</p>
                <span className="record-hint">Based on your current weekly workout target.</span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">Today&apos;s training suggestion</span>
                <strong>{assistantInsight.todaySuggestion.title}</strong>
                <p>{assistantInsight.todaySuggestion.message}</p>
                <span className="record-hint">
                  Recommended focus: {assistantInsight.todaySuggestion.trainingType}
                </span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">PR opportunity</span>
                <strong>
                  {assistantInsight.prOpportunity
                    ? assistantInsight.prOpportunity.exerciseName
                    : 'No clear push today'}
                </strong>
                <p>
                  {assistantInsight.prOpportunity
                    ? assistantInsight.prOpportunity.message
                    : 'Build another few sessions before pushing for a heavier top set.'}
                </p>
                <span className="record-hint">
                  {assistantInsight.prOpportunity?.targetWeightKg
                    ? `Suggested target: ${assistantInsight.prOpportunity.targetWeightKg} kg`
                    : 'Suggestions use recent logged sets rather than fixed programming.'}
                </span>
              </article>

              <article className="assistant-card">
                <span className="stat-label">Revisit next</span>
                <strong>
                  {assistantInsight.revisitSuggestions.length === 0
                    ? 'No overdue exercises'
                    : `${assistantInsight.revisitSuggestions.length} exercise suggestions`}
                </strong>
                {assistantInsight.revisitSuggestions.length === 0 ? (
                  <p>You have touched your recent exercise rotation enough to avoid obvious gaps.</p>
                ) : (
                  <div className="assistant-list">
                    {assistantInsight.revisitSuggestions.map((suggestion) => (
                      <div key={`${suggestion.exerciseName}-${suggestion.lastTrainedAt}`} className="assistant-list-item">
                        <strong>{suggestion.exerciseName}</strong>
                        <span>{suggestion.message}</span>
                      </div>
                    ))}
                  </div>
                )}
              </article>

              {cycleGuidance?.isEnabled ? (
                <article className="assistant-card">
                  <span className="stat-label">Cycle-aware guidance</span>
                  <strong>
                    {cycleGuidance.estimatedCurrentPhase
                      ? `${cycleGuidance.estimatedCurrentPhase} phase`
                      : 'Prediction building'}
                  </strong>
                  <p>{cycleGuidance.guidanceMessage}</p>
                  <span className="record-hint">
                    {cycleGuidance.currentCycleDay
                      ? `Day ${cycleGuidance.currentCycleDay}`
                      : 'Add more history for a better estimate'}
                    {cycleGuidance.estimatedNextPeriodStartDate
                      ? ` • Next period around ${formatDate(cycleGuidance.estimatedNextPeriodStartDate)}`
                      : ''}
                  </span>
                </article>
              ) : null}
            </div>
          )}
        </div>

        <div className="panel panel-span-2 dashboard-panel-overview">
          <div className="panel-header">
            <div>
              <h2>Overview</h2>
              <p>Recent activity and all-time context for quick reference.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading overview" description="Collecting recent activity and totals." loading />
          ) : errorMessage ? (
            <StateCard title="Dashboard unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="dashboard-metric-list dashboard-overview-grid">
              <div className="dashboard-metric-card dashboard-overview-card">
                <span className="stat-label">Total workouts</span>
                <strong>{metrics.totalWorkouts}</strong>
                <span className="stat-subtext">All recorded sessions</span>
              </div>
              <div className="dashboard-metric-card dashboard-overview-card">
                <span className="stat-label">Latest workout</span>
                <strong>{metrics.latestWorkout ? formatDate(metrics.latestWorkout.date) : 'No data'}</strong>
                <span className="stat-subtext">Most recent logged training day</span>
              </div>
              <div className="dashboard-metric-card dashboard-overview-card">
                <span className="stat-label">Latest weigh-in</span>
                <strong>
                  {metrics.latestWeightEntry
                    ? `${metrics.latestWeightEntry.weightKg} kg`
                    : 'No data'}
                </strong>
                <span className="stat-subtext">Latest recorded body weight</span>
              </div>
              <div className="dashboard-metric-card dashboard-overview-card">
                <span className="stat-label">Last weigh-in date</span>
                <strong>
                  {metrics.latestWeightEntry ? formatDate(metrics.latestWeightEntry.date) : 'No data'}
                </strong>
                <span className="stat-subtext">Most recent date in weight history</span>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
  )
}

function ForgeStatCard({
  tone,
  label,
  value,
  unit,
  description,
  trend,
}: {
  tone: 'lime' | 'blue' | 'teal' | 'violet'
  label: string
  value: string
  unit?: string
  description: string
  trend?: string
}) {
  return (
    <article className={`forge-stat-card forge-stat-card-${tone}`}>
      <div className="forge-stat-glow" aria-hidden="true" />
      <span className="stat-label">{label}</span>
      <strong>
        {value}
        {unit ? <span>{unit}</span> : null}
      </strong>
      <p>{description}</p>
      {trend ? <span className="forge-stat-trend">{trend}</span> : null}
    </article>
  )
}

type ReadinessSelectorProps = {
  label: string
  value: number
  lowLabel: string
  mediumLabel: string
  highLabel: string
  onChange: (value: number) => void
}

function ReadinessSelector({
  label,
  value,
  lowLabel,
  mediumLabel,
  highLabel,
  onChange,
}: ReadinessSelectorProps) {
  return (
    <div className="readiness-selector">
      <span>{label}</span>
      <div className="readiness-option-row" role="group" aria-label={label}>
        {[1, 2, 3].map((option) => (
          <button
            key={option}
            type="button"
            className={option === value ? 'readiness-option readiness-option-active' : 'readiness-option'}
            onClick={() => onChange(option)}
          >
            {option === 1 ? lowLabel : option === 2 ? mediumLabel : highLabel}
          </button>
        ))}
      </div>
    </div>
  )
}

function startOfWeek(date: Date) {
  const result = new Date(date)
  const day = result.getDay()
  const diff = (day + 6) % 7
  result.setHours(0, 0, 0, 0)
  result.setDate(result.getDate() - diff)
  return result
}

function addDays(date: Date, days: number) {
  const result = new Date(date)
  result.setDate(result.getDate() + days)
  return result
}

function getWorkoutWeekStreak(workouts: Workout[]) {
  if (workouts.length === 0) {
    return 0
  }

  const uniqueWorkoutWeeks = new Set(
    workouts.map((workout) => startOfWeek(new Date(workout.date)).toISOString().slice(0, 10)),
  )

  let streak = 0
  let cursor = startOfWeek(new Date())

  while (uniqueWorkoutWeeks.has(cursor.toISOString().slice(0, 10))) {
    streak += 1
    cursor = addDays(cursor, -7)
  }

  return streak
}

function getBodyWeightGoalProgress(
  currentWeightKg: number | null,
  targetWeightKg: number | null,
  fitnessPhase: GoalSettings['fitnessPhase'],
) {
  if (targetWeightKg === null) {
    return {
      percentage: 0,
      message: 'Set a target body weight to track progress here.',
    }
  }

  if (currentWeightKg === null) {
    return {
      percentage: 0,
      message: `Target is ${targetWeightKg} kg. Add a weigh-in to start tracking progress.`,
    }
  }

  const difference = Number((currentWeightKg - targetWeightKg).toFixed(1))

  if (fitnessPhase === 'bulk') {
    if (difference >= 0) {
      return { percentage: 100, message: `Target reached. Current body weight is ${currentWeightKg} kg.` }
    }

    return {
      percentage: Math.max(10, Math.min(100, 100 - Math.abs(difference) * 10)),
      message: `${Math.abs(difference).toFixed(1)} kg to gain to reach ${targetWeightKg} kg.`,
    }
  }

  if (fitnessPhase === 'cut') {
    if (difference <= 0) {
      return { percentage: 100, message: `Target reached. Current body weight is ${currentWeightKg} kg.` }
    }

    return {
      percentage: Math.max(10, Math.min(100, 100 - Math.abs(difference) * 10)),
      message: `${difference.toFixed(1)} kg to lose to reach ${targetWeightKg} kg.`,
    }
  }

  if (Math.abs(difference) <= 0.5) {
    return {
      percentage: 100,
      message: `You are within 0.5 kg of your maintenance target at ${currentWeightKg} kg.`,
    }
  }

  return {
    percentage: Math.max(10, Math.min(100, 100 - Math.abs(difference) * 20)),
    message: `${Math.abs(difference).toFixed(1)} kg away from your maintenance target.`,
  }
}

function getWeeklyWorkoutGoalProgress(workoutCount: number, weeklyWorkoutTarget: number | null) {
  if (weeklyWorkoutTarget === null) {
    return {
      percentage: 0,
      message: 'Set a weekly workout target to track consistency.',
    }
  }

  const percentage = Math.min(100, Math.round((workoutCount / weeklyWorkoutTarget) * 100))

  if (workoutCount >= weeklyWorkoutTarget) {
    return {
      percentage,
      message: `Weekly target met with ${workoutCount} workouts logged this week.`,
    }
  }

  return {
    percentage,
    message: `${weeklyWorkoutTarget - workoutCount} more workout${weeklyWorkoutTarget - workoutCount === 1 ? '' : 's'} needed this week.`,
  }
}

function formatPhase(fitnessPhase: GoalSettings['fitnessPhase']) {
  return fitnessPhase.charAt(0).toUpperCase() + fitnessPhase.slice(1)
}

function getDashboardStatus(workoutsThisWeek: number, weeklyWorkoutTarget: number | null) {
  if (weeklyWorkoutTarget === null) {
    return 'Target not set'
  }

  if (workoutsThisWeek >= weeklyWorkoutTarget) {
    return 'Weekly target met'
  }

  if (workoutsThisWeek === 0) {
    return 'Week not started'
  }

  return 'On the climb'
}

function getPhaseSummary(fitnessPhase: GoalSettings['fitnessPhase']) {
  switch (fitnessPhase) {
    case 'cut':
      return 'Body-weight progress is framed around moving down toward your target.'
    case 'bulk':
      return 'Body-weight progress is framed around moving up toward your target.'
    default:
      return 'Body-weight progress is framed around staying close to your target.'
  }
}
