import axios from 'axios'

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

export function isNotFoundError(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 404
}

export function getRequestErrorMessage(error: unknown, fallbackMessage: string) {
  if (axios.isAxiosError(error)) {
    const apiMessage = error.response?.data?.message
    if (typeof apiMessage === 'string' && apiMessage.trim()) {
      return apiMessage
    }

    const apiTitle = error.response?.data?.title
    if (typeof apiTitle === 'string' && apiTitle.trim()) {
      return apiTitle
    }

    const apiErrors = error.response?.data?.errors

    if (apiErrors && typeof apiErrors === 'object') {
      const firstError = Object.values(apiErrors).flat().find(Boolean)
      if (typeof firstError === 'string') {
        return firstError
      }
    }
  }

  return fallbackMessage
}
