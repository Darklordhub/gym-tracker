export type AuthUser = {
  id: number
  email: string
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
