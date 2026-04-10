import type { Workout } from '../types/workout'
import type { GoalSettings } from '../types/goals'

export type ExerciseSuggestion = {
  suggestedWeightKg: number
  reason: string
  confidenceLabel?: string
  prOpportunity?: {
    message: string
    targetWeightKg: number | null
  }
}

export type ExerciseSetHistoryItem = {
  date: string
  workoutId?: number
  setOrder: number
  reps: number
  weightKg: number
}

export type WorkoutAssistantInsight = {
  weeklyNudge: string
  prOpportunity: {
    exerciseName: string
    message: string
    targetWeightKg: number | null
  } | null
  revisitSuggestions: Array<{
    exerciseName: string
    lastTrainedAt: string
    message: string
  }>
}

export function getExerciseHistory(workouts: Workout[], exerciseName: string) {
  const normalizedExerciseName = normalizeExerciseName(exerciseName)

  return workouts
    .flatMap<ExerciseSetHistoryItem>((workout) =>
      workout.exerciseEntries
        .filter((exercise) => normalizeExerciseName(exercise.exerciseName) === normalizedExerciseName)
        .flatMap((exercise) =>
          exercise.sets.map((set) => ({
            date: workout.date,
            workoutId: workout.id,
            setOrder: set.order,
            reps: set.reps,
            weightKg: set.weightKg,
          })),
        ),
    )
    .sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())
}

export function getSuggestedNextWeight(
  workouts: Workout[],
  exerciseName: string,
  setOrder: number | null,
  reps: number | null,
) {
  const history = getExerciseHistory(workouts, exerciseName)

  if (history.length === 0) {
    return null
  }

  const latest = history[0]
  const recentWindow = history.slice(0, 3)
  const recentAverage =
    recentWindow.length > 0
      ? Number(
          (
            recentWindow.reduce((sum, entry) => sum + entry.weightKg, 0) / recentWindow.length
          ).toFixed(1),
        )
      : latest.weightKg
  const personalBest = Math.max(...history.map((entry) => entry.weightKg))
  const prOpportunity = getPrOpportunity(latest.weightKg, personalBest)

  if (setOrder && reps) {
    const exactMatch = history.find((entry) => entry.setOrder === setOrder && entry.reps === reps)

    if (exactMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(exactMatch.weightKg),
        reason: `Last set ${setOrder} for ${reps} reps was ${exactMatch.weightKg} kg.`,
        confidenceLabel: 'Matched exact set and reps',
        prOpportunity: prOpportunity ?? undefined,
      } satisfies ExerciseSuggestion
    }
  }

  if (reps) {
    const repMatch = history.find((entry) => entry.reps === reps)

    if (repMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(repMatch.weightKg),
        reason: `Last ${reps}-rep set was ${repMatch.weightKg} kg.`,
        confidenceLabel: 'Matched recent reps',
        prOpportunity: prOpportunity ?? undefined,
      } satisfies ExerciseSuggestion
    }
  }

  return {
    suggestedWeightKg: roundToNearestIncrement(recentAverage),
    reason: `Based on your last ${recentWindow.length} logged set${recentWindow.length === 1 ? '' : 's'}, ${recentAverage} kg is the current working baseline.`,
    confidenceLabel: 'Recent-set average',
    prOpportunity: prOpportunity ?? undefined,
  } satisfies ExerciseSuggestion
}

