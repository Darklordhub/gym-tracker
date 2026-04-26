export type ExerciseCatalogItem = {
  id: number
  source: string
  externalId: string | null
  name: string
  slug: string
  description: string | null
  instructions: string | null
  primaryMuscle: string | null
  secondaryMuscles: string[]
  equipment: string | null
  difficulty: string | null
  videoUrl: string | null
  thumbnailUrl: string | null
  localMediaPath: string | null
  isActive: boolean
  lastSyncedAt: string | null
  createdAt: string
  updatedAt: string
}

export type ExerciseCatalogPage = {
  items: ExerciseCatalogItem[]
  page: number
  pageSize: number
  totalCount: number
}
