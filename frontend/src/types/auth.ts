export type AuthUser = {
  id: number
  email: string
  fullName: string
  displayName: string | null
  dateOfBirth: string | null
  heightCm: number | null
  gender: string | null
  createdAt: string
}

export type AuthResponse = {
  token: string
  expiresAtUtc: string
  user: AuthUser
}

export type LoginPayload = {
  email: string
  password: string
}

export type RegisterPayload = {
  email: string
  password: string
}
