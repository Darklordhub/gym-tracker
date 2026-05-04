export type CalorieLog = {
  id: number
  date: string
  caloriesConsumed: number
  sourceMode?: string | null
  totalProtein?: number | null
  totalCarbs?: number | null
  totalFat?: number | null
  lastRolledUpAt?: string | null
  notes: string | null
  createdAt: string
  updatedAt: string
}

export type CalorieLogPayload = {
  date: string
  caloriesConsumed: number
  notes: string | null
}
