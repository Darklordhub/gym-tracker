export type RecommendedSessionType = 'strength' | 'cardio' | 'rest'
export type TrainingRecommendationIntensity = 'low' | 'moderate' | 'high'
export type TrainingRecommendationFatigue = 'low' | 'moderate' | 'high'

export type TrainingRecommendation = {
  date: string
  recommendedSessionType: RecommendedSessionType
  intensity: TrainingRecommendationIntensity
  fatigueLevel: TrainingRecommendationFatigue
  shortReason: string
  goalContext: string
  recentWorkoutCount: number
  recentStrengthWorkoutCount: number
  recentCardioWorkoutCount: number
  weeklyLoadScore: number
  readinessScore: number | null
  netCaloriesToday: number | null
}
