import type { Workout } from '../types/workout'

export type ExerciseSuggestion = {
  suggestedWeightKg: number
  reason: string
}

export type ExerciseSetHistoryItem = {
  date: string
  setOrder: number
  reps: number
  weightKg: number
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

  if (setOrder && reps) {
    const exactMatch = history.find((entry) => entry.setOrder === setOrder && entry.reps === reps)

    if (exactMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(exactMatch.weightKg),
        reason: `Last set ${setOrder} for ${reps} reps was ${exactMatch.weightKg} kg.`,
      } satisfies ExerciseSuggestion
    }
  }

  if (reps) {
    const repMatch = history.find((entry) => entry.reps === reps)

    if (repMatch) {
      return {
        suggestedWeightKg: getSuggestedIncrease(repMatch.weightKg),
        reason: `Last ${reps}-rep set was ${repMatch.weightKg} kg.`,
      } satisfies ExerciseSuggestion
    }
  }

  return {
    suggestedWeightKg: roundToNearestIncrement(latest.weightKg),
    reason: `Repeat your latest logged set at ${latest.weightKg} kg.`,
  } satisfies ExerciseSuggestion
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
