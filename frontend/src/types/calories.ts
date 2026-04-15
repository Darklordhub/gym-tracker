export type CalorieLog = {
  id: number
  date: string
  caloriesConsumed: number
  createdAt: string
  updatedAt: string
}

export type CalorieLogPayload = {
  date: string
  caloriesConsumed: number
}
