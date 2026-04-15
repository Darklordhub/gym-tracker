import type { CalorieLog } from '../types/calories'
import type { GoalSettings } from '../types/goals'
import type { Workout } from '../types/workout'

export type DailyCalorieBalance = {
  date: string
  targetCalories: number | null
  targetSource: 'manual' | 'goal-based' | 'unset'
  caloriesConsumed: number | null
  caloriesBurned: number
  netBalanceCalories: number | null
  status: 'deficit' | 'balanced' | 'surplus' | 'target-not-set' | 'no-intake'
  statusLabel: string
  statusMessage: string
}

export function estimateWorkoutCaloriesBurned(workout: Workout) {
  if (workout.workoutType === 'cardio') {
    const factor =
      workout.cardioIntensity === 'high' ? 11 : workout.cardioIntensity === 'low' ? 5 : 8

    return Math.round((workout.cardioDurationMinutes ?? 0) * factor)
  }

  const exerciseCount = workout.exerciseEntries.length
  const setCount = workout.exerciseEntries.reduce((total, exercise) => total + exercise.sets.length, 0)
  return exerciseCount === 0 ? 0 : exerciseCount * 12 + setCount * 7
}

export function resolveDailyCalorieTarget(
  goals: GoalSettings | null,
  referenceWeightKg?: number | null,
) {
  if (!goals) {
    return { targetCalories: null, source: 'unset' as const }
  }

  if (goals.calorieTargetMode === 'manual') {
    return {
      targetCalories: goals.dailyCalorieTarget,
      source: goals.dailyCalorieTarget === null ? ('unset' as const) : ('manual' as const),
    }
  }

  const weightKg = goals.targetBodyWeightKg ?? referenceWeightKg ?? null
  if (weightKg === null) {
    return { targetCalories: null, source: 'unset' as const }
  }

  const multiplier =
    goals.fitnessPhase === 'cut' ? 27 : goals.fitnessPhase === 'bulk' ? 34 : 31

  return {
    targetCalories: roundCalories(weightKg * multiplier),
    source: 'goal-based' as const,
  }
}

export function buildDailyCalorieBalance(
  workouts: Workout[],
  goals: GoalSettings | null,
  calorieLog: CalorieLog | null,
  date: string,
  referenceWeightKg?: number | null,
): DailyCalorieBalance {
  const dayWorkouts = workouts.filter((workout) => workout.date.slice(0, 10) === date)
  const caloriesBurned = dayWorkouts.reduce((total, workout) => total + estimateWorkoutCaloriesBurned(workout), 0)
  const target = resolveDailyCalorieTarget(goals, referenceWeightKg)
  const caloriesConsumed = calorieLog?.date === date ? calorieLog.caloriesConsumed : null

  if (target.targetCalories === null) {
    return {
      date,
      targetCalories: null,
      targetSource: target.source,
      caloriesConsumed,
      caloriesBurned,
      netBalanceCalories: caloriesConsumed === null ? null : caloriesConsumed - caloriesBurned,
      status: 'target-not-set',
      statusLabel: 'Target not set',
      statusMessage: 'Add a manual calorie target or enable goal-based target calculation.',
    }
  }

  if (caloriesConsumed === null) {
    return {
      date,
      targetCalories: target.targetCalories,
      targetSource: target.source,
      caloriesConsumed: null,
      caloriesBurned,
      netBalanceCalories: null,
      status: 'no-intake',
      statusLabel: 'Log calories',
      statusMessage: 'Add today’s calories to see whether you are in a deficit, balanced, or surplus state.',
    }
  }

  const netBalanceCalories = caloriesConsumed - caloriesBurned - target.targetCalories

  if (netBalanceCalories <= -200) {
    return {
      date,
      targetCalories: target.targetCalories,
      targetSource: target.source,
      caloriesConsumed,
      caloriesBurned,
      netBalanceCalories,
      status: 'deficit',
      statusLabel: 'Deficit',
      statusMessage: `${Math.abs(netBalanceCalories)} kcal under your daily target after training burn.`,
    }
  }

  if (netBalanceCalories >= 200) {
    return {
      date,
      targetCalories: target.targetCalories,
      targetSource: target.source,
      caloriesConsumed,
      caloriesBurned,
      netBalanceCalories,
      status: 'surplus',
      statusLabel: 'Surplus',
      statusMessage: `${netBalanceCalories} kcal above your daily target after training burn.`,
    }
  }

  return {
    date,
    targetCalories: target.targetCalories,
    targetSource: target.source,
    caloriesConsumed,
    caloriesBurned,
    netBalanceCalories,
    status: 'balanced',
    statusLabel: 'Balanced',
    statusMessage: 'You are close to your current daily calorie target after training burn.',
  }
}

function roundCalories(value: number) {
  return Math.round(value / 25) * 25
}
