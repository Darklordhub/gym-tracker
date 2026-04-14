import type { Workout } from '../types/workout'

export function startOfWeek(date: Date) {
  const result = new Date(date)
  const day = result.getDay()
  const diff = day
  result.setHours(0, 0, 0, 0)
  result.setDate(result.getDate() - diff)
  return result
}

export function addDays(date: Date, days: number) {
  const result = new Date(date)
  result.setDate(result.getDate() + days)
  return result
}

export function countWorkoutsInWeek(workouts: Workout[], now = new Date()) {
  const weekStart = startOfWeek(now)
  const weekEnd = addDays(weekStart, 7)

  return workouts.filter((workout) => {
    const workoutDate = new Date(workout.date)
    return workoutDate >= weekStart && workoutDate < weekEnd
  }).length
}

export function getWorkoutWeekStreak(workouts: Workout[], now = new Date()) {
  if (workouts.length === 0) {
    return 0
  }

  const uniqueWorkoutWeeks = new Set(
    workouts.map((workout) => startOfWeek(new Date(workout.date)).toISOString().slice(0, 10)),
  )

  let streak = 0
  let cursor = startOfWeek(now)

  while (uniqueWorkoutWeeks.has(cursor.toISOString().slice(0, 10))) {
    streak += 1
    cursor = addDays(cursor, -7)
  }

  return streak
}
