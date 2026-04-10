import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { fetchCurrentUser, login as loginRequest, register as registerRequest } from '../api/auth'
import { setTokenGetter, setUnauthorizedHandler } from '../lib/http'
import type { AuthResponse, AuthUser, LoginPayload, RegisterPayload } from '../types/auth'

type StoredAuthState = {
  token: string
  expiresAtUtc: string
  user: AuthUser
}

type AuthContextValue = {
  authState: StoredAuthState | null
  isAuthenticated: boolean
  isInitializing: boolean
  login: (payload: LoginPayload) => Promise<void>
  register: (payload: RegisterPayload) => Promise<void>
  setCurrentUser: (user: AuthUser) => void
  logout: () => void
}

const storageKey = 'gym-tracker-auth'

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function readStoredAuthState(): StoredAuthState | null {
  const rawValue = window.localStorage.getItem(storageKey)

  if (!rawValue) {
    return null
  }

  try {
    const parsedValue = JSON.parse(rawValue) as StoredAuthState
    if (!parsedValue.token || !parsedValue.user?.email) {
      return null
    }

    return {
      ...parsedValue,
      user: normalizeAuthUser(parsedValue.user),
    }
  } catch {
    return null
  }
}

function persistAuthState(authState: StoredAuthState | null) {
  if (authState) {
    window.localStorage.setItem(storageKey, JSON.stringify(authState))
    return
  }

  window.localStorage.removeItem(storageKey)
}

function toStoredAuthState(response: AuthResponse): StoredAuthState {
  return {
    token: response.token,
    expiresAtUtc: response.expiresAtUtc,
    user: normalizeAuthUser(response.user),
  }
}

function normalizeAuthUser(user: AuthUser): AuthUser {
  return {
    ...user,
    fullName: user.fullName ?? '',
    displayName: user.displayName ?? null,
    dateOfBirth: user.dateOfBirth ?? null,
    heightCm: user.heightCm ?? null,
    gender: user.gender ?? null,
    role: user.role ?? 'User',
    isActive: user.isActive ?? true,
  }
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [authState, setAuthState] = useState<StoredAuthState | null>(() => readStoredAuthState())
  const [isInitializing, setIsInitializing] = useState(() => readStoredAuthState() !== null)

  useEffect(() => {
    const cleanup = setTokenGetter(() => authState?.token ?? null)
    return cleanup
  }, [authState])

  useEffect(() => {
    const cleanup = setUnauthorizedHandler(() => {
      setAuthState(null)
      persistAuthState(null)
    })

    return cleanup
  }, [])

  useEffect(() => {
    const currentAuthState = readStoredAuthState()

    if (!currentAuthState) {
      setIsInitializing(false)
      return
    }

    const storedAuthState = currentAuthState
    let isCancelled = false

    async function validateSession() {
      try {
        const currentUser = await fetchCurrentUser()

        if (isCancelled) {
          return
        }

        const nextAuthState: StoredAuthState = {
          token: storedAuthState.token,
          expiresAtUtc: storedAuthState.expiresAtUtc,
          user: normalizeAuthUser(currentUser),
        }

        setAuthState(nextAuthState)
        persistAuthState(nextAuthState)
      } catch {
        if (isCancelled) {
          return
        }

        setAuthState(null)
        persistAuthState(null)
      } finally {
        if (!isCancelled) {
          setIsInitializing(false)
        }
      }
    }

    void validateSession()

    return () => {
      isCancelled = true
    }
  }, [])

  async function handleAuthResponse(request: Promise<AuthResponse>) {
    const response = await request
    const nextAuthState = toStoredAuthState(response)
    setAuthState(nextAuthState)
    persistAuthState(nextAuthState)
  }

  function logout() {
    setAuthState(null)
    persistAuthState(null)
  }

  function setCurrentUser(user: AuthUser) {
    setAuthState((current) => {
      if (!current) {
        return current
      }

      const nextAuthState: StoredAuthState = {
        ...current,
        user: normalizeAuthUser(user),
      }

      persistAuthState(nextAuthState)
      return nextAuthState
    })
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      authState,
      isAuthenticated: authState !== null,
      isInitializing,
      login: async (payload) => {
        await handleAuthResponse(loginRequest(payload))
      },
      register: async (payload) => {
        await handleAuthResponse(registerRequest(payload))
      },
      setCurrentUser,
      logout,
    }),
    [authState, isInitializing],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)

  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.')
  }

  return context
}
