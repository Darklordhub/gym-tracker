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

export type ExerciseMediaDraftStatus =
  | 'Queued'
  | 'Generating'
  | 'Generated'
  | 'NeedsReview'
  | 'Approved'
  | 'Rejected'
  | 'Published'
  | 'Failed'
  | 'Archived'

export type ExerciseMediaDraftMediaType = 'Video' | 'Thumbnail' | 'Image'

export type ExerciseMediaDraftResponse = {
  id: number
  exerciseCatalogItemId: number
  exerciseName: string
  exerciseSource: string
  status: ExerciseMediaDraftStatus | string
  mediaType: ExerciseMediaDraftMediaType | string
  promptText: string
  promptVersion: string | null
  sourceSnapshotJson: string | null
  generatedThumbnailUrl: string | null
  generatedVideoUrl: string | null
  generationProvider: string | null
  generationModel: string | null
  providerJobId: string | null
  reviewNotes: string | null
  rejectionReason: string | null
  createdByUserId: number | null
  reviewedByUserId: number | null
  publishedByUserId: number | null
  errorMessage: string | null
  createdAt: string
  updatedAt: string
  generatedAt: string | null
  reviewedAt: string | null
  publishedAt: string | null
}

export type ExerciseMediaStudioExerciseResponse = {
  id: number
  name: string
  providerName: string
  source: string
  externalId: string | null
  instructions: string | null
  primaryMuscle: string | null
  secondaryMuscles: string[]
  equipment: string | null
  difficulty: string | null
  videoUrl: string | null
  providerVideoUrl: string | null
  localVideoUrlOverride: string | null
  thumbnailUrl: string | null
  providerThumbnailUrl: string | null
  localThumbnailUrlOverride: string | null
  localMediaPath: string | null
  isActive: boolean
  isManuallyEdited: boolean
  latestDrafts: ExerciseMediaDraftResponse[]
}

export type CreateExerciseMediaDraftRequest = {
  mediaType?: string | null
}

export type ReviewExerciseMediaDraftRequest = {
  reviewNotes?: string | null
}

export type RejectExerciseMediaDraftRequest = {
  reviewNotes?: string | null
  rejectionReason?: string | null
}
