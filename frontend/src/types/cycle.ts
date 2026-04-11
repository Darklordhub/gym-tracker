export type CycleRegularity = 'regular' | 'somewhat-irregular' | 'irregular'

export type CycleSettings = {
  isEnabled: boolean
  lastPeriodStartDate: string | null
  averageCycleLengthDays: number | null
  averagePeriodLengthDays: number | null
  cycleRegularity: CycleRegularity
  usesHormonalContraception: boolean | null
  isNaturallyCycling: boolean | null
  updatedAt: string | null
}

export type CycleSettingsPayload = {
  isEnabled: boolean
  lastPeriodStartDate: string | null
  averageCycleLengthDays: number | null
  averagePeriodLengthDays: number | null
  cycleRegularity: CycleRegularity
  usesHormonalContraception: boolean | null
  isNaturallyCycling: boolean | null
}

export type CycleEntry = {
  id: number
  periodStartDate: string
  periodEndDate: string
  notes: string | null
  createdAt: string
}

export type CycleEntryPayload = {
  periodStartDate: string
  periodEndDate: string
  notes: string | null
}

export type CycleGuidance = {
  isEnabled: boolean
  currentCycleDay: number | null
  estimatedCurrentPhase: string | null
  estimatedNextPeriodStartDate: string | null
  predictionConfidence: string
  guidanceHeadline: string
  guidanceMessage: string
  recentLoadLabel: string
  recentWorkoutCount: number
  recentSetCount: number
  recentTrainingLoad: number
  isHigherFatiguePhase: boolean
}
