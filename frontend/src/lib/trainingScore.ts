import { buildDailyCalorieBalance, type DailyCalorieBalance } from './calorieBalance'
import { countWorkoutsInWeek } from './workoutMetrics'
import type { CalorieLog } from '../types/calories'
import type { CycleGuidance } from '../types/cycle'
import type { GoalSettings } from '../types/goals'
import type { ReadinessLog } from '../types/readiness'
import type { Workout } from '../types/workout'

export type DailyTrainingScore = {
  score: number
  label: string
  summary: string
  highlights: string[]
  breakdown: {
    activity: number
    recovery: number
    fueling: number
    alignment: number
  }
}

export type WeeklyTrainingScoreSummary = {
  averageScore: number
  label: string
  summary: string
}

export function buildDailyTrainingScore(params: {
  workouts: Workout[]
  goals: GoalSettings | null
  calorieLog: CalorieLog | null
  readinessLog: ReadinessLog | null
  cycleGuidance?: CycleGuidance | null
  date: string
  referenceWeightKg?: number | null
}): { score: DailyTrainingScore; calorieBalance: DailyCalorieBalance } {
  const calorieBalance = buildDailyCalorieBalance(
    params.workouts,
    params.goals,
    params.calorieLog,
    params.date,
    params.referenceWeightKg,
  )
  const dayWorkouts = params.workouts.filter((workout) => workout.date.slice(0, 10) === params.date)
  const workoutsUpToDate = params.workouts.filter((workout) => workout.date.slice(0, 10) <= params.date)
  const readinessLog = params.readinessLog?.date === params.date ? params.readinessLog : null
  const activity = scoreActivity(dayWorkouts, readinessLog, params.cycleGuidance)
  const recovery = scoreRecovery(dayWorkouts, readinessLog, params.cycleGuidance)
  const fueling = scoreFueling(calorieBalance, params.goals?.fitnessPhase ?? 'maintain', dayWorkouts.length > 0)
  const alignment = scoreAlignment(workoutsUpToDate, params.goals, dayWorkouts.length > 0, params.date)

  const score = Math.max(0, Math.min(100, activity + recovery + fueling + alignment))
  const highlights = buildHighlights({
    dayWorkouts,
    readinessLog,
    cycleGuidance: params.cycleGuidance,
    calorieBalance,
    goals: params.goals,
    alignment,
  })

  return {
    calorieBalance,
    score: {
      score,
      label: score >= 80 ? 'Strong day' : score >= 65 ? 'Solid day' : score >= 45 ? 'Mixed day' : 'Recovery day',
      summary: highlights[0] ?? 'Training score is using activity, recovery, fueling, and goal alignment.',
      highlights,
      breakdown: {
        activity,
        recovery,
        fueling,
        alignment,
      },
    },
  }
}

export function buildWeeklyTrainingScoreSummary(params: {
  workouts: Workout[]
  goals: GoalSettings | null
  calorieLogs: CalorieLog[]
  readinessLogs: ReadinessLog[]
  referenceWeightKg?: number | null
  now?: Date
}) {
  const now = params.now ?? new Date()
  const dailyScores: number[] = []

  for (let offset = 0; offset < 7; offset += 1) {
    const date = new Date(now)
    date.setHours(0, 0, 0, 0)
    date.setDate(date.getDate() - offset)
    const dateKey = date.toISOString().slice(0, 10)
    const readinessLog = params.readinessLogs.find((log) => log.date === dateKey) ?? null
    const calorieLog = params.calorieLogs.find((log) => log.date === dateKey) ?? null

    dailyScores.push(
      buildDailyTrainingScore({
        workouts: params.workouts,
        goals: params.goals,
        calorieLog,
        readinessLog,
        date: dateKey,
        referenceWeightKg: params.referenceWeightKg,
      }).score.score,
    )
  }

  const averageScore = Math.round(dailyScores.reduce((sum, score) => sum + score, 0) / dailyScores.length)
  return {
    averageScore,
    label:
      averageScore >= 80 ? 'Strong week' : averageScore >= 65 ? 'Solid week' : averageScore >= 45 ? 'Mixed week' : 'Recovery-heavy week',
    summary:
      averageScore >= 80
        ? 'Training, recovery, and fueling are lining up well this week.'
        : averageScore >= 65
          ? 'The week is generally aligned, with a few softer recovery or fueling signals.'
          : averageScore >= 45
            ? 'Some parts of the week are working, but readiness, fueling, or consistency are reducing the score.'
            : 'Recovery, fueling, or consistency are out of sync with workload right now.',
  } satisfies WeeklyTrainingScoreSummary
}

function scoreActivity(
  dayWorkouts: Workout[],
  readinessLog: ReadinessLog | null,
  cycleGuidance?: CycleGuidance | null,
) {
  if (dayWorkouts.length === 0) {
    if (
      readinessLog?.readinessScore !== undefined &&
      readinessLog.readinessScore < 1.9 &&
      (cycleGuidance?.isHigherFatiguePhase || readinessLog.sorenessLevel >= 3)
    ) {
      return 24
    }

    return 10
  }

  const strengthSessions = dayWorkouts.filter((workout) => workout.workoutType !== 'cardio')
  const cardioSessions = dayWorkouts.filter((workout) => workout.workoutType === 'cardio')
  const cardioMinutes = cardioSessions.reduce((sum, workout) => sum + (workout.cardioDurationMinutes ?? 0), 0)

  if (strengthSessions.length > 0 && cardioSessions.length > 0) {
    return cardioMinutes >= 45 ? 28 : 26
  }

  if (strengthSessions.length > 0) {
    return 24
  }

  if (cardioMinutes >= 40) {
    return 22
  }

  return 18
}

