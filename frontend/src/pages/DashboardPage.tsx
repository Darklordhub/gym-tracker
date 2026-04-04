import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { fetchGoals, updateGoals } from '../api/goals'
import { fetchWeightEntries } from '../api/weightEntries'
import { fetchWorkouts } from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { GoalSettings, GoalSettingsPayload } from '../types/goals'
import type { WeightEntry } from '../types/weight'
import type { Workout } from '../types/workout'

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

  async function loadDashboard() {
    try {
      setIsLoading(true)
      setErrorMessage(null)
      setGoalMessage(null)
      setGoalErrorMessage(null)

      const [workoutData, weightData, goalsData] = await Promise.all([
        fetchWorkouts(),
        fetchWeightEntries(),
        fetchGoals(),
      ])

      setWorkouts(workoutData)
      setWeightEntries(weightData)
      applyGoalState(goalsData)
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

  return (
    <main className="page-shell">
      <section className="hero-panel">
        <div className="hero-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Dashboard</h1>
          <p className="hero-text">
            Weekly activity, recent body-weight trend, and a simple consistency check in one place.
          </p>
        </div>

        <div className="stats-grid">
          <article className="stat-card">
            <span className="stat-label">This Week</span>
            <strong>{metrics.workoutsThisWeek}</strong>
            <span className="stat-subtext">Workouts logged this week</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Average Weight</span>
            <strong>{metrics.averageBodyWeight === null ? 'No data' : `${metrics.averageBodyWeight} kg`}</strong>
            <span className="stat-subtext">Average across all weigh-ins</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Workout Streak</span>
            <strong>{metrics.workoutStreakWeeks}</strong>
            <span className="stat-subtext">Consecutive active weeks</span>
          </article>
          <article className="stat-card">
            <span className="stat-label">Current Phase</span>
            <strong>{formatPhase(goals.fitnessPhase)}</strong>
            <span className="stat-subtext">Active goal setting mode</span>
          </article>
        </div>
      </section>

      <section className="content-grid dashboard-grid">
        <div className="panel panel-span-2">
          <div className="panel-header">
            <div>
              <h2>Goals</h2>
              <p>Set simple weekly targets and body-weight direction.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading goals" description="Pulling your current goal settings." loading />
          ) : errorMessage ? (
            <StateCard title="Goals unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="goals-grid">
              <form className="goal-form" onSubmit={handleGoalSubmit}>
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
                  <button type="submit" disabled={isSavingGoals}>
                    {isSavingGoals ? 'Saving goals...' : 'Save goals'}
                  </button>
                </div>

                {goalMessage ? <p className="feedback success">{goalMessage}</p> : null}
                {goalErrorMessage ? <p className="feedback error">{goalErrorMessage}</p> : null}
              </form>

              <div className="goal-progress-list">
                <article className="goal-progress-card">
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

                <article className="goal-progress-card">
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

                <article className="goal-progress-card">
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

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Weekly stats</h2>
              <p>Current week snapshot.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading weekly stats" description="Pulling your latest dashboard summary." loading />
          ) : errorMessage ? (
            <StateCard title="Dashboard unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="dashboard-metric-list">
              <div className="dashboard-metric-card">
                <span className="stat-label">Workout count</span>
                <strong>{metrics.workoutsThisWeek}</strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Exercises logged</span>
                <strong>{metrics.exercisesThisWeek}</strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Weigh-ins</span>
                <strong>{metrics.weighInsThisWeek}</strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Weekly avg body weight</span>
                <strong>
                  {metrics.averageBodyWeightThisWeek === null
                    ? 'No data'
                    : `${metrics.averageBodyWeightThisWeek} kg`}
                </strong>
              </div>
            </div>
          )}
        </div>

        <div className="panel">
          <div className="panel-header">
            <div>
              <h2>Overview</h2>
              <p>Simple totals and latest activity.</p>
            </div>
          </div>

          {isLoading ? (
            <StateCard title="Loading overview" description="Collecting recent activity and totals." loading />
          ) : errorMessage ? (
            <StateCard title="Dashboard unavailable" description={errorMessage} tone="error" />
          ) : (
            <div className="dashboard-metric-list">
              <div className="dashboard-metric-card">
                <span className="stat-label">Total workouts</span>
                <strong>{metrics.totalWorkouts}</strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Latest workout</span>
                <strong>{metrics.latestWorkout ? formatDate(metrics.latestWorkout.date) : 'No data'}</strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Latest weigh-in</span>
                <strong>
                  {metrics.latestWeightEntry
                    ? `${metrics.latestWeightEntry.weightKg} kg`
                    : 'No data'}
                </strong>
              </div>
              <div className="dashboard-metric-card">
                <span className="stat-label">Last weigh-in date</span>
                <strong>
                  {metrics.latestWeightEntry ? formatDate(metrics.latestWeightEntry.date) : 'No data'}
                </strong>
              </div>
            </div>
          )}
        </div>
      </section>
    </main>
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
