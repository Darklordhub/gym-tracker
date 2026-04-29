import type { AiWorkoutExercise, AiWorkoutPlan } from '../types/aiWorkout'
import type { ActiveWorkoutSessionPayload, ExerciseEntryPayload, ExerciseSetPayload, WorkoutPayload } from '../types/workout'

const MAX_NOTES_LENGTH = 500

export function buildWorkoutPayloadFromAiPlan(plan: AiWorkoutPlan, date: string): WorkoutPayload {
  return {
    date,
    workoutType: 'strength',
    notes: buildAiPlanNotes(plan),
    exerciseEntries: buildAiPlanExerciseEntries(plan),
  }
}

export function buildActiveWorkoutSessionPayloadFromAiPlan(plan: AiWorkoutPlan): ActiveWorkoutSessionPayload {
  return {
    notes: buildAiPlanNotes(plan),
    exerciseEntries: buildAiPlanExerciseEntries(plan),
  }
}

export function hasLoggableAiWorkoutExercises(plan: AiWorkoutPlan) {
  return buildAiPlanExerciseEntries(plan).length > 0
}

function buildAiPlanExerciseEntries(plan: AiWorkoutPlan): ExerciseEntryPayload[] {
  return getLoggablePlanExercises(plan)
    .map((exercise) => ({
      exerciseName: exercise.name.trim(),
      sets: buildExerciseSetPayloads(exercise),
    }))
    .filter((exercise) => exercise.exerciseName && exercise.sets.length > 0)
}

function buildExerciseSetPayloads(exercise: AiWorkoutExercise): ExerciseSetPayload[] {
  return exercise.suggestedSets
    .slice(0, 10)
    .map((set) => ({
      reps: clampInteger(Math.round(set.reps), 1, 100),
      weightKg: clampDecimal(set.weightKg, 0, 500),
    }))
}

function getLoggablePlanExercises(plan: AiWorkoutPlan): AiWorkoutExercise[] {
  const allExercises = plan.sections.flatMap((section) => section.exercises)
  const primaryExercises = allExercises.filter((exercise) => !isSupportExerciseCategory(exercise.category))

  return primaryExercises.length > 0 ? primaryExercises : allExercises
}

function isSupportExerciseCategory(category: string | null) {
  const normalizedCategory = category?.trim().toLowerCase()
  return normalizedCategory === 'warmup' || normalizedCategory === 'cooldown'
}

function buildAiPlanNotes(plan: AiWorkoutPlan) {
  const noteLines = [
    `AI Plan: ${plan.title}`,
    `${plan.goal} · ${plan.workoutType} · ${plan.difficulty} · ${plan.estimatedDurationMinutes} min`,
    ...buildSupportSectionNotes(plan),
    ...plan.notes.slice(0, 2),
  ].filter((line) => Boolean(line?.trim()))

  const combinedNotes = noteLines.join('\n').trim()
  return combinedNotes.length <= MAX_NOTES_LENGTH
    ? combinedNotes
    : `${combinedNotes.slice(0, MAX_NOTES_LENGTH - 1).trimEnd()}…`
}

function buildSupportSectionNotes(plan: AiWorkoutPlan) {
  return plan.sections
    .filter((section) => section.exercises.some((exercise) => isSupportExerciseCategory(exercise.category)))
    .map((section) => {
      const exerciseNames = section.exercises
        .map((exercise) => exercise.name.trim())
        .filter(Boolean)
        .join(', ')

      return exerciseNames ? `${section.name}: ${exerciseNames}` : section.name
    })
}

function clampInteger(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max)
}

function clampDecimal(value: number, min: number, max: number) {
  return Math.min(Math.max(Math.round(value * 10) / 10, min), max)
}