function scoreRecovery(
  dayWorkouts: Workout[],
  readinessLog: ReadinessLog | null,
  cycleGuidance?: CycleGuidance | null,
) {
  const hasWorkout = dayWorkouts.length > 0
  const base =
    readinessLog === null
      ? 14
      : readinessLog.readinessScore >= 2.5
        ? 22
        : readinessLog.readinessScore >= 1.9
          ? 17
          : 10

  let score = base

  if (!hasWorkout && readinessLog && readinessLog.readinessScore < 1.9) {
    score += 6
  }

  if (cycleGuidance?.isHigherFatiguePhase) {
    score += hasWorkout ? -3 : 3
  }

  return clamp(score, 6, 25)
}

function scoreFueling(
  calorieBalance: DailyCalorieBalance,
  fitnessPhase: GoalSettings['fitnessPhase'],
  hasWorkout: boolean,
) {
  if (calorieBalance.status === 'target-not-set' || calorieBalance.status === 'no-intake') {
    return 12
  }

  if (fitnessPhase === 'cut') {
    if (calorieBalance.status === 'deficit') {
      return calorieBalance.netBalanceCalories !== null && calorieBalance.netBalanceCalories < -650 ? 16 : 24
    }

    if (calorieBalance.status === 'balanced') {
      return 19
    }

    return 9
  }

  if (fitnessPhase === 'bulk') {
    if (calorieBalance.status === 'surplus') {
      return calorieBalance.netBalanceCalories !== null && calorieBalance.netBalanceCalories > 700 ? 17 : 24
    }

    if (calorieBalance.status === 'balanced') {
      return 18
    }

    return hasWorkout ? 10 : 12
  }

  if (calorieBalance.status === 'balanced') {
    return 25
  }

  if (Math.abs(calorieBalance.netBalanceCalories ?? 0) <= 350) {
    return 18
  }

  return hasWorkout ? 11 : 13
}

function scoreAlignment(
  workoutsUpToDate: Workout[],
  goals: GoalSettings | null,
  hasWorkout: boolean,
  date: string,
) {
  if (goals?.weeklyWorkoutTarget === null || goals?.weeklyWorkoutTarget === undefined) {
    return 12
  }

  const workoutsThisWeek = countWorkoutsInWeek(workoutsUpToDate, new Date(date))
  const progress = workoutsThisWeek / goals.weeklyWorkoutTarget

  if (progress >= 1) {
    return 20
  }

  if (progress >= 0.66) {
    return hasWorkout ? 17 : 15
  }

  if (progress >= 0.33) {
    return hasWorkout ? 15 : 12
  }

  return hasWorkout ? 13 : 9
}

function buildHighlights(params: {
  dayWorkouts: Workout[]
  readinessLog: ReadinessLog | null
  cycleGuidance?: CycleGuidance | null
  calorieBalance: DailyCalorieBalance
  goals: GoalSettings | null
  alignment: number
}) {
  const highlights: string[] = []

  if (params.dayWorkouts.length > 0) {
    highlights.push(
      params.dayWorkouts.some((workout) => workout.workoutType !== 'cardio')
        ? 'You logged training today, which keeps the activity score moving.'
        : 'Cardio activity is carrying today’s activity score without overloading recovery.',
    )
  } else {
    highlights.push('No training is logged today, so the score leans more on recovery and fueling alignment.')
  }

  if (params.readinessLog) {
    highlights.push(
      params.readinessLog.readinessScore >= 2.5
        ? 'Recovery signals look strong today.'
        : params.readinessLog.readinessScore >= 1.9
          ? 'Recovery signals are steady but not especially sharp today.'
          : 'Low readiness is reducing the score today.',
    )
  }

  if (params.cycleGuidance?.isHigherFatiguePhase) {
    highlights.push('Cycle-aware guidance is treating today as a higher-fatigue phase, which lowers the tolerance for hard work.')
  }

  if (params.calorieBalance.status === 'deficit') {
    highlights.push(
      params.goals?.fitnessPhase === 'cut'
        ? 'Calorie balance is supporting a cut, but a deeper deficit will still reduce recovery quality.'
        : 'You are running in a calorie deficit today, which reduces recovery support for training.',
    )
  } else if (params.calorieBalance.status === 'surplus') {
    highlights.push(
      params.goals?.fitnessPhase === 'bulk'
        ? 'A moderate surplus is supporting growth and recovery today.'
        : 'Calorie intake is running above target, which is reducing goal alignment today.',
    )
  } else if (params.calorieBalance.status === 'balanced') {
    highlights.push('Fueling is close to target after training burn, which helps the score.')
  } else if (params.calorieBalance.status === 'no-intake') {
    highlights.push('Calories are not logged yet, so the fueling score is conservative.')
  }

  if (params.alignment >= 17) {
    highlights.push('Weekly training consistency is aligned well with your current goal.')
  } else if (params.alignment <= 10) {
    highlights.push('Weekly training consistency is lagging behind your current goal.')
  }

  return highlights.slice(0, 3)
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, Math.round(value)))
}
