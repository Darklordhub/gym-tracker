import { apiClient } from '../lib/http'
import type { TrainingRecommendation } from '../types/trainingIntelligence'

export async function fetchDailyTrainingRecommendation() {
  const response = await apiClient.get<TrainingRecommendation>('/TrainingIntelligence/daily-recommendation')
  return response.data
}
