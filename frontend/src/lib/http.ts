import axios from 'axios'

export function getRequestErrorMessage(error: unknown, fallbackMessage: string) {
  if (axios.isAxiosError(error)) {
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
