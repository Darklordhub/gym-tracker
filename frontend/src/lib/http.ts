import axios from 'axios'

let getToken: (() => string | null) | null = null
let onUnauthorized: (() => void) | null = null

function normalizeApiBaseUrl() {
  const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()

  if (!configuredApiBaseUrl) {
    return '/api'
  }

  return configuredApiBaseUrl.endsWith('/')
    ? configuredApiBaseUrl.slice(0, -1)
    : configuredApiBaseUrl
}

export const apiClient = axios.create({
  baseURL: normalizeApiBaseUrl(),
})

apiClient.interceptors.request.use((config) => {
  const token = getToken?.()

  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401 && getToken?.()) {
      onUnauthorized?.()
    }

    return Promise.reject(error)
  },
)

export function setTokenGetter(nextGetter: (() => string | null) | null) {
  getToken = nextGetter

  return () => {
    if (getToken === nextGetter) {
      getToken = null
    }
  }
}

export function setUnauthorizedHandler(nextHandler: (() => void) | null) {
  onUnauthorized = nextHandler

  return () => {
    if (onUnauthorized === nextHandler) {
      onUnauthorized = null
    }
  }
}

export function isNotFoundError(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 404
}

export function isForbiddenError(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 403
}

export function getRequestErrorMessage(error: unknown, fallbackMessage: string) {
  if (axios.isAxiosError(error)) {
    const apiMessage = error.response?.data?.message
    if (typeof apiMessage === 'string' && apiMessage.trim()) {
      return apiMessage
    }

    const apiErrors = error.response?.data?.errors

    if (apiErrors && typeof apiErrors === 'object') {
      const firstError = Object.values(apiErrors).flat().find(Boolean)
      if (typeof firstError === 'string') {
        return firstError
      }
    }

    const apiTitle = error.response?.data?.title
    if (typeof apiTitle === 'string' && apiTitle.trim()) {
      return apiTitle
    }
  }

  return fallbackMessage
}
