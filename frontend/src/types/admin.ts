export type AdminUser = {
  id: number
  email: string
  fullName: string
  displayName: string | null
  role: 'User' | 'Admin'
  isActive: boolean
  createdAt: string
}

export type UpdateAdminUserRolePayload = {
  role: AdminUser['role']
}

export type UpdateAdminUserStatusPayload = {
  isActive: boolean
}

export type ResetAdminUserPasswordPayload = {
  newPassword: string
}

export type ExerciseCatalogSyncResult = {
  provider: string
  isEnabled: boolean
  processed: number
  created: number
  updated: number
  skipped: number
  message: string
}

export type AdminExerciseCatalogItem = {
  id: number
  source: string
  externalId: string | null
  name: string
  providerName: string
  slug: string
  description: string | null
  instructions: string | null
  providerInstructions: string | null
  primaryMuscle: string | null
  secondaryMuscles: string[]
  equipment: string | null
  difficulty: string | null
  videoUrl: string | null
  providerVideoUrl: string | null
  thumbnailUrl: string | null
  providerThumbnailUrl: string | null
  isActive: boolean
  isManuallyEdited: boolean
  lastSyncedAt: string | null
  lastEditedAt: string | null
  createdAt: string
  updatedAt: string
}

export type UpdateExerciseCatalogItemPayload = {
  name: string
  instructions: string
  thumbnailUrl: string
  videoUrl: string
  isActive: boolean
}
