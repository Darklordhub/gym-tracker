import type { AuthUser } from './auth'

export type UserProfile = AuthUser

export type UpdateProfilePayload = {
  fullName: string
  displayName: string | null
  dateOfBirth: string | null
  heightCm: number | null
  gender: string | null
}

export type ChangePasswordPayload = {
  currentPassword: string
  newPassword: string
}
