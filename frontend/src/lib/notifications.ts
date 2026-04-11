import { getWorkoutAssistantInsight } from './exerciseSuggestions'
import type { GoalSettings } from '../types/goals'
import type { Workout } from '../types/workout'

export type AppNotification = {
  id: string
  type: 'weekly-goal-reminder' | 'inactivity-reminder' | 'pr-opportunity' | 'goal-achievement'
  title: string
  message: string
  createdAt: string
}

export function generateNotifications(
  workouts: Workout[],
  goals: GoalSettings | null,
  now = new Date(),
) {
  const notifications: Array<AppNotification & { priority: number }> = []
  const sortedWorkouts = [...workouts].sort(
    (left, right) => new Date(right.date).getTime() - new Date(left.date).getTime() || right.id - left.id,
  )
  const latestWorkout = sortedWorkouts[0] ?? null
  const weekStart = startOfWeek(now)
  const weekStartKey = weekStart.toISOString().slice(0, 10)
  const workoutsThisWeek = sortedWorkouts.filter((workout) => {
    const workoutDate = new Date(workout.date)
    return workoutDate >= weekStart
  }).length
  const assistantInsight = getWorkoutAssistantInsight(sortedWorkouts, goals, undefined, undefined, now)

  if (goals?.weeklyWorkoutTarget) {
    if (workoutsThisWeek >= goals.weeklyWorkoutTarget) {
      notifications.push({
        id: `goal-achievement:${weekStartKey}:${goals.weeklyWorkoutTarget}`,
        type: 'goal-achievement',
        title: 'Weekly goal achieved',
        message: `You have already logged ${workoutsThisWeek} of ${goals.weeklyWorkoutTarget} planned workouts this week.`,
        createdAt: now.toISOString(),
        priority: 4,
      })
    } else {
      const remaining = goals.weeklyWorkoutTarget - workoutsThisWeek

      notifications.push({
        id: `weekly-goal-reminder:${weekStartKey}:${remaining}`,
        type: 'weekly-goal-reminder',
        title: 'Weekly target reminder',
        message: `${remaining} more workout${remaining === 1 ? '' : 's'} would keep you on track for this week.`,
        createdAt: now.toISOString(),
        priority: 3,
      })
    }
  }

  if (latestWorkout) {
    const daysSinceLastWorkout = getDaysSince(latestWorkout.date, now)

    if (daysSinceLastWorkout >= 5) {
      notifications.push({
        id: `inactivity-reminder:${latestWorkout.date}`,
        type: 'inactivity-reminder',
        title: 'Training has gone quiet',
        message: `Your last logged workout was ${daysSinceLastWorkout} day${daysSinceLastWorkout === 1 ? '' : 's'} ago.`,
        createdAt: latestWorkout.date,
        priority: 2,
      })
    }
  }

  if (assistantInsight.prOpportunity) {
    notifications.push({
      id: `pr-opportunity:${assistantInsight.prOpportunity.exerciseName}:${assistantInsight.prOpportunity.targetWeightKg ?? 'none'}`,
      type: 'pr-opportunity',
      title: 'Possible PR window',
      message: `${assistantInsight.prOpportunity.exerciseName}: ${assistantInsight.prOpportunity.message}`,
      createdAt: now.toISOString(),
      priority: 4,
    })
  }

  return notifications
    .sort(
      (left, right) =>
        right.priority - left.priority ||
        new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime(),
    )
    .slice(0, 5)
    .map(({ priority, ...notification }) => notification)
}

function startOfWeek(date: Date) {
  const result = new Date(date)
  const day = result.getDay()
  const diff = (day + 6) % 7
  result.setHours(0, 0, 0, 0)
  result.setDate(result.getDate() - diff)
  return result
}

function getDaysSince(date: string, now: Date) {
  const target = new Date(date)
  target.setHours(0, 0, 0, 0)
  const current = new Date(now)
  current.setHours(0, 0, 0, 0)
  return Math.max(0, Math.round((current.getTime() - target.getTime()) / 86400000))
}
