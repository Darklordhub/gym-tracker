import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { fetchCurrentUser, login as loginRequest, register as registerRequest } from '../api/auth'
import { AuthContext, type AuthContextValue, type StoredAuthState } from './auth-context'
import { setTokenGetter, setUnauthorizedHandler } from '../lib/http'
import type { AuthResponse, AuthUser } from '../types/auth'

const storageKey = 'gym-tracker-auth'

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

function areAuthUsersEqual(left: AuthUser, right: AuthUser) {
  return left.id === right.id
    && left.email === right.email
    && left.fullName === right.fullName
    && left.displayName === right.displayName
    && left.dateOfBirth === right.dateOfBirth
    && left.heightCm === right.heightCm
    && left.gender === right.gender
    && left.role === right.role
    && left.isActive === right.isActive
    && left.createdAt === right.createdAt
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

  const handleAuthResponse = useCallback(async (request: Promise<AuthResponse>) => {
    const response = await request
    const nextAuthState = toStoredAuthState(response)
    setAuthState(nextAuthState)
    persistAuthState(nextAuthState)
  }, [])

  const login = useCallback(async (payload: Parameters<typeof loginRequest>[0]) => {
    await handleAuthResponse(loginRequest(payload))
  }, [handleAuthResponse])

  const register = useCallback(async (payload: Parameters<typeof registerRequest>[0]) => {
    await handleAuthResponse(registerRequest(payload))
  }, [handleAuthResponse])

  const logout = useCallback(() => {
    setAuthState(null)
    persistAuthState(null)
  }, [])

  const setCurrentUser = useCallback((user: AuthUser) => {
    setAuthState((current) => {
      if (!current) {
        return current
      }

      const normalizedUser = normalizeAuthUser(user)
      if (areAuthUsersEqual(current.user, normalizedUser)) {
        return current
      }

      const nextAuthState: StoredAuthState = {
        ...current,
        user: normalizedUser,
      }

      persistAuthState(nextAuthState)
      return nextAuthState
    })
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      authState,
      isAuthenticated: authState !== null,
      isInitializing,
      login,
      register,
      setCurrentUser,
      logout,
    }),
    [authState, isInitializing, login, logout, register, setCurrentUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
