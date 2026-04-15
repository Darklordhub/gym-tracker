export type FitnessPhase = 'cut' | 'maintain' | 'bulk'
export type CalorieTargetMode = 'manual' | 'goal-based'

export type GoalSettings = {
  targetBodyWeightKg: number | null
  weeklyWorkoutTarget: number | null
  fitnessPhase: FitnessPhase
  dailyCalorieTarget: number | null
  calorieTargetMode: CalorieTargetMode
}

export type GoalSettingsPayload = GoalSettings
