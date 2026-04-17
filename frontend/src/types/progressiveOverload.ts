export type ProgressiveOverloadStatus = 'increase' | 'hold' | 'deload'

export type ProgressiveOverloadRecommendation = {
  exerciseName: string
  recommendedWeightKg: number | null
  recommendedRepTarget: string
  progressionStatus: ProgressiveOverloadStatus
  shortReason: string
  relevantSessionCount: number
  latestWorkingWeightKg: number | null
  recentBestWeightKg: number | null
}
