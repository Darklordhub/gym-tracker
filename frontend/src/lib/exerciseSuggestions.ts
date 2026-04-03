import type { Workout } from '../types/workout'

export type ExerciseSuggestion = {
  suggestedWeightKg: number
  reason: string
}

type ExerciseHistoryItem = {
  date: string
  sets: number
  reps: number
  weightKg: number
}

export function getExerciseHistory(workouts: Workout[], exerciseName: string) {
  const normalizedExerciseName = normalizeExerciseName(exerciseName)

  return workouts
    .flatMap<ExerciseHistoryItem>((workout) =>
      workout.exerciseEntries
        .filter((exercise) => normalizeExerciseName(exercise.exerciseName) === normalizedExerciseName)
        .map((exercise) => ({
          date: workout.date,
          sets: exercise.sets,
          reps: exercise.reps,
          weightKg: exercise.weightKg,
        })),
    )
    .sort((left, right) => new Date(right.date).getTime() - new Date(left.date).getTime())
}

export function getSuggestedNextWeight(
  workouts: Workout[],
  exerciseName: string,
  sets: number | null,
  reps: number | null,
) {
  const history = getExerciseHistory(workouts, exerciseName)

  if (history.length === 0) {
    return null
  }

  const latest = history[0]

  if (sets && reps) {
    const matchingStructure = history.find((entry) => entry.sets === sets && entry.reps === reps)

    if (matchingStructure) {
      const suggestedWeightKg = roundToNearestIncrement(
        matchingStructure.weightKg >= 20
          ? matchingStructure.weightKg + 2.5
          : matchingStructure.weightKg + 1,
      )

      return {
        suggestedWeightKg,
        reason: `Last ${sets} x ${reps} was ${matchingStructure.weightKg} kg.`,
      } satisfies ExerciseSuggestion
    }
  }

  return {
    suggestedWeightKg: roundToNearestIncrement(latest.weightKg),
    reason: `Repeat your latest logged weight of ${latest.weightKg} kg.`,
  } satisfies ExerciseSuggestion
}

function roundToNearestIncrement(value: number) {
  return Math.round(value * 2) / 2
}

function normalizeExerciseName(exerciseName: string) {
  return exerciseName.trim().toUpperCase()
}
