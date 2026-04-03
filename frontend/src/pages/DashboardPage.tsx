import { useEffect, useMemo, useState } from 'react'
import { fetchWeightEntries } from '../api/weightEntries'
import { fetchWorkouts } from '../api/workouts'
import { StateCard } from '../components/StateCard'
import { formatDate } from '../lib/format'
import { getRequestErrorMessage } from '../lib/http'
import type { WeightEntry } from '../types/weight'
import type { Workout } from '../types/workout'

export function DashboardPage() {
  const [workouts, setWorkouts] = useState<Workout[]>([])
  const [weightEntries, setWeightEntries] = useState<WeightEntry[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

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
    }
  }, [weightEntries, workouts])

  async function loadDashboard() {
    try {
      setIsLoading(true)
      setErrorMessage(null)

      const [workoutData, weightData] = await Promise.all([
        fetchWorkouts(),
        fetchWeightEntries(),
      ])

      setWorkouts(workoutData)
      setWeightEntries(weightData)
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to load dashboard data.'))
    } finally {
      setIsLoading(false)
    }
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
        </div>
      </section>

      <section className="content-grid dashboard-grid">
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
