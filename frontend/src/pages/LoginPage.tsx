import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getRequestErrorMessage } from '../lib/http'

type LoginFormState = {
  email: string
  password: string
}

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { login } = useAuth()
  const [form, setForm] = useState<LoginFormState>({
    email: '',
    password: '',
  })
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const isSubmitDisabled = isSubmitting || form.email.trim() === '' || form.password === ''

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    try {
      setIsSubmitting(true)
      setErrorMessage(null)
      await login({
        email: form.email.trim(),
        password: form.password,
      })

      const redirectTo =
        typeof location.state === 'object' &&
        location.state &&
        'from' in location.state &&
        typeof location.state.from === 'string'
          ? location.state.from
          : '/dashboard'

      navigate(redirectTo, { replace: true })
    } catch (error) {
      setErrorMessage(getRequestErrorMessage(error, 'Unable to log in.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="auth-shell">
      <section className="auth-card">
        <div className="auth-copy">
          <span className="eyebrow">Gym Tracker</span>
          <h1>Welcome back</h1>
          <p className="hero-text">
            Sign in to continue with your workouts, weight history, goals, and active session.
          </p>
          <p className="auth-supporting-text">
            Use the email and password linked to your account.
          </p>
        </div>

        <form className="weight-form auth-form" onSubmit={handleSubmit} noValidate>
          <label className="field">
            <span>Email address</span>
            <input
              type="email"
              autoComplete="email"
              inputMode="email"
              value={form.email}
              onChange={(event) => {
                setForm((current) => ({ ...current, email: event.target.value }))
                if (errorMessage) {
                  setErrorMessage(null)
                }
              }}
              placeholder="you@example.com"
              aria-invalid={Boolean(errorMessage)}
              required
            />
            <small>Use the same email you registered with.</small>
          </label>

          <label className="field">
            <span>Password</span>
            <input
              type="password"
              autoComplete="current-password"
              value={form.password}
              onChange={(event) => {
                setForm((current) => ({ ...current, password: event.target.value }))
                if (errorMessage) {
                  setErrorMessage(null)
                }
              }}
              minLength={8}
              placeholder="Enter your password"
              aria-invalid={Boolean(errorMessage)}
              required
            />
            <small>Passwords are case-sensitive.</small>
          </label>

          <button type="submit" className="primary-button auth-submit-button" disabled={isSubmitDisabled}>
            {isSubmitting ? 'Signing you in...' : 'Log in'}
          </button>
        </form>

        {errorMessage ? (
          <p className="feedback error auth-feedback" role="alert">
            {errorMessage}
          </p>
        ) : null}

        <p className="auth-footer">
          Need an account? <Link to="/register">Register</Link>
        </p>
      </section>
    </main>
  )
}
