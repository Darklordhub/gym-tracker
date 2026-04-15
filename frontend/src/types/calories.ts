export type CalorieLog = {
  id: number
  date: string
  caloriesConsumed: number
  notes: string | null
  createdAt: string
  updatedAt: string
}

export type CalorieLogPayload = {
  date: string
  caloriesConsumed: number
  notes: string | null
}
