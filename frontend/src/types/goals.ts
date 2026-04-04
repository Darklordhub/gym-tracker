export type FitnessPhase = 'cut' | 'maintain' | 'bulk'

export type GoalSettings = {
  targetBodyWeightKg: number | null
  weeklyWorkoutTarget: number | null
  fitnessPhase: FitnessPhase
}

export type GoalSettingsPayload = GoalSettings