export function getWorkoutAssistantInsight(
  workouts: Workout[],
  goals: GoalSettings | null,
  now = new Date(),
): WorkoutAssistantInsight {
  const weekStart = startOfWeek(now)
  const weekEnd = addDays(weekStart, 7)

  const workoutsThisWeek = workouts.filter((workout) => {
    const workoutDate = new Date(workout.date)
    return workoutDate >= weekStart && workoutDate < weekEnd
  }).length

  const weeklyTarget = goals?.weeklyWorkoutTarget ?? null
  const weeklyNudge =
    weeklyTarget === null
      ? 'Set a weekly workout target to unlock consistency nudges.'
      : workoutsThisWeek >= weeklyTarget
        ? `Weekly goal met. You have logged ${workoutsThisWeek} of ${weeklyTarget} planned workouts this week.`
        : `${weeklyTarget - workoutsThisWeek} more workout${weeklyTarget - workoutsThisWeek === 1 ? '' : 's'} would keep you on track for this week.`

  const exerciseStats = buildExerciseStats(workouts)

  const prOpportunity =
    [...exerciseStats.values()]
      .filter((entry) => entry.recentBestWeightKg >= entry.personalBestWeightKg - 2.5)
      .sort((left, right) => right.recentBestWeightKg - left.recentBestWeightKg)[0] ?? null

  const revisitSuggestions = [...exerciseStats.values()]
    .filter((entry) => entry.daysSinceLastSession >= 7)
    .sort((left, right) => right.daysSinceLastSession - left.daysSinceLastSession)
    .slice(0, 3)
    .map((entry) => ({
      exerciseName: entry.exerciseName,
      lastTrainedAt: entry.lastSessionDate,
      message: `${entry.exerciseName} has not been trained for ${entry.daysSinceLastSession} day${entry.daysSinceLastSession === 1 ? '' : 's'}.`,
    }))

  return {
    weeklyNudge,
    prOpportunity: prOpportunity
      ? {
          exerciseName: prOpportunity.exerciseName,
          message:
            prOpportunity.recentBestWeightKg >= prOpportunity.personalBestWeightKg
              ? `${prOpportunity.exerciseName} is already matching your best recent work. Consider a small PR attempt if session quality is good.`
              : `${prOpportunity.exerciseName} is within ${Number((prOpportunity.personalBestWeightKg - prOpportunity.recentBestWeightKg).toFixed(1))} kg of your best lift.`,
          targetWeightKg: getSuggestedIncrease(prOpportunity.personalBestWeightKg),
        }
      : null,
    revisitSuggestions,
  }
}

function getSuggestedIncrease(weightKg: number) {
  return roundToNearestIncrement(weightKg >= 20 ? weightKg + 2.5 : weightKg + 1)
}

function roundToNearestIncrement(value: number) {
  return Math.round(value * 2) / 2
}

function normalizeExerciseName(exerciseName: string) {
  return exerciseName.trim().toUpperCase()
}

function getPrOpportunity(currentWeightKg: number, personalBestWeightKg: number) {
  if (currentWeightKg >= personalBestWeightKg) {
    return {
      message: 'You are already at your best logged weight. A small increase could create a new PR.',
      targetWeightKg: getSuggestedIncrease(personalBestWeightKg),
    }
  }

  if (currentWeightKg >= personalBestWeightKg - 2.5) {
    return {
      message: `You are within ${Number((personalBestWeightKg - currentWeightKg).toFixed(1))} kg of your best logged weight.`,
      targetWeightKg: personalBestWeightKg,
    }
  }

  return null
}

function buildExerciseStats(workouts: Workout[]) {
  const stats = new Map<
    string,
    {
      exerciseName: string
      personalBestWeightKg: number
      recentBestWeightKg: number
      lastSessionDate: string
      daysSinceLastSession: number
    }
  >()

  const now = new Date()

  for (const workout of workouts) {
    for (const exercise of workout.exerciseEntries) {
      const key = normalizeExerciseName(exercise.exerciseName)
      const workoutDate = workout.date
      const bestSetWeight = Math.max(...exercise.sets.map((set) => set.weightKg))
      const current = stats.get(key)

      if (!current) {
        stats.set(key, {
          exerciseName: exercise.exerciseName,
          personalBestWeightKg: exercise.personalRecordWeightKg,
          recentBestWeightKg: bestSetWeight,
          lastSessionDate: workoutDate,
          daysSinceLastSession: getDaysSince(workoutDate, now),
        })
        continue
      }

      const isNewer = new Date(workoutDate).getTime() > new Date(current.lastSessionDate).getTime()

      stats.set(key, {
        exerciseName: current.exerciseName,
        personalBestWeightKg: Math.max(current.personalBestWeightKg, exercise.personalRecordWeightKg),
        recentBestWeightKg: isNewer ? bestSetWeight : current.recentBestWeightKg,
        lastSessionDate: isNewer ? workoutDate : current.lastSessionDate,
        daysSinceLastSession: isNewer ? getDaysSince(workoutDate, now) : current.daysSinceLastSession,
      })
    }
  }

  return stats
}

function getDaysSince(date: string, now: Date) {
  const target = new Date(date)
  target.setHours(0, 0, 0, 0)
  const current = new Date(now)
  current.setHours(0, 0, 0, 0)
  return Math.max(0, Math.round((current.getTime() - target.getTime()) / 86400000))
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
