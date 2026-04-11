export type WorkoutType = 'strength' | 'cardio'
export type CardioActivityType = 'walking' | 'running' | 'cycling' | 'other'
export type CardioIntensity = 'low' | 'moderate' | 'high'

export type ExerciseSet = {
  id: number
  order: number
  reps: number
  weightKg: number
}

export type ExerciseSetPayload = {
  reps: number
  weightKg: number
}

export type ExerciseEntry = {
  id: number
  exerciseName: string
  sets: ExerciseSet[]
  isPersonalRecord: boolean
  personalRecordWeightKg: number
}

export type Workout = {
  id: number
  date: string
  workoutType: WorkoutType
  notes: string
  cardioActivityType: CardioActivityType | null
  cardioDurationMinutes: number | null
  cardioDistanceKm: number | null
  cardioIntensity: CardioIntensity | null
  exerciseEntries: ExerciseEntry[]
}

export type ExerciseEntryPayload = {
  exerciseName: string
  sets: ExerciseSetPayload[]
}

export type WorkoutPayload = {
  date: string
  workoutType?: WorkoutType
  notes: string
  exerciseEntries: ExerciseEntryPayload[]
  cardioActivityType?: CardioActivityType | null
  cardioDurationMinutes?: number | null
  cardioDistanceKm?: number | null
  cardioIntensity?: CardioIntensity | null
}

export type WorkoutTemplateExerciseEntry = {
  id: number
  exerciseName: string
  sets: ExerciseSet[]
}

export type WorkoutTemplate = {
  id: number
  name: string
  notes: string
  exerciseEntries: WorkoutTemplateExerciseEntry[]
}

export type WorkoutTemplateExerciseEntryPayload = {
  exerciseName: string
  sets: ExerciseSetPayload[]
}

export type WorkoutTemplatePayload = {
  name: string
  notes: string
  exerciseEntries: WorkoutTemplateExerciseEntryPayload[]
}

export type ActiveWorkoutSessionExerciseEntry = {
  id: number
  exerciseName: string
  sets: ExerciseSet[]
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
