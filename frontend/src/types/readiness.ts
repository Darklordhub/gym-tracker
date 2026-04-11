export type ReadinessLog = {
  id: number
  date: string
  energyLevel: number
  sorenessLevel: number
  sleepQuality: number
  motivationLevel: number
  notes: string | null
  readinessLabel: string
  readinessScore: number
  createdAt: string
  updatedAt: string
}

export type ReadinessLogPayload = {
  date: string
  energyLevel: number
  sorenessLevel: number
  sleepQuality: number
  motivationLevel: number
  notes: string | null
}
