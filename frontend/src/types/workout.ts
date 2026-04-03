export type ExerciseEntry = {
  id: number
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
  isPersonalRecord: boolean
  personalRecordWeightKg: number
}

export type Workout = {
  id: number
  date: string
  notes: string
  exerciseEntries: ExerciseEntry[]
}

export type ExerciseEntryPayload = {
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
}

export type WorkoutPayload = {
  date: string
  notes: string
  exerciseEntries: ExerciseEntryPayload[]
}

export type WorkoutTemplateExerciseEntry = {
  id: number
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
}

export type WorkoutTemplate = {
  id: number
  name: string
  notes: string
  exerciseEntries: WorkoutTemplateExerciseEntry[]
}

export type WorkoutTemplateExerciseEntryPayload = {
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
}

export type WorkoutTemplatePayload = {
  name: string
  notes: string
  exerciseEntries: WorkoutTemplateExerciseEntryPayload[]
}

export type ActiveWorkoutSessionExerciseEntry = {
  id: number
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
}

export type ActiveWorkoutSession = {
  id: number
  startedAtUtc: string
  notes: string
  exerciseEntries: ActiveWorkoutSessionExerciseEntry[]
}

export type ActiveWorkoutSessionPayload = {
  notes: string
  exerciseEntries: ExerciseEntryPayload[]
}
