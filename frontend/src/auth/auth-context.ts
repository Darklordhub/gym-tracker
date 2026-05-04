import { createContext } from 'react'
import type { AuthUser, LoginPayload, RegisterPayload } from '../types/auth'

export type StoredAuthState = {
  token: string
  expiresAtUtc: string
  user: AuthUser
}

export type AuthContextValue = {
  authState: StoredAuthState | null
  isAuthenticated: boolean
  isInitializing: boolean
  login: (payload: LoginPayload) => Promise<void>
  register: (payload: RegisterPayload) => Promise<void>
  setCurrentUser: (user: AuthUser) => void
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
