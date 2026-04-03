export type ExerciseEntry = {
  id: number
  exerciseName: string
  sets: number
  reps: number
  weightKg: number
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
