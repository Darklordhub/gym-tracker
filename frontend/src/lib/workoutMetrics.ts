import type { Workout } from '../types/workout'
import { addDaysToDateOnly, compareDateOnlyValues, startOfWeekDateOnly, todayLocalDateOnly } from './dateOnly'

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
  const weekStart = startOfWeekDateOnly(now)
  const weekEnd = addDaysToDateOnly(weekStart, 7)

  return workouts.filter((workout) => {
    return compareDateOnlyValues(workout.date, weekStart) >= 0 && compareDateOnlyValues(workout.date, weekEnd) < 0
  }).length
}

export function getWorkoutWeekStreak(workouts: Workout[], now = new Date()) {
  if (workouts.length === 0) {
    return 0
  }

  const uniqueWorkoutWeeks = new Set(
    workouts.map((workout) => startOfWeekDateOnly(workout.date)),
  )

  let streak = 0
  let cursor = startOfWeekDateOnly(todayLocalDateOnly(now))

  while (uniqueWorkoutWeeks.has(cursor)) {
    streak += 1
    cursor = addDaysToDateOnly(cursor, -7)
  }

  return streak
}
