export type AiWorkoutGeneratePayload = {
  goal: string
  preferredWorkoutType?: string | null
  durationMinutes?: number | null
  fitnessLevel?: string | null
  targetMuscles?: string[] | null
  excludedExercises?: string[] | null
  includeWarmup: boolean
  includeCooldown: boolean
}

export type AiWorkoutExercise = {
  exerciseCatalogItemId: number | null
  name: string
  category: string | null
  targetMuscle: string | null
  sets: number
  reps: string
  suggestedWeight: string | null
  restSeconds: number
  instructions: string
  thumbnailUrl: string | null
  videoUrl: string | null
}

export type AiWorkoutSection = {
  name: string
  exercises: AiWorkoutExercise[]
}

export type AiWorkoutPlan = {
  title: string
  goal: string
  workoutType: string
  estimatedDurationMinutes: number
  difficulty: string
  sections: AiWorkoutSection[]
  notes: string[]
}
